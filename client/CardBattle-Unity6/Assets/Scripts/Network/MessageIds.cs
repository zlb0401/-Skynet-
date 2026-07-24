namespace CardBattle.Network
{
    public static class MessageIds
    {
        public const ushort C2S_LoginReq = 1001;
        public const ushort C2S_MatchReq = 1002;
        public const ushort C2S_PlayCard = 1003;
        public const ushort C2S_EndTurn = 1004;
        public const ushort C2S_BattleReady = 1005;
        public const ushort C2S_RegisterReq = 1006;
        public const ushort C2S_TokenLoginReq = 1007;
        public const ushort C2S_CaptchaReq = 1008;
        public const ushort C2S_InventoryReq = 1009;
        public const ushort C2S_ClaimRewardReq = 1010;
        public const ushort C2S_ListUpgradesReq = 1011;
        public const ushort C2S_UpgradeCardReq = 1012;
        public const ushort C2S_UseItemReq = 1013;
        public const ushort C2S_GetDeckReq = 1014;
        public const ushort C2S_SaveDeckReq = 1015;
        public const ushort C2S_GachaReq = 1016;
        public const ushort C2S_Heartbeat = 1099;

        public const ushort S2C_LoginResp = 2001;
        public const ushort S2C_MatchResp = 2002;
        public const ushort S2C_BattleStart = 2003;
        public const ushort S2C_BattleState = 2004;
        public const ushort S2C_BattleEnd = 2005;
        public const ushort S2C_RegisterResp = 2006;
        public const ushort S2C_CaptchaResp = 2008;
        public const ushort S2C_InventoryResp = 2009;
        public const ushort S2C_ClaimRewardResp = 2010;
        public const ushort S2C_ListUpgradesResp = 2011;
        public const ushort S2C_UpgradeCardResp = 2012;
        public const ushort S2C_UseItemResp = 2013;
        public const ushort S2C_GetDeckResp = 2014;
        public const ushort S2C_SaveDeckResp = 2015;
        public const ushort S2C_GachaResp = 2016;
        public const ushort S2C_Heartbeat = 2099;
        public const ushort S2C_Error = 2999;
    }
}
