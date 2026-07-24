#include "db.h"
#include "card_catalog.h"

#include <cstdio>
#include <cstdlib>
#include <map>
#include <unordered_map>

Database::Database(const AuthConfig& cfg) : cfg_(cfg) {}

Database::~Database() {
    if (conn_) {
        mysql_close(conn_);
        conn_ = nullptr;
    }
}

bool Database::connect() {
    if (conn_) {
        mysql_close(conn_);
        conn_ = nullptr;
    }

    conn_ = mysql_init(nullptr);
    if (!conn_) {
        return false;
    }

    // Auto-reconnect for long-lived auth process (MySQL may drop idle connections).
    bool reconnect = true;
    mysql_options(conn_, MYSQL_OPT_RECONNECT, &reconnect);

    if (!mysql_real_connect(conn_,
                            cfg_.db_host.c_str(),
                            cfg_.db_user.c_str(),
                            cfg_.db_password.c_str(),
                            cfg_.db_name.c_str(),
                            cfg_.db_port,
                            nullptr,
                            0)) {
        std::fprintf(stderr, "[db] connect failed: %s\n", mysql_error(conn_));
        return false;
    }

    mysql_set_character_set(conn_, "utf8mb4");
    // Keep connection warm longer than default wait_timeout when possible (server may still override).
    mysql_query(conn_, "SET SESSION wait_timeout=28800");
    mysql_query(conn_, "SET SESSION interactive_timeout=28800");
    std::printf("[db] connected to %s\n", cfg_.db_name.c_str());
    return true;
}

bool Database::ensure_alive() {
    if (!conn_) {
        return connect();
    }
    if (mysql_ping(conn_) == 0) {
        return true;
    }
    std::fprintf(stderr, "[db] ping failed (%s), reconnecting...\n", mysql_error(conn_));
    return connect();
}

bool Database::query(const std::string& sql) {
    if (!ensure_alive()) {
        std::fprintf(stderr, "[db] ensure_alive failed before query\n");
        return false;
    }
    if (mysql_query(conn_, sql.c_str()) == 0) {
        return true;
    }

    const unsigned err = mysql_errno(conn_);
    // CR_SERVER_GONE_ERROR=2006, CR_SERVER_LOST=2013 — retry once after reconnect.
    if (err == 2006 || err == 2013) {
        std::fprintf(stderr, "[db] lost connection (%u), retrying once: %s\n", err, mysql_error(conn_));
        if (!connect()) {
            return false;
        }
        return mysql_query(conn_, sql.c_str()) == 0;
    }

    std::fprintf(stderr, "[db] query failed: %s\n", mysql_error(conn_));
    return false;
}

void Database::ensure_schema() {
    query("CREATE TABLE IF NOT EXISTS player_wallet ("
          "  uid INT UNSIGNED NOT NULL PRIMARY KEY,"
          "  gold INT NOT NULL DEFAULT 0,"
          "  dust INT NOT NULL DEFAULT 0,"
          "  diamond INT NOT NULL DEFAULT 0,"
          "  ticket INT NOT NULL DEFAULT 0,"
          "  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP"
          ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
    // Legacy DBs may miss columns.
    mysql_query(conn_, "ALTER TABLE player_wallet ADD COLUMN diamond INT NOT NULL DEFAULT 0");
    mysql_query(conn_, "ALTER TABLE player_wallet ADD COLUMN ticket INT NOT NULL DEFAULT 0");
    mysql_query(conn_, "ALTER TABLE player_wallet ADD COLUMN gacha_pity INT NOT NULL DEFAULT 0");
    mysql_query(conn_, "ALTER TABLE player_wallet ADD COLUMN chest_pity INT NOT NULL DEFAULT 0");

    query("CREATE TABLE IF NOT EXISTS milestones ("
          "  uid INT UNSIGNED NOT NULL,"
          "  mile_key VARCHAR(64) NOT NULL,"
          "  claimed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,"
          "  PRIMARY KEY (uid, mile_key)"
          ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    query("CREATE TABLE IF NOT EXISTS player_inventory ("
          "  uid INT UNSIGNED NOT NULL,"
          "  item_id SMALLINT UNSIGNED NOT NULL,"
          "  count INT UNSIGNED NOT NULL DEFAULT 0,"
          "  PRIMARY KEY (uid, item_id)"
          ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    query("CREATE TABLE IF NOT EXISTS stage_claims ("
          "  uid INT UNSIGNED NOT NULL,"
          "  stage_key VARCHAR(64) NOT NULL,"
          "  run_id VARCHAR(64) NOT NULL,"
          "  claimed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,"
          "  PRIMARY KEY (uid, stage_key, run_id)"
          ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    query("CREATE TABLE IF NOT EXISTS card_levels ("
          "  uid INT UNSIGNED NOT NULL,"
          "  card_key VARCHAR(64) NOT NULL,"
          "  level TINYINT UNSIGNED NOT NULL DEFAULT 0,"
          "  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,"
          "  PRIMARY KEY (uid, card_key)"
          ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    query("CREATE TABLE IF NOT EXISTS player_owned_cards ("
          "  uid INT UNSIGNED NOT NULL,"
          "  card_key VARCHAR(64) NOT NULL,"
          "  unlocked_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,"
          "  PRIMARY KEY (uid, card_key)"
          ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    query("CREATE TABLE IF NOT EXISTS player_deck ("
          "  uid INT UNSIGNED NOT NULL,"
          "  slot TINYINT UNSIGNED NOT NULL,"
          "  card_key VARCHAR(64) NOT NULL,"
          "  PRIMARY KEY (uid, slot)"
          ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
}

std::string Database::escape(const std::string& s) {
    std::string out(s.size() * 2 + 1, '\0');
    const unsigned long n = mysql_real_escape_string(conn_, out.data(), s.c_str(), s.size());
    out.resize(n);
    return out;
}

std::optional<UserRow> Database::find_user(const std::string& username) {
    if (!ensure_alive()) {
        return std::nullopt;
    }
    const std::string sql =
        "SELECT id, username, password FROM users WHERE username='" + escape(username) + "' LIMIT 1";
    if (!query(sql)) {
        return std::nullopt;
    }

    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) {
        return std::nullopt;
    }

    MYSQL_ROW row = mysql_fetch_row(res);
    std::optional<UserRow> out;
    if (row && row[0] && row[1] && row[2]) {
        UserRow u;
        u.id = static_cast<uint32_t>(std::strtoul(row[0], nullptr, 10));
        u.username = row[1];
        u.password = row[2];
        out = u;
    }
    mysql_free_result(res);
    return out;
}

std::optional<uint32_t> Database::create_user(const std::string& username, const std::string& password_hash) {
    if (!ensure_alive()) {
        return std::nullopt;
    }
    const std::string sql =
        "INSERT INTO users (username, password) VALUES ('" + escape(username) + "','" + escape(password_hash) + "')";
    if (!query(sql)) {
        std::fprintf(stderr, "[db] insert failed: %s\n", mysql_error(conn_));
        return std::nullopt;
    }
    return static_cast<uint32_t>(mysql_insert_id(conn_));
}

bool Database::save_session(const std::string& token, uint32_t uid, const std::string& username, int ttl_seconds) {
    if (!ensure_alive()) {
        return false;
    }
    char sql[512];
    std::snprintf(sql,
                  sizeof(sql),
                  "REPLACE INTO auth_sessions (token, uid, username, expires_at) "
                  "VALUES ('%s', %u, '%s', DATE_ADD(NOW(), INTERVAL %d SECOND))",
                  escape(token).c_str(),
                  uid,
                  escape(username).c_str(),
                  ttl_seconds);
    if (!query(sql)) {
        std::fprintf(stderr, "[db] save_session failed: %s\n", mysql_error(conn_));
        return false;
    }
    return true;
}

std::optional<SessionRow> Database::find_session(const std::string& token) {
    if (!ensure_alive()) {
        return std::nullopt;
    }
    const std::string sql =
        "SELECT uid, username FROM auth_sessions WHERE token='" + escape(token) + "' AND expires_at > NOW() LIMIT 1";
    if (!query(sql)) {
        return std::nullopt;
    }
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) {
        return std::nullopt;
    }
    MYSQL_ROW row = mysql_fetch_row(res);
    std::optional<SessionRow> out;
    if (row && row[0] && row[1]) {
        SessionRow s;
        s.uid = static_cast<uint32_t>(std::strtoul(row[0], nullptr, 10));
        s.username = row[1];
        out = s;
    }
    mysql_free_result(res);
    return out;
}

void Database::purge_expired_sessions() {
    query("DELETE FROM auth_sessions WHERE expires_at < NOW()");
}

void Database::ensure_wallet(uint32_t uid) {
    char sql[320];
    std::snprintf(sql,
                  sizeof(sql),
                  "INSERT IGNORE INTO player_wallet (uid, gold, dust, diamond, ticket) VALUES (%u, 100, 40, 50, 1)",
                  uid);
    query(sql);
    if (mysql_affected_rows(conn_) == 1) {
        add_item(uid, 1, 1);
    }
    ensure_owned_starter(uid);
}

WalletRow Database::get_wallet(uint32_t uid) {
    WalletRow w;
    if (!ensure_alive()) {
        return w;
    }
    char sql[128];
    std::snprintf(sql, sizeof(sql), "SELECT gold, dust, diamond, ticket FROM player_wallet WHERE uid=%u LIMIT 1", uid);
    if (!query(sql)) {
        return w;
    }
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) {
        return w;
    }
    if (MYSQL_ROW row = mysql_fetch_row(res)) {
        if (row[0]) {
            w.gold = static_cast<int32_t>(std::strtol(row[0], nullptr, 10));
        }
        if (row[1]) {
            w.dust = static_cast<int32_t>(std::strtol(row[1], nullptr, 10));
        }
        if (row[2]) {
            w.diamond = static_cast<int32_t>(std::strtol(row[2], nullptr, 10));
        }
        if (row[3]) {
            w.ticket = static_cast<int32_t>(std::strtol(row[3], nullptr, 10));
        }
    }
    mysql_free_result(res);
    return w;
}

std::vector<InventoryItem> Database::get_inventory(uint32_t uid) {
    std::vector<InventoryItem> items;
    if (!ensure_alive()) {
        return items;
    }
    char sql[160];
    std::snprintf(sql,
                  sizeof(sql),
                  "SELECT item_id, count FROM player_inventory WHERE uid=%u AND count > 0",
                  uid);
    if (!query(sql)) {
        return items;
    }
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) {
        return items;
    }
    while (MYSQL_ROW row = mysql_fetch_row(res)) {
        if (!row[0] || !row[1]) {
            continue;
        }
        InventoryItem it;
        it.item_id = static_cast<uint16_t>(std::strtoul(row[0], nullptr, 10));
        it.count = static_cast<uint32_t>(std::strtoul(row[1], nullptr, 10));
        items.push_back(it);
    }
    mysql_free_result(res);
    return items;
}

bool Database::add_item(uint32_t uid, uint16_t item_id, uint32_t count) {
    char sql[256];
    std::snprintf(sql,
                  sizeof(sql),
                  "INSERT INTO player_inventory (uid, item_id, count) VALUES (%u, %u, %u) "
                  "ON DUPLICATE KEY UPDATE count = count + %u",
                  uid,
                  item_id,
                  count,
                  count);
    return query(sql);
}

bool Database::consume_item(uint32_t uid, uint16_t item_id, uint32_t count) {
    if (count == 0) {
        return true;
    }
    char sql[256];
    std::snprintf(sql,
                  sizeof(sql),
                  "UPDATE player_inventory SET count = count - %u "
                  "WHERE uid=%u AND item_id=%u AND count >= %u",
                  count, uid, item_id, count);
    if (!query(sql) || mysql_affected_rows(conn_) == 0) {
        return false;
    }
    // Cleanup zero rows.
    std::snprintf(sql, sizeof(sql),
                  "DELETE FROM player_inventory WHERE uid=%u AND item_id=%u AND count=0",
                  uid, item_id);
    query(sql);
    return true;
}

bool Database::add_gold(uint32_t uid, int32_t delta) {
    ensure_wallet(uid);
    char sql[160];
    std::snprintf(sql, sizeof(sql), "UPDATE player_wallet SET gold = gold + (%d) WHERE uid=%u", delta, uid);
    return query(sql);
}

bool Database::add_dust(uint32_t uid, int32_t delta) {
    ensure_wallet(uid);
    char sql[160];
    std::snprintf(sql, sizeof(sql), "UPDATE player_wallet SET dust = dust + (%d) WHERE uid=%u", delta, uid);
    return query(sql);
}

bool Database::add_diamond(uint32_t uid, int32_t delta) {
    ensure_wallet(uid);
    char sql[160];
    std::snprintf(sql, sizeof(sql), "UPDATE player_wallet SET diamond = diamond + (%d) WHERE uid=%u", delta, uid);
    return query(sql);
}

bool Database::add_ticket(uint32_t uid, int32_t delta) {
    ensure_wallet(uid);
    char sql[160];
    std::snprintf(sql, sizeof(sql), "UPDATE player_wallet SET ticket = ticket + (%d) WHERE uid=%u", delta, uid);
    return query(sql);
}

bool Database::spend_diamond(uint32_t uid, int32_t cost) {
    if (cost <= 0) {
        return true;
    }
    char sql[200];
    std::snprintf(sql, sizeof(sql),
                  "UPDATE player_wallet SET diamond = diamond - (%d) WHERE uid=%u AND diamond >= %d",
                  cost, uid, cost);
    return query(sql) && mysql_affected_rows(conn_) > 0;
}

bool Database::spend_ticket(uint32_t uid, int32_t cost) {
    if (cost <= 0) {
        return true;
    }
    char sql[200];
    std::snprintf(sql, sizeof(sql),
                  "UPDATE player_wallet SET ticket = ticket - (%d) WHERE uid=%u AND ticket >= %d",
                  cost, uid, cost);
    return query(sql) && mysql_affected_rows(conn_) > 0;
}

bool Database::spend_gold(uint32_t uid, int32_t cost) {
    if (cost <= 0) {
        return true;
    }
    char sql[200];
    std::snprintf(sql, sizeof(sql),
                  "UPDATE player_wallet SET gold = gold - (%d) WHERE uid=%u AND gold >= %d",
                  cost, uid, cost);
    return query(sql) && mysql_affected_rows(conn_) > 0;
}

bool Database::try_claim_milestone(uint32_t uid, const std::string& mile_key) {
    if (!ensure_alive()) {
        return false;
    }
    char sql[256];
    std::snprintf(sql, sizeof(sql),
                  "INSERT IGNORE INTO milestones (uid, mile_key) VALUES (%u, '%s')",
                  uid, escape(mile_key).c_str());
    if (!query(sql)) {
        return false;
    }
    return mysql_affected_rows(conn_) > 0;
}

bool Database::try_claim_stage(uint32_t uid, const std::string& stage_key, const std::string& run_id,
                               int32_t gold_delta, int32_t dust_delta) {
    if (!ensure_alive()) {
        return false;
    }
    char sql[512];
    std::snprintf(sql,
                  sizeof(sql),
                  "INSERT INTO stage_claims (uid, stage_key, run_id) VALUES (%u, '%s', '%s')",
                  uid,
                  escape(stage_key).c_str(),
                  escape(run_id).c_str());
    if (!query(sql)) {
        // Duplicate = already claimed, or transient error.
        return false;
    }
    if (gold_delta != 0) {
        add_gold(uid, gold_delta);
    }
    if (dust_delta != 0) {
        add_dust(uid, dust_delta);
    }
    return true;
}

uint8_t Database::get_card_level(uint32_t uid, const std::string& card_key) {
    if (!ensure_alive()) {
        return 0;
    }
    const std::string sql =
        "SELECT level FROM card_levels WHERE uid=" + std::to_string(uid) +
        " AND card_key='" + escape(card_key) + "' LIMIT 1";
    if (!query(sql)) {
        return 0;
    }
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) {
        return 0;
    }
    uint8_t level = 0;
    if (MYSQL_ROW row = mysql_fetch_row(res)) {
        if (row[0]) {
            level = static_cast<uint8_t>(std::strtoul(row[0], nullptr, 10));
        }
    }
    mysql_free_result(res);
    return level;
}

std::vector<CardLevelRow> Database::list_card_levels(uint32_t uid) {
    std::vector<CardLevelRow> rows;
    if (!ensure_alive()) {
        return rows;
    }
    const std::string sql =
        "SELECT card_key, level FROM card_levels WHERE uid=" + std::to_string(uid) + " AND level > 0";
    if (!query(sql)) {
        return rows;
    }
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) {
        return rows;
    }
    while (MYSQL_ROW row = mysql_fetch_row(res)) {
        if (!row[0] || !row[1]) {
            continue;
        }
        CardLevelRow r;
        r.card_key = row[0];
        r.level = static_cast<uint8_t>(std::strtoul(row[1], nullptr, 10));
        rows.push_back(r);
    }
    mysql_free_result(res);
    return rows;
}

namespace {

uint8_t max_copies_for_level(uint8_t level) {
    // Lv0 → 1, Lv1 → 2, Lv2+ → 3 (cap)
    const int v = 1 + static_cast<int>(level);
    return static_cast<uint8_t>(v > 3 ? 3 : v);
}

}  // namespace

bool Database::try_upgrade_card(uint32_t uid, const std::string& card_key, uint8_t& out_level,
                                int32_t& out_dust_spent, std::string& err) {
    out_level = 0;
    out_dust_spent = 0;
    if (!is_known_card(card_key)) {
        err = "unknown card";
        return false;
    }

    constexpr uint8_t kMaxLevel = 3;
    ensure_wallet(uid);
    const uint8_t cur = get_card_level(uid, card_key);
    if (cur >= kMaxLevel) {
        err = "already max level";
        out_level = cur;
        return false;
    }

    const int32_t cost = 20 * static_cast<int32_t>(cur + 1);  // 20 / 40 / 60
    const WalletRow wallet = get_wallet(uid);
    if (wallet.dust < cost) {
        err = "not enough dust";
        out_level = cur;
        return false;
    }

    char sql[256];
    std::snprintf(sql, sizeof(sql),
                  "UPDATE player_wallet SET dust = dust - (%d) WHERE uid=%u AND dust >= %d",
                  cost, uid, cost);
    if (!query(sql) || mysql_affected_rows(conn_) == 0) {
        err = "not enough dust";
        out_level = cur;
        return false;
    }

    const uint8_t next = static_cast<uint8_t>(cur + 1);
    std::snprintf(sql, sizeof(sql),
                  "INSERT INTO card_levels (uid, card_key, level) VALUES (%u, '%s', %u) "
                  "ON DUPLICATE KEY UPDATE level=%u",
                  uid, escape(card_key).c_str(), next, next);
    if (!query(sql)) {
        err = "upgrade failed";
        add_dust(uid, cost);  // refund
        return false;
    }

    out_level = next;
    out_dust_spent = cost;
    err = "upgrade success";
    return true;
}

void Database::ensure_owned_starter(uint32_t uid) {
    static const char* kStarter[] = {
        "Blood Rush", "Last Resort", "Gut Reaction", "Focus Breathing", "Lashing Out",
    };
    for (const char* c : kStarter) {
        add_owned_card(uid, c);
    }
}

bool Database::add_owned_card(uint32_t uid, const std::string& card_key) {
    if (!is_known_card(card_key)) {
        return false;
    }
    char sql[256];
    std::snprintf(sql, sizeof(sql),
                  "INSERT IGNORE INTO player_owned_cards (uid, card_key) VALUES (%u, '%s')",
                  uid, escape(card_key).c_str());
    return query(sql);
}

std::vector<std::string> Database::list_owned_cards(uint32_t uid) {
    std::vector<std::string> out;
    if (!ensure_alive()) {
        return out;
    }
    const std::string sql =
        "SELECT card_key FROM player_owned_cards WHERE uid=" + std::to_string(uid) + " ORDER BY card_key";
    if (!query(sql)) {
        return out;
    }
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) {
        return out;
    }
    while (MYSQL_ROW row = mysql_fetch_row(res)) {
        if (row[0]) {
            out.emplace_back(row[0]);
        }
    }
    mysql_free_result(res);
    return out;
}

std::string Database::roll_chest_card(uint32_t uid) {
    auto owned = list_owned_cards(uid);
    std::vector<std::string> missing;
    for (const auto& c : all_card_keys()) {
        bool have = false;
        for (const auto& o : owned) {
            if (o == c) {
                have = true;
                break;
            }
        }
        if (!have) {
            missing.push_back(c);
        }
    }
    const std::vector<std::string>& pool = missing.empty() ? all_card_keys() : missing;
    if (pool.empty()) {
        return {};
    }
    const size_t idx = static_cast<size_t>(std::rand()) % pool.size();
    const std::string pick = pool[idx];
    add_owned_card(uid, pick);
    return pick;
}


bool Database::owns_card(uint32_t uid, const std::string& card_key) {
    if (!ensure_alive()) return false;
    const std::string sql = "SELECT 1 FROM player_owned_cards WHERE uid=" + std::to_string(uid) +
                            " AND card_key='" + escape(card_key) + "' LIMIT 1";
    if (!query(sql)) return false;
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) return false;
    const bool ok = mysql_fetch_row(res) != nullptr;
    mysql_free_result(res);
    return ok;
}

uint8_t Database::get_gacha_pity(uint32_t uid) {
    if (!ensure_alive()) return 0;
    char sql[256];
    std::snprintf(sql, sizeof(sql), "SELECT gacha_pity FROM player_wallet WHERE uid=%u LIMIT 1", uid);
    if (!query(sql)) return 0;
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) return 0;
    MYSQL_ROW row = mysql_fetch_row(res);
    uint8_t pity = 0;
    if (row && row[0]) pity = static_cast<uint8_t>(std::atoi(row[0]));
    mysql_free_result(res);
    return pity;
}

void Database::set_gacha_pity(uint32_t uid, uint8_t pity) {
    if (!ensure_alive()) return;
    char sql[256];
    std::snprintf(sql, sizeof(sql), "UPDATE player_wallet SET gacha_pity=%u WHERE uid=%u", pity, uid);
    query(sql);
}

uint8_t Database::get_chest_pity(uint32_t uid) {
    if (!ensure_alive()) return 0;
    char sql[256];
    std::snprintf(sql, sizeof(sql), "SELECT chest_pity FROM player_wallet WHERE uid=%u LIMIT 1", uid);
    if (!query(sql)) return 0;
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) return 0;
    MYSQL_ROW row = mysql_fetch_row(res);
    uint8_t pity = 0;
    if (row && row[0]) pity = static_cast<uint8_t>(std::atoi(row[0]));
    mysql_free_result(res);
    return pity;
}

void Database::set_chest_pity(uint32_t uid, uint8_t pity) {
    if (!ensure_alive()) return;
    char sql[256];
    std::snprintf(sql, sizeof(sql), "UPDATE player_wallet SET chest_pity=%u WHERE uid=%u", pity, uid);
    query(sql);
}

Database::GachaPullResult Database::roll_chest_pool_pull(uint32_t uid, uint8_t& pity_inout) {
    // 40% gold (50~150), 60% chest. Soft pity 8 → iron+, hard pity 20 → gold chest.
    GachaPullResult out;
    ensure_wallet(uid);
    pity_inout = static_cast<uint8_t>(pity_inout + 1);
    const bool force_gold_chest = pity_inout >= 20;
    const bool force_iron_plus = pity_inout >= 8;
    const int roll = std::rand() % 100;

    if (!force_gold_chest && !force_iron_plus && roll < 40) {
        const int amount = 50 + (std::rand() % 101);  // 50~150
        add_gold(uid, amount);
        out.card_key = "__GOLD__";
        out.is_new = false;
        out.dust_gained = amount;
        out.rarity = 0;
        return out;
    }

    uint16_t item_id = 1;
    if (force_gold_chest) {
        item_id = 3;
    } else if (force_iron_plus) {
        item_id = (std::rand() % 100 < 20) ? 3 : 2;
    } else {
        const int cr = std::rand() % 100;
        if (cr < 70) item_id = 1;
        else if (cr < 95) item_id = 2;
        else item_id = 3;
    }

    add_item(uid, item_id, 1);
    if (item_id == 1) out.card_key = "__CHEST_1__";
    else if (item_id == 2) out.card_key = "__CHEST_2__";
    else out.card_key = "__CHEST_3__";
    out.is_new = false;
    out.dust_gained = 0;
    out.rarity = static_cast<uint8_t>(item_id);
    if (item_id >= 2) {
        pity_inout = 0;  // soft/hard pity reset on iron+ / gold
    }
    return out;
}

Database::GachaPullResult Database::roll_gacha_pull(uint32_t uid, bool force_high_rarity) {
    GachaPullResult out;
    ensure_owned_starter(uid);
    auto owned = list_owned_cards(uid);
    std::vector<std::string> pool;
    for (const auto& c : all_cards()) {
        if (force_high_rarity && c.second < 2) continue; // Rare+
        pool.push_back(c.first);
    }
    if (pool.empty()) {
        for (const auto& c : all_cards()) pool.push_back(c.first);
    }
    // Prefer unowned when not forcing
    if (!force_high_rarity) {
        std::vector<std::string> missing;
        for (const auto& key : pool) {
            bool have = false;
            for (const auto& o : owned) if (o == key) { have = true; break; }
            if (!have) missing.push_back(key);
        }
        if (!missing.empty()) pool = missing;
    }
    if (pool.empty()) return out;
    const size_t idx = static_cast<size_t>(std::rand()) % pool.size();
    out.card_key = pool[idx];
    out.rarity = card_rarity(out.card_key);
    const bool already = owns_card(uid, out.card_key);
    if (already) {
        out.is_new = false;
        out.dust_gained = dupe_dust_for_rarity(out.rarity);
        add_dust(uid, out.dust_gained);
    } else {
        out.is_new = true;
        out.dust_gained = 0;
        add_owned_card(uid, out.card_key);
    }
    return out;
}



bool Database::clamp_deck_copies(uint32_t uid, std::vector<std::string>& cards, std::string& err) {
    (void)err;
    bool changed = false;
    std::vector<std::string> out;
    out.reserve(cards.size());
    std::unordered_map<std::string, int> counts;
    for (const auto& c : cards) {
        const uint8_t max_copies = max_copies_for_level(get_card_level(uid, c));
        const int next = counts[c] + 1;
        if (next > static_cast<int>(max_copies)) {
            changed = true;
            continue;
        }
        counts[c] = next;
        out.push_back(c);
    }
    // Ensure minimum size by appending owned uniques if needed
    if (out.size() < 5) {
        auto owned = list_owned_cards(uid);
        for (const auto& c : owned) {
            if (out.size() >= 5) break;
            const uint8_t max_copies = max_copies_for_level(get_card_level(uid, c));
            if (counts[c] >= static_cast<int>(max_copies)) continue;
            counts[c] += 1;
            out.push_back(c);
            changed = true;
        }
    }
    if (changed) cards.swap(out);
    return changed;
}


std::string Database::roll_gacha_card(uint32_t uid) {
    return roll_chest_card(uid);
}

std::vector<std::string> Database::get_deck(uint32_t uid) {
    std::vector<std::string> out;
    if (!ensure_alive()) {
        return out;
    }
    const std::string sql =
        "SELECT card_key FROM player_deck WHERE uid=" + std::to_string(uid) + " ORDER BY slot ASC";
    if (!query(sql)) {
        return out;
    }
    MYSQL_RES* res = mysql_store_result(conn_);
    if (!res) {
        return out;
    }
    while (MYSQL_ROW row = mysql_fetch_row(res)) {
        if (row[0]) {
            out.emplace_back(row[0]);
        }
    }
    mysql_free_result(res);
    return out;
}

bool Database::save_deck(uint32_t uid, const std::vector<std::string>& cards, std::string& err) {
    if (cards.size() < 5 || cards.size() > 12) {
        err = "deck size 5-12";
        return false;
    }
    ensure_owned_starter(uid);
    auto owned = list_owned_cards(uid);
    std::unordered_map<std::string, int> counts;
    for (const auto& c : cards) {
        if (!is_known_card(c)) {
            err = "unknown card";
            return false;
        }
        bool ok = false;
        for (const auto& o : owned) {
            if (o == c) {
                ok = true;
                break;
            }
        }
        if (!ok) {
            err = "card not owned";
            return false;
        }
        counts[c] += 1;
        const uint8_t max_copies = max_copies_for_level(get_card_level(uid, c));
        if (counts[c] > static_cast<int>(max_copies)) {
            err = "too many copies";
            return false;
        }
    }

    if (!query("DELETE FROM player_deck WHERE uid=" + std::to_string(uid))) {
        err = "save failed";
        return false;
    }
    for (size_t i = 0; i < cards.size(); ++i) {
        char sql[256];
        std::snprintf(sql, sizeof(sql),
                      "INSERT INTO player_deck (uid, slot, card_key) VALUES (%u, %u, '%s')",
                      uid, static_cast<unsigned>(i), escape(cards[i]).c_str());
        if (!query(sql)) {
            err = "save failed";
            return false;
        }
    }
    err = "ok";
    return true;
}
