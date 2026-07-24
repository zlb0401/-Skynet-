local M = {}

-- Client -> Server
M.C2S_LoginReq     = 1001
M.C2S_MatchReq     = 1002
M.C2S_PlayCard     = 1003
M.C2S_EndTurn      = 1004
M.C2S_BattleReady  = 1005
M.C2S_RegisterReq  = 1006 -- deprecated: use C++ Auth :8889
M.C2S_TokenLoginReq = 1007
M.C2S_Heartbeat    = 1099

-- Server -> Client
M.S2C_LoginResp    = 2001
M.S2C_MatchResp    = 2002
M.S2C_BattleStart  = 2003
M.S2C_BattleState  = 2004
M.S2C_BattleEnd    = 2005
M.S2C_RegisterResp = 2006 -- deprecated
M.S2C_Heartbeat    = 2099
M.S2C_Error        = 2999

return M
