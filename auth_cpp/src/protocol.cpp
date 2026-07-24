#include "protocol.h"

#include <cstring>

namespace protocol {
namespace {

void write_u16_be(std::vector<uint8_t>& out, uint16_t v) {
    out.push_back(static_cast<uint8_t>((v >> 8) & 0xFF));
    out.push_back(static_cast<uint8_t>(v & 0xFF));
}

void write_u32_be(std::vector<uint8_t>& out, uint32_t v) {
    out.push_back(static_cast<uint8_t>((v >> 24) & 0xFF));
    out.push_back(static_cast<uint8_t>((v >> 16) & 0xFF));
    out.push_back(static_cast<uint8_t>((v >> 8) & 0xFF));
    out.push_back(static_cast<uint8_t>(v & 0xFF));
}

uint16_t read_u16_be(const uint8_t* p) {
    return static_cast<uint16_t>((p[0] << 8) | p[1]);
}

}  // namespace

bool unpack_str8(const std::vector<uint8_t>& payload, size_t& offset, std::string& out) {
    if (offset >= payload.size()) {
        return false;
    }
    const uint8_t n = payload[offset++];
    if (offset + n > payload.size()) {
        return false;
    }
    out.assign(reinterpret_cast<const char*>(payload.data() + offset), n);
    offset += n;
    return true;
}

void append_str8(std::vector<uint8_t>& out, const std::string& s) {
    const uint8_t n = static_cast<uint8_t>(s.size() > 255 ? 255 : s.size());
    out.push_back(n);
    out.insert(out.end(), s.begin(), s.begin() + n);
}

bool unpack_login_req(const std::vector<uint8_t>& payload, std::string& username, std::string& password) {
    size_t o = 0;
    return unpack_str8(payload, o, username) && unpack_str8(payload, o, password);
}

bool unpack_register_req(const std::vector<uint8_t>& payload,
                         std::string& username,
                         std::string& password,
                         std::string& captcha_id,
                         std::string& captcha_answer) {
    size_t o = 0;
    return unpack_str8(payload, o, username)
        && unpack_str8(payload, o, password)
        && unpack_str8(payload, o, captcha_id)
        && unpack_str8(payload, o, captcha_answer);
}

std::vector<uint8_t> pack_login_resp(bool ok, const std::string& message, uint32_t uid, const std::string& token) {
    std::vector<uint8_t> payload;
    payload.push_back(ok ? 1 : 0);
    write_u16_be(payload, static_cast<uint16_t>(message.size()));
    payload.insert(payload.end(), message.begin(), message.end());
    if (ok) {
        write_u32_be(payload, uid);
        append_str8(payload, token);
    }
    return payload;
}

std::vector<uint8_t> pack_captcha_resp(const std::string& captcha_id, const std::string& question) {
    std::vector<uint8_t> payload;
    append_str8(payload, captcha_id);
    append_str8(payload, question);
    return payload;
}

std::vector<uint8_t> pack_inventory_resp(bool ok,
                                         const std::string& message,
                                         int32_t gold,
                                         int32_t dust,
                                         int32_t diamond,
                                         int32_t ticket,
                                         const std::vector<InventoryItemView>& items) {
    std::vector<uint8_t> payload;
    payload.push_back(ok ? 1 : 0);
    write_u16_be(payload, static_cast<uint16_t>(message.size()));
    payload.insert(payload.end(), message.begin(), message.end());
    if (!ok) {
        return payload;
    }
    write_u32_be(payload, static_cast<uint32_t>(gold));
    write_u32_be(payload, static_cast<uint32_t>(dust));
    write_u32_be(payload, static_cast<uint32_t>(diamond));
    write_u32_be(payload, static_cast<uint32_t>(ticket));
    const uint8_t n = static_cast<uint8_t>(items.size() > 255 ? 255 : items.size());
    payload.push_back(n);
    for (uint8_t i = 0; i < n; ++i) {
        write_u16_be(payload, items[i].item_id);
        write_u32_be(payload, items[i].count);
    }
    return payload;
}

std::vector<uint8_t> pack_claim_reward_resp(bool ok,
                                            const std::string& message,
                                            int32_t gold_delta,
                                            int32_t dust_delta,
                                            int32_t diamond_delta,
                                            int32_t ticket_delta,
                                            int32_t gold,
                                            int32_t dust,
                                            int32_t diamond,
                                            int32_t ticket) {
    std::vector<uint8_t> payload;
    payload.push_back(ok ? 1 : 0);
    write_u16_be(payload, static_cast<uint16_t>(message.size()));
    payload.insert(payload.end(), message.begin(), message.end());
    if (!ok) {
        return payload;
    }
    write_u32_be(payload, static_cast<uint32_t>(gold_delta));
    write_u32_be(payload, static_cast<uint32_t>(dust_delta));
    write_u32_be(payload, static_cast<uint32_t>(diamond_delta));
    write_u32_be(payload, static_cast<uint32_t>(ticket_delta));
    write_u32_be(payload, static_cast<uint32_t>(gold));
    write_u32_be(payload, static_cast<uint32_t>(dust));
    write_u32_be(payload, static_cast<uint32_t>(diamond));
    write_u32_be(payload, static_cast<uint32_t>(ticket));
    return payload;
}

std::vector<uint8_t> pack_list_upgrades_resp(bool ok,
                                             const std::string& message,
                                             int32_t gold,
                                             int32_t dust,
                                             int32_t diamond,
                                             int32_t ticket,
                                             const std::vector<CardUpgradeView>& upgrades) {
    std::vector<uint8_t> payload;
    payload.push_back(ok ? 1 : 0);
    write_u16_be(payload, static_cast<uint16_t>(message.size()));
    payload.insert(payload.end(), message.begin(), message.end());
    if (!ok) {
        return payload;
    }
    write_u32_be(payload, static_cast<uint32_t>(gold));
    write_u32_be(payload, static_cast<uint32_t>(dust));
    write_u32_be(payload, static_cast<uint32_t>(diamond));
    write_u32_be(payload, static_cast<uint32_t>(ticket));
    const uint8_t n = static_cast<uint8_t>(upgrades.size() > 255 ? 255 : upgrades.size());
    payload.push_back(n);
    for (uint8_t i = 0; i < n; ++i) {
        append_str8(payload, upgrades[i].card_key);
        payload.push_back(upgrades[i].level);
    }
    return payload;
}

std::vector<uint8_t> pack_upgrade_card_resp(bool ok,
                                            const std::string& message,
                                            const std::string& card_key,
                                            uint8_t level,
                                            int32_t dust_spent,
                                            int32_t gold,
                                            int32_t dust,
                                            int32_t diamond,
                                            int32_t ticket) {
    std::vector<uint8_t> payload;
    payload.push_back(ok ? 1 : 0);
    write_u16_be(payload, static_cast<uint16_t>(message.size()));
    payload.insert(payload.end(), message.begin(), message.end());
    if (!ok) {
        return payload;
    }
    append_str8(payload, card_key);
    payload.push_back(level);
    write_u32_be(payload, static_cast<uint32_t>(dust_spent));
    write_u32_be(payload, static_cast<uint32_t>(gold));
    write_u32_be(payload, static_cast<uint32_t>(dust));
    write_u32_be(payload, static_cast<uint32_t>(diamond));
    write_u32_be(payload, static_cast<uint32_t>(ticket));
    return payload;
}

std::vector<uint8_t> pack_use_item_resp(bool ok,
                                        const std::string& message,
                                        int32_t gold,
                                        int32_t dust,
                                        int32_t diamond,
                                        int32_t ticket,
                                        const std::string& granted_card,
                                        const std::vector<InventoryItemView>& items) {
    std::vector<uint8_t> payload;
    payload.push_back(ok ? 1 : 0);
    write_u16_be(payload, static_cast<uint16_t>(message.size()));
    payload.insert(payload.end(), message.begin(), message.end());
    if (!ok) {
        return payload;
    }
    write_u32_be(payload, static_cast<uint32_t>(gold));
    write_u32_be(payload, static_cast<uint32_t>(dust));
    write_u32_be(payload, static_cast<uint32_t>(diamond));
    write_u32_be(payload, static_cast<uint32_t>(ticket));
    append_str8(payload, granted_card);
    const uint8_t n = static_cast<uint8_t>(items.size() > 255 ? 255 : items.size());
    payload.push_back(n);
    for (uint8_t i = 0; i < n; ++i) {
        write_u16_be(payload, items[i].item_id);
        write_u32_be(payload, items[i].count);
    }
    return payload;
}

std::vector<uint8_t> pack_gacha_resp(bool ok,
                                     const std::string& message,
                                     int32_t gold,
                                     int32_t dust,
                                     int32_t diamond,
                                     int32_t ticket,
                                     uint8_t pity,
                                     const std::vector<GachaItemView>& items) {
    std::vector<uint8_t> payload;
    payload.push_back(ok ? 1 : 0);
    write_u16_be(payload, static_cast<uint16_t>(message.size()));
    payload.insert(payload.end(), message.begin(), message.end());
    if (!ok) {
        return payload;
    }
    write_u32_be(payload, static_cast<uint32_t>(gold));
    write_u32_be(payload, static_cast<uint32_t>(dust));
    write_u32_be(payload, static_cast<uint32_t>(diamond));
    write_u32_be(payload, static_cast<uint32_t>(ticket));
    payload.push_back(pity);
    const uint8_t n = static_cast<uint8_t>(items.size() > 255 ? 255 : items.size());
    payload.push_back(n);
    for (uint8_t i = 0; i < n; ++i) {
        append_str8(payload, items[i].card_key);
        payload.push_back(items[i].is_new);
        write_u32_be(payload, items[i].dust_gained);
        payload.push_back(items[i].rarity);
    }
    return payload;
}

std::vector<uint8_t> pack_deck_resp(bool ok,
                                    const std::string& message,
                                    const std::vector<std::string>& deck,
                                    const std::vector<std::string>& owned) {
    std::vector<uint8_t> payload;
    payload.push_back(ok ? 1 : 0);
    write_u16_be(payload, static_cast<uint16_t>(message.size()));
    payload.insert(payload.end(), message.begin(), message.end());
    if (!ok) {
        return payload;
    }
    auto append_list = [&](const std::vector<std::string>& list) {
        const uint8_t n = static_cast<uint8_t>(list.size() > 255 ? 255 : list.size());
        payload.push_back(n);
        for (uint8_t i = 0; i < n; ++i) {
            append_str8(payload, list[i]);
        }
    };
    append_list(deck);
    append_list(owned);
    return payload;
}

std::vector<uint8_t> pack_frame(uint16_t msg_id, const std::vector<uint8_t>& payload) {
    std::vector<uint8_t> body;
    write_u16_be(body, msg_id);
    body.insert(body.end(), payload.begin(), payload.end());

    std::vector<uint8_t> frame;
    write_u16_be(frame, static_cast<uint16_t>(body.size()));
    frame.insert(frame.end(), body.begin(), body.end());
    return frame;
}

void append_bytes(std::vector<uint8_t>& buffer, const uint8_t* data, size_t n) {
    buffer.insert(buffer.end(), data, data + n);
}

bool try_read_packet(std::vector<uint8_t>& buffer, Packet& out) {
    if (buffer.size() < 2) {
        return false;
    }
    const uint16_t body_len = read_u16_be(buffer.data());
    if (buffer.size() < static_cast<size_t>(2 + body_len)) {
        return false;
    }
    if (body_len < 2) {
        buffer.erase(buffer.begin(), buffer.begin() + 2 + body_len);
        return false;
    }

    out.msg_id = read_u16_be(buffer.data() + 2);
    out.payload.assign(buffer.begin() + 4, buffer.begin() + 2 + body_len);
    buffer.erase(buffer.begin(), buffer.begin() + 2 + body_len);
    return true;
}

}  // namespace protocol
