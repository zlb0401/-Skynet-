#pragma once
#include <string>
#include <utility>
#include <vector>

// rarity: 0 Common, 1 Uncommon, 2 Rare, 3 Legendary
inline const std::vector<std::pair<std::string, uint8_t>>& all_cards() {
    static const std::vector<std::pair<std::string, uint8_t>> k = {
        {"Blood Rush", 0}, {"Lashing Out", 0}, {"Last Resort", 1}, {"Wide Swing", 0},
        {"Iron Strike", 1}, {"Cleave", 1}, {"Piercing Thrust", 2},
        {"Brace", 0}, {"Gut Reaction", 0}, {"Shift Stance", 1}, {"Iron Wall", 1},
        {"Shield Bash", 1}, {"Fortify", 2},
        {"Deep Focus", 1}, {"Fight or Flight", 0}, {"Focus Breathing", 0},
        {"Snap Decision", 1}, {"Meditate", 1}, {"Quick Draw", 2}, {"Smoke Screen", 2},
    };
    return k;
}

inline const std::vector<std::string>& all_card_keys() {
    static std::vector<std::string> keys;
    if (keys.empty()) {
        for (const auto& c : all_cards()) keys.push_back(c.first);
    }
    return keys;
}

inline bool is_known_card(const std::string& key) {
    for (const auto& c : all_cards()) if (c.first == key) return true;
    return false;
}

inline uint8_t card_rarity(const std::string& key) {
    for (const auto& c : all_cards()) if (c.first == key) return c.second;
    return 0;
}

inline int32_t dupe_dust_for_rarity(uint8_t rarity) {
    switch (rarity) {
        case 1: return 20;
        case 2: return 50;
        case 3: return 120;
        default: return 8;
    }
}
