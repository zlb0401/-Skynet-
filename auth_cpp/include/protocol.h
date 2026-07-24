#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace protocol {

constexpr uint16_t C2S_LoginReq = 1001;
constexpr uint16_t C2S_RegisterReq = 1006;
constexpr uint16_t C2S_CaptchaReq = 1008;
constexpr uint16_t C2S_InventoryReq = 1009;
constexpr uint16_t C2S_ClaimRewardReq = 1010;
constexpr uint16_t C2S_ListUpgradesReq = 1011;
constexpr uint16_t C2S_UpgradeCardReq = 1012;
constexpr uint16_t C2S_UseItemReq = 1013;
constexpr uint16_t C2S_GetDeckReq = 1014;
constexpr uint16_t C2S_SaveDeckReq = 1015;
constexpr uint16_t C2S_GachaReq = 1016;
constexpr uint16_t S2C_LoginResp = 2001;
constexpr uint16_t S2C_MatchResp = 2002;
constexpr uint16_t S2C_RegisterResp = 2006;
constexpr uint16_t S2C_CaptchaResp = 2008;
constexpr uint16_t S2C_InventoryResp = 2009;
constexpr uint16_t S2C_ClaimRewardResp = 2010;
constexpr uint16_t S2C_ListUpgradesResp = 2011;
constexpr uint16_t S2C_UpgradeCardResp = 2012;
constexpr uint16_t S2C_UseItemResp = 2013;
constexpr uint16_t S2C_GetDeckResp = 2014;
constexpr uint16_t S2C_SaveDeckResp = 2015;
constexpr uint16_t S2C_GachaResp = 2016;

struct InventoryItemView {
    uint16_t item_id = 0;
    uint32_t count = 0;
};

struct CardUpgradeView {
    std::string card_key;
    uint8_t level = 0;
};

struct Packet {
    uint16_t msg_id = 0;
    std::vector<uint8_t> payload;
};

bool unpack_str8(const std::vector<uint8_t>& payload, size_t& offset, std::string& out);
void append_str8(std::vector<uint8_t>& out, const std::string& s);

bool unpack_login_req(const std::vector<uint8_t>& payload, std::string& username, std::string& password);
bool unpack_register_req(const std::vector<uint8_t>& payload,
                         std::string& username,
                         std::string& password,
                         std::string& captcha_id,
                         std::string& captcha_answer);

std::vector<uint8_t> pack_login_resp(bool ok, const std::string& message, uint32_t uid = 0, const std::string& token = "");
std::vector<uint8_t> pack_captcha_resp(const std::string& captcha_id, const std::string& question);
std::vector<uint8_t> pack_inventory_resp(bool ok,
                                         const std::string& message,
                                         int32_t gold,
                                         int32_t dust,
                                         int32_t diamond,
                                         int32_t ticket,
                                         const std::vector<InventoryItemView>& items);
std::vector<uint8_t> pack_claim_reward_resp(bool ok,
                                            const std::string& message,
                                            int32_t gold_delta,
                                            int32_t dust_delta,
                                            int32_t diamond_delta,
                                            int32_t ticket_delta,
                                            int32_t gold,
                                            int32_t dust,
                                            int32_t diamond,
                                            int32_t ticket);
std::vector<uint8_t> pack_list_upgrades_resp(bool ok,
                                             const std::string& message,
                                             int32_t gold,
                                             int32_t dust,
                                             int32_t diamond,
                                             int32_t ticket,
                                             const std::vector<CardUpgradeView>& upgrades);
std::vector<uint8_t> pack_upgrade_card_resp(bool ok,
                                            const std::string& message,
                                            const std::string& card_key,
                                            uint8_t level,
                                            int32_t dust_spent,
                                            int32_t gold,
                                            int32_t dust,
                                            int32_t diamond,
                                            int32_t ticket);
/// ok/msg[/gold/dust/diamond/ticket/granted_card/items...]
std::vector<uint8_t> pack_use_item_resp(bool ok,
                                        const std::string& message,
                                        int32_t gold,
                                        int32_t dust,
                                        int32_t diamond,
                                        int32_t ticket,
                                        const std::string& granted_card,
                                        const std::vector<InventoryItemView>& items);
struct GachaItemView {
    std::string card_key;
    uint8_t is_new = 0;      // 1=new unlock, 0=dupe→dust
    uint32_t dust_gained = 0;
    uint8_t rarity = 0;
};

std::vector<uint8_t> pack_gacha_resp(bool ok,
                                     const std::string& message,
                                     int32_t gold,
                                     int32_t dust,
                                     int32_t diamond,
                                     int32_t ticket,
                                     uint8_t pity,
                                     const std::vector<GachaItemView>& items);
std::vector<uint8_t> pack_deck_resp(bool ok,
                                    const std::string& message,
                                    const std::vector<std::string>& deck,
                                    const std::vector<std::string>& owned = {});
std::vector<uint8_t> pack_frame(uint16_t msg_id, const std::vector<uint8_t>& payload);

void append_bytes(std::vector<uint8_t>& buffer, const uint8_t* data, size_t n);
bool try_read_packet(std::vector<uint8_t>& buffer, Packet& out);

}  // namespace protocol
