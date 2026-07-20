namespace CardBattle.Network
{
    public static class MessageIds
    {
        public const ushort C2S_LoginReq = 1001;
        public const ushort C2S_MatchReq = 1002;
        public const ushort C2S_PlayCard = 1003;
        public const ushort C2S_EndTurn = 1004;
        public const ushort C2S_BattleReady = 1005;
        public const ushort C2S_Heartbeat = 1099;

        public const ushort S2C_LoginResp = 2001;
        public const ushort S2C_MatchResp = 2002;
        public const ushort S2C_BattleStart = 2003;
        public const ushort S2C_BattleState = 2004;
        public const ushort S2C_BattleEnd = 2005;
        public const ushort S2C_Heartbeat = 2099;
        public const ushort S2C_Error = 2999;
    }
}
