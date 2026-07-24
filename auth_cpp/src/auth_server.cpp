#include "auth_server.h"

#include "captcha.h"
#include "password.h"
#include "protocol.h"

#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <unistd.h>

#include <cstdio>
#include <cstring>
#include <thread>
#include <vector>

AuthServer::AuthServer(AuthConfig cfg) : cfg_(std::move(cfg)), db_(cfg_) {}

void AuthServer::handle_client(int client_fd) {
    std::vector<uint8_t> buffer;
    uint8_t temp[4096];

    auto send_resp = [&](uint16_t msg_id, bool ok, const std::string& msg, uint32_t uid = 0, const std::string& token = "") {
        const auto payload = protocol::pack_login_resp(ok, msg, uid, token);
        const auto frame = protocol::pack_frame(msg_id, payload);
        send(client_fd, frame.data(), frame.size(), MSG_NOSIGNAL);
    };

    auto send_raw = [&](uint16_t msg_id, const std::vector<uint8_t>& payload) {
        const auto frame = protocol::pack_frame(msg_id, payload);
        send(client_fd, frame.data(), frame.size(), MSG_NOSIGNAL);
    };

    while (true) {
        const ssize_t n = recv(client_fd, temp, sizeof(temp), 0);
        if (n <= 0) {
            break;
        }
        protocol::append_bytes(buffer, temp, static_cast<size_t>(n));

        protocol::Packet packet;
        while (protocol::try_read_packet(buffer, packet)) {
            if (packet.msg_id == protocol::C2S_CaptchaReq) {
                const auto [id, question] = CaptchaStore::instance().create();
                send_raw(protocol::S2C_CaptchaResp, protocol::pack_captcha_resp(id, question));
                continue;
            }

            if (packet.msg_id == protocol::C2S_RegisterReq) {
                std::string username;
                std::string password;
                std::string captcha_id;
                std::string captcha_answer;
                if (!protocol::unpack_register_req(packet.payload, username, password, captcha_id, captcha_answer)) {
                    send_resp(protocol::S2C_RegisterResp, false, "bad register packet");
                    continue;
                }

                if (!CaptchaStore::instance().consume(captcha_id, captcha_answer)) {
                    send_resp(protocol::S2C_RegisterResp, false, "captcha invalid or expired");
                    continue;
                }

                std::string err;
                if (!password_util::validate_username(username, err) ||
                    !password_util::validate_password(password, err)) {
                    send_resp(protocol::S2C_RegisterResp, false, err);
                    continue;
                }
                if (db_.find_user(username)) {
                    send_resp(protocol::S2C_RegisterResp, false, "username already exists");
                    continue;
                }
                const std::string hashed = password_util::hash(password);
                const auto uid_opt = db_.create_user(username, hashed);
                if (!uid_opt) {
                    send_resp(protocol::S2C_RegisterResp, false, "register failed");
                    continue;
                }
                const uint32_t uid = *uid_opt;
                db_.ensure_wallet(uid);
                const std::string token = password_util::make_token(uid, username);
                db_.save_session(token, uid, username, cfg_.session_ttl_seconds);
                std::printf("[auth] register ok user=%s uid=%u\n", username.c_str(), uid);
                send_resp(protocol::S2C_RegisterResp, true, "register success", uid, token);
                continue;
            }

            if (packet.msg_id == protocol::C2S_LoginReq) {
                std::string username;
                std::string password;
                if (!protocol::unpack_login_req(packet.payload, username, password)) {
                    send_resp(protocol::S2C_LoginResp, false, "bad packet");
                    continue;
                }
                std::string err;
                if (!password_util::validate_username(username, err)) {
                    send_resp(protocol::S2C_LoginResp, false, err);
                    continue;
                }
                if (password.empty()) {
                    send_resp(protocol::S2C_LoginResp, false, "password is empty");
                    continue;
                }
                const auto row = db_.find_user(username);
                if (!row || !password_util::verify(password, row->password)) {
                    send_resp(protocol::S2C_LoginResp, false, "invalid username or password");
                    continue;
                }
                db_.ensure_wallet(row->id);
                const std::string token = password_util::make_token(row->id, username);
                db_.save_session(token, row->id, username, cfg_.session_ttl_seconds);
                std::printf("[auth] login ok user=%s uid=%u\n", username.c_str(), row->id);
                send_resp(protocol::S2C_LoginResp, true, "login success", row->id, token);
                continue;
            }

            if (packet.msg_id == protocol::C2S_InventoryReq) {
                std::string token;
                size_t o = 0;
                if (!protocol::unpack_str8(packet.payload, o, token)) {
                    send_raw(protocol::S2C_InventoryResp, protocol::pack_inventory_resp(false, "bad packet", 0, 0, 0, 0, {}));
                    continue;
                }
                const auto session = db_.find_session(token);
                if (!session) {
                    send_raw(protocol::S2C_InventoryResp, protocol::pack_inventory_resp(false, "invalid or expired token", 0, 0, 0, 0, {}));
                    continue;
                }
                db_.ensure_wallet(session->uid);
                const auto wallet = db_.get_wallet(session->uid);
                const auto items = db_.get_inventory(session->uid);
                std::vector<protocol::InventoryItemView> views;
                views.reserve(items.size());
                for (const auto& it : items) {
                    views.push_back({it.item_id, it.count});
                }
                send_raw(protocol::S2C_InventoryResp,
                         protocol::pack_inventory_resp(true, "ok", wallet.gold, wallet.dust, wallet.diamond, wallet.ticket, views));
                continue;
            }

            if (packet.msg_id == protocol::C2S_ClaimRewardReq) {
                std::string token;
                std::string stage_key;
                std::string run_id;
                size_t o = 0;
                if (!protocol::unpack_str8(packet.payload, o, token)
                    || !protocol::unpack_str8(packet.payload, o, stage_key)
                    || !protocol::unpack_str8(packet.payload, o, run_id)) {
                    send_raw(protocol::S2C_ClaimRewardResp,
                             protocol::pack_claim_reward_resp(false, "bad packet", 0, 0, 0, 0, 0, 0, 0, 0));
                    continue;
                }
                const auto session = db_.find_session(token);
                if (!session) {
                    send_raw(protocol::S2C_ClaimRewardResp,
                             protocol::pack_claim_reward_resp(false, "invalid or expired token", 0, 0, 0, 0, 0, 0, 0, 0));
                    continue;
                }

                int32_t gold_delta = 0;
                int32_t dust_delta = 0;
                int32_t diamond_delta = 0;
                int32_t ticket_delta = 0;
                if (stage_key == "battle1_clear") {
                    gold_delta = 50;
                    dust_delta = 15;
                } else if (stage_key == "boss1_clear") {
                    gold_delta = 150;
                    dust_delta = 40;
                } else {
                    send_raw(protocol::S2C_ClaimRewardResp,
                             protocol::pack_claim_reward_resp(false, "unknown stage", 0, 0, 0, 0, 0, 0, 0, 0));
                    continue;
                }

                db_.ensure_wallet(session->uid);
                const bool claimed = db_.try_claim_stage(session->uid, stage_key, run_id, gold_delta, dust_delta);
                if (claimed) {
                    if (stage_key == "battle1_clear") {
                        db_.add_item(session->uid, 1, 1);  // 木宝箱
                        if (db_.try_claim_milestone(session->uid, "battle1_first")) {
                            diamond_delta = 80;
                            ticket_delta = 3;
                            db_.add_diamond(session->uid, diamond_delta);
                            db_.add_ticket(session->uid, ticket_delta);
                        }
                    } else if (stage_key == "boss1_clear") {
                        db_.add_item(session->uid, 2, 1);  // 铁宝箱
                        if (db_.try_claim_milestone(session->uid, "boss1_first")) {
                            diamond_delta = 150;
                            ticket_delta = 5;
                            db_.add_diamond(session->uid, diamond_delta);
                            db_.add_ticket(session->uid, ticket_delta);
                        }
                    }
                }
                const auto wallet = db_.get_wallet(session->uid);
                if (!claimed) {
                    send_raw(protocol::S2C_ClaimRewardResp,
                             protocol::pack_claim_reward_resp(true, "already claimed", 0, 0, 0, 0, wallet.gold, wallet.dust, wallet.diamond, wallet.ticket));
                } else {
                    std::printf("[auth] claim stage=%s uid=%u +%d gold +%d dust +%d diamond +%d ticket\n",
                                stage_key.c_str(), session->uid, gold_delta, dust_delta, diamond_delta, ticket_delta);
                    send_raw(protocol::S2C_ClaimRewardResp,
                             protocol::pack_claim_reward_resp(true, "reward granted", gold_delta, dust_delta, diamond_delta, ticket_delta, wallet.gold, wallet.dust, wallet.diamond, wallet.ticket));
                }
                continue;
            }

            if (packet.msg_id == protocol::C2S_ListUpgradesReq) {
                std::string token;
                size_t o = 0;
                if (!protocol::unpack_str8(packet.payload, o, token)) {
                    send_raw(protocol::S2C_ListUpgradesResp,
                             protocol::pack_list_upgrades_resp(false, "bad packet", 0, 0, 0, 0, {}));
                    continue;
                }
                const auto session = db_.find_session(token);
                if (!session) {
                    send_raw(protocol::S2C_ListUpgradesResp,
                             protocol::pack_list_upgrades_resp(false, "invalid or expired token", 0, 0, 0, 0, {}));
                    continue;
                }
                db_.ensure_wallet(session->uid);
                const auto wallet = db_.get_wallet(session->uid);
                const auto levels = db_.list_card_levels(session->uid);
                std::vector<protocol::CardUpgradeView> views;
                views.reserve(levels.size());
                for (const auto& row : levels) {
                    views.push_back({row.card_key, row.level});
                }
                send_raw(protocol::S2C_ListUpgradesResp,
                         protocol::pack_list_upgrades_resp(true, "ok", wallet.gold, wallet.dust, wallet.diamond, wallet.ticket, views));
                continue;
            }

            if (packet.msg_id == protocol::C2S_UpgradeCardReq) {
                std::string token;
                std::string card_key;
                size_t o = 0;
                if (!protocol::unpack_str8(packet.payload, o, token)
                    || !protocol::unpack_str8(packet.payload, o, card_key)) {
                    send_raw(protocol::S2C_UpgradeCardResp,
                             protocol::pack_upgrade_card_resp(false, "bad packet", "", 0, 0, 0, 0, 0, 0));
                    continue;
                }
                const auto session = db_.find_session(token);
                if (!session) {
                    send_raw(protocol::S2C_UpgradeCardResp,
                             protocol::pack_upgrade_card_resp(false, "invalid or expired token", "", 0, 0, 0, 0, 0, 0));
                    continue;
                }

                uint8_t level = 0;
                int32_t spent = 0;
                std::string err;
                const bool ok = db_.try_upgrade_card(session->uid, card_key, level, spent, err);
                const auto wallet = db_.get_wallet(session->uid);
                if (ok) {
                    std::printf("[auth] upgrade uid=%u card=%s -> Lv%d spent=%d\n",
                                session->uid, card_key.c_str(), static_cast<int>(level), spent);
                }
                send_raw(protocol::S2C_UpgradeCardResp,
                         protocol::pack_upgrade_card_resp(ok, err, card_key, level, spent, wallet.gold, wallet.dust, wallet.diamond, wallet.ticket));
                continue;
            }

            if (packet.msg_id == protocol::C2S_UseItemReq) {
                std::string token;
                size_t o = 0;
                if (!protocol::unpack_str8(packet.payload, o, token) || o + 2 > packet.payload.size()) {
                    send_raw(protocol::S2C_UseItemResp,
                             protocol::pack_use_item_resp(false, "bad packet", 0, 0, 0, 0, "", {}));
                    continue;
                }
                const uint16_t item_id = static_cast<uint16_t>((packet.payload[o] << 8) | packet.payload[o + 1]);
                const auto session = db_.find_session(token);
                if (!session) {
                    send_raw(protocol::S2C_UseItemResp,
                             protocol::pack_use_item_resp(false, "invalid or expired token", 0, 0, 0, 0, "", {}));
                    continue;
                }

                int32_t dust_gain = 0;
                int32_t gold_gain = 0;
                if (item_id == 1) {
                    dust_gain = 10;
                } else if (item_id == 2) {
                    dust_gain = 25;
                } else if (item_id == 3) {
                    dust_gain = 40;
                    gold_gain = 30;
                } else {
                    send_raw(protocol::S2C_UseItemResp,
                             protocol::pack_use_item_resp(false, "unknown item", 0, 0, 0, 0, "", {}));
                    continue;
                }

                if (!db_.consume_item(session->uid, item_id, 1)) {
                    const auto wallet0 = db_.get_wallet(session->uid);
                    const auto inv0 = db_.get_inventory(session->uid);
                    std::vector<protocol::InventoryItemView> views0;
                    for (const auto& it : inv0) {
                        views0.push_back({it.item_id, it.count});
                    }
                    send_raw(protocol::S2C_UseItemResp,
                             protocol::pack_use_item_resp(false, "not enough item", wallet0.gold, wallet0.dust, wallet0.diamond, wallet0.ticket, "", views0));
                    continue;
                }

                if (dust_gain != 0) {
                    db_.add_dust(session->uid, dust_gain);
                }
                if (gold_gain != 0) {
                    db_.add_gold(session->uid, gold_gain);
                }

                db_.ensure_owned_starter(session->uid);
                const std::string granted = db_.roll_chest_card(session->uid);

                const auto wallet = db_.get_wallet(session->uid);
                const auto inv = db_.get_inventory(session->uid);
                std::vector<protocol::InventoryItemView> views;
                for (const auto& it : inv) {
                    views.push_back({it.item_id, it.count});
                }
                std::printf("[auth] use item=%u uid=%u card=%s\n", item_id, session->uid, granted.c_str());
                send_raw(protocol::S2C_UseItemResp,
                         protocol::pack_use_item_resp(true, "item used", wallet.gold, wallet.dust, wallet.diamond, wallet.ticket, granted, views));
                continue;
            }

            if (packet.msg_id == protocol::C2S_GetDeckReq) {
                std::string token;
                size_t o = 0;
                if (!protocol::unpack_str8(packet.payload, o, token)) {
                    send_raw(protocol::S2C_GetDeckResp, protocol::pack_deck_resp(false, "bad packet", {}));
                    continue;
                }
                const auto session = db_.find_session(token);
                if (!session) {
                    send_raw(protocol::S2C_GetDeckResp, protocol::pack_deck_resp(false, "invalid or expired token", {}));
                    continue;
                }
                db_.ensure_wallet(session->uid);
                db_.ensure_owned_starter(session->uid);
                auto deck = db_.get_deck(session->uid);
                if (deck.empty()) {
                    // Lv.0 → max 1 copy each. Distinct starters, size 5-12.
                    deck = {"Blood Rush", "Last Resort", "Gut Reaction", "Focus Breathing", "Lashing Out",
                            "Brace"};
                    std::string err;
                    if (!db_.save_deck(session->uid, deck, err)) {
                        std::printf("[auth] seed deck failed: %s\n", err.c_str());
                    }
                } else {
                    // Clamp illegal stacks (e.g. old 3x Blood Rush at Lv.0).
                    std::string err;
                    if (db_.clamp_deck_copies(session->uid, deck, err)) {
                        db_.save_deck(session->uid, deck, err);
                    }
                }
                const auto owned = db_.list_owned_cards(session->uid);
                send_raw(protocol::S2C_GetDeckResp, protocol::pack_deck_resp(true, "ok", deck, owned));
                continue;
            }

            if (packet.msg_id == protocol::C2S_SaveDeckReq) {
                std::string token;
                size_t o = 0;
                if (!protocol::unpack_str8(packet.payload, o, token) || o >= packet.payload.size()) {
                    send_raw(protocol::S2C_SaveDeckResp, protocol::pack_deck_resp(false, "bad packet", {}));
                    continue;
                }
                const uint8_t n = packet.payload[o++];
                std::vector<std::string> cards;
                cards.reserve(n);
                bool bad = false;
                for (uint8_t i = 0; i < n; ++i) {
                    std::string key;
                    if (!protocol::unpack_str8(packet.payload, o, key)) {
                        bad = true;
                        break;
                    }
                    cards.push_back(key);
                }
                if (bad) {
                    send_raw(protocol::S2C_SaveDeckResp, protocol::pack_deck_resp(false, "bad packet", {}));
                    continue;
                }
                const auto session = db_.find_session(token);
                if (!session) {
                    send_raw(protocol::S2C_SaveDeckResp, protocol::pack_deck_resp(false, "invalid or expired token", {}));
                    continue;
                }
                std::string err;
                const bool ok = db_.save_deck(session->uid, cards, err);
                const auto owned = db_.list_owned_cards(session->uid);
                send_raw(protocol::S2C_SaveDeckResp,
                         protocol::pack_deck_resp(ok, err, ok ? cards : std::vector<std::string>{}, owned));
                continue;
            }

            if (packet.msg_id == protocol::C2S_GachaReq) {
                std::string token;
                size_t o = 0;
                if (!protocol::unpack_str8(packet.payload, o, token) || o >= packet.payload.size()) {
                    send_raw(protocol::S2C_GachaResp,
                             protocol::pack_gacha_resp(false, "bad packet", 0, 0, 0, 0, 0, {}));
                    continue;
                }
                const uint8_t pay_type = packet.payload[o++];  // 0=ticket 1=diamond 2=gold(chest pool)
                uint8_t count = 1;
                if (o < packet.payload.size()) {
                    count = packet.payload[o++];
                }
                if (count != 1 && count != 10) {
                    send_raw(protocol::S2C_GachaResp,
                             protocol::pack_gacha_resp(false, "bad count", 0, 0, 0, 0, 0, {}));
                    continue;
                }
                const auto session = db_.find_session(token);
                if (!session) {
                    send_raw(protocol::S2C_GachaResp,
                             protocol::pack_gacha_resp(false, "invalid or expired token", 0, 0, 0, 0, 0, {}));
                    continue;
                }
                db_.ensure_wallet(session->uid);

                // Chest pool (pay_type=2): spend gold, roll gold/chest.
                if (pay_type == 2) {
                    const int gold_cost = static_cast<int>(count) * 100;
                    if (!db_.spend_gold(session->uid, gold_cost)) {
                        const auto w = db_.get_wallet(session->uid);
                        send_raw(protocol::S2C_GachaResp,
                                 protocol::pack_gacha_resp(false, "not enough gold", w.gold, w.dust, w.diamond, w.ticket,
                                                           db_.get_chest_pity(session->uid), {}));
                        continue;
                    }
                    uint8_t pity = db_.get_chest_pity(session->uid);
                    std::vector<protocol::GachaItemView> items;
                    items.reserve(count);
                    for (uint8_t i = 0; i < count; ++i) {
                        auto pull = db_.roll_chest_pool_pull(session->uid, pity);
                        protocol::GachaItemView view;
                        view.card_key = pull.card_key;
                        view.is_new = 0;
                        view.dust_gained = static_cast<uint32_t>(pull.dust_gained);
                        view.rarity = pull.rarity;
                        items.push_back(view);
                        std::printf("[auth] chest-pool uid=%u key=%s amount=%d rarity=%u pity=%u\n",
                                    session->uid, pull.card_key.c_str(), pull.dust_gained, pull.rarity, pity);
                    }
                    db_.set_chest_pity(session->uid, pity);
                    const auto wallet = db_.get_wallet(session->uid);
                    send_raw(protocol::S2C_GachaResp,
                             protocol::pack_gacha_resp(true, "gacha success", wallet.gold, wallet.dust,
                                                       wallet.diamond, wallet.ticket, pity, items));
                    continue;
                }

                const int ticket_cost = (pay_type == 0) ? static_cast<int>(count) : 0;
                const int diamond_cost = (pay_type == 1) ? static_cast<int>(count) * 160 : 0;
                bool paid = false;
                if (pay_type == 0) {
                    paid = db_.spend_ticket(session->uid, ticket_cost);
                    if (!paid) {
                        const auto w = db_.get_wallet(session->uid);
                        send_raw(protocol::S2C_GachaResp,
                                 protocol::pack_gacha_resp(false, "not enough ticket", w.gold, w.dust, w.diamond, w.ticket, db_.get_gacha_pity(session->uid), {}));
                        continue;
                    }
                } else if (pay_type == 1) {
                    paid = db_.spend_diamond(session->uid, diamond_cost);
                    if (!paid) {
                        const auto w = db_.get_wallet(session->uid);
                        send_raw(protocol::S2C_GachaResp,
                                 protocol::pack_gacha_resp(false, "not enough diamond", w.gold, w.dust, w.diamond, w.ticket, db_.get_gacha_pity(session->uid), {}));
                        continue;
                    }
                } else {
                    send_raw(protocol::S2C_GachaResp,
                             protocol::pack_gacha_resp(false, "bad pay type", 0, 0, 0, 0, 0, {}));
                    continue;
                }

                db_.ensure_owned_starter(session->uid);
                uint8_t pity = db_.get_gacha_pity(session->uid);
                std::vector<protocol::GachaItemView> items;
                items.reserve(count);
                for (uint8_t i = 0; i < count; ++i) {
                    pity += 1;
                    const bool force = pity >= 20;
                    auto pull = db_.roll_gacha_pull(session->uid, force);
                    if (pull.rarity >= 2 || force) {
                        pity = 0;
                    }
                    protocol::GachaItemView view;
                    view.card_key = pull.card_key;
                    view.is_new = pull.is_new ? 1 : 0;
                    view.dust_gained = static_cast<uint32_t>(pull.dust_gained);
                    view.rarity = pull.rarity;
                    items.push_back(view);
                    std::printf("[auth] gacha uid=%u pay=%u card=%s new=%d dust=%d rarity=%u\n",
                                session->uid, pay_type, pull.card_key.c_str(), pull.is_new ? 1 : 0,
                                pull.dust_gained, pull.rarity);
                }
                db_.set_gacha_pity(session->uid, pity);
                const auto wallet = db_.get_wallet(session->uid);
                send_raw(protocol::S2C_GachaResp,
                         protocol::pack_gacha_resp(true, "gacha success", wallet.gold, wallet.dust,
                                                   wallet.diamond, wallet.ticket, pity, items));
                continue;
            }

            send_resp(protocol::S2C_LoginResp, false, "unknown message");
        }
    }

    close(client_fd);
}

int AuthServer::run() {
    if (!db_.connect()) {
        return 1;
    }
    db_.purge_expired_sessions();
    db_.ensure_schema();

    const int listen_fd = socket(AF_INET, SOCK_STREAM, 0);
    if (listen_fd < 0) {
        std::perror("socket");
        return 1;
    }

    int yes = 1;
    setsockopt(listen_fd, SOL_SOCKET, SO_REUSEADDR, &yes, sizeof(yes));

    sockaddr_in addr {};
    addr.sin_family = AF_INET;
    addr.sin_port = htons(static_cast<uint16_t>(cfg_.listen_port));
    if (inet_pton(AF_INET, cfg_.listen_host.c_str(), &addr.sin_addr) != 1) {
        addr.sin_addr.s_addr = INADDR_ANY;
    }

    if (bind(listen_fd, reinterpret_cast<sockaddr*>(&addr), sizeof(addr)) < 0) {
        std::perror("bind");
        close(listen_fd);
        return 1;
    }
    if (listen(listen_fd, 128) < 0) {
        std::perror("listen");
        close(listen_fd);
        return 1;
    }

    std::printf("[auth] C++ AuthServer listening on %s:%d\n", cfg_.listen_host.c_str(), cfg_.listen_port);

    while (true) {
        sockaddr_in client_addr {};
        socklen_t len = sizeof(client_addr);
        const int client_fd = accept(listen_fd, reinterpret_cast<sockaddr*>(&client_addr), &len);
        if (client_fd < 0) {
            std::perror("accept");
            continue;
        }
        std::thread([this, client_fd]() { handle_client(client_fd); }).detach();
    }
}
