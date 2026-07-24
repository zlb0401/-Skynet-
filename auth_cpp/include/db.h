#pragma once

#include <mysql/mysql.h>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include "config.h"

struct UserRow {
    uint32_t id = 0;
    std::string username;
    std::string password;
};

struct SessionRow {
    uint32_t uid = 0;
    std::string username;
};

struct WalletRow {
    int32_t gold = 0;
    int32_t dust = 0;
    int32_t diamond = 0;
    int32_t ticket = 0;
};

struct InventoryItem {
    uint16_t item_id = 0;
    uint32_t count = 0;
};

struct CardLevelRow {
    std::string card_key;
    uint8_t level = 0;
};

class Database {
public:
    explicit Database(const AuthConfig& cfg);
    ~Database();

    Database(const Database&) = delete;
    Database& operator=(const Database&) = delete;

    bool connect();
    void ensure_schema();

    std::optional<UserRow> find_user(const std::string& username);
    std::optional<uint32_t> create_user(const std::string& username, const std::string& password_hash);
    bool save_session(const std::string& token, uint32_t uid, const std::string& username, int ttl_seconds);
    std::optional<SessionRow> find_session(const std::string& token);
    void purge_expired_sessions();

    void ensure_wallet(uint32_t uid);
    WalletRow get_wallet(uint32_t uid);
    std::vector<InventoryItem> get_inventory(uint32_t uid);
    bool add_item(uint32_t uid, uint16_t item_id, uint32_t count);
    bool consume_item(uint32_t uid, uint16_t item_id, uint32_t count);
    bool add_gold(uint32_t uid, int32_t delta);
    bool add_dust(uint32_t uid, int32_t delta);
    bool add_diamond(uint32_t uid, int32_t delta);
    bool add_ticket(uint32_t uid, int32_t delta);
    bool spend_diamond(uint32_t uid, int32_t cost);
    bool spend_ticket(uint32_t uid, int32_t cost);
    bool spend_gold(uint32_t uid, int32_t cost);
    /// Returns false if already claimed this run-key (idempotent per uid+stage+run).
    bool try_claim_stage(uint32_t uid, const std::string& stage_key, const std::string& run_id,
                         int32_t gold_delta, int32_t dust_delta);
    /// Account-lifetime first clear milestones (diamond/ticket).
    bool try_claim_milestone(uint32_t uid, const std::string& mile_key);

    uint8_t get_card_level(uint32_t uid, const std::string& card_key);
    std::vector<CardLevelRow> list_card_levels(uint32_t uid);
    /// Spend dust and bump card level by 1. Returns false on insufficient dust / max level / unknown card.
    bool try_upgrade_card(uint32_t uid, const std::string& card_key, uint8_t& out_level, int32_t& out_dust_spent,
                          std::string& err);

    void ensure_owned_starter(uint32_t uid);
    bool add_owned_card(uint32_t uid, const std::string& card_key);
    std::vector<std::string> list_owned_cards(uint32_t uid);
    std::string roll_chest_card(uint32_t uid);
    struct GachaPullResult {
        std::string card_key;
        bool is_new = false;
        int32_t dust_gained = 0;
        uint8_t rarity = 0;
    };
    uint8_t get_gacha_pity(uint32_t uid);
    void set_gacha_pity(uint32_t uid, uint8_t pity);
    uint8_t get_chest_pity(uint32_t uid);
    void set_chest_pity(uint32_t uid, uint8_t pity);
    bool owns_card(uint32_t uid, const std::string& card_key);
    GachaPullResult roll_gacha_pull(uint32_t uid, bool force_high_rarity);
    /// Chest pool: 40% gold / 60% chest. Keys: __GOLD__, __CHEST_1/2/3__.
    /// dust_gained carries gold amount for __GOLD__.
    GachaPullResult roll_chest_pool_pull(uint32_t uid, uint8_t& pity_inout);
    std::string roll_gacha_card(uint32_t uid);
    std::vector<std::string> get_deck(uint32_t uid);
    bool save_deck(uint32_t uid, const std::vector<std::string>& cards, std::string& err);
    /// Returns true if deck was modified to obey copy caps.
    bool clamp_deck_copies(uint32_t uid, std::vector<std::string>& cards, std::string& err);

private:
    AuthConfig cfg_;
    MYSQL* conn_ = nullptr;

    std::string escape(const std::string& s);
    /// Ping / reconnect so overnight idle MySQL wait_timeout does not break login.
    bool ensure_alive();
    bool query(const std::string& sql);
};
