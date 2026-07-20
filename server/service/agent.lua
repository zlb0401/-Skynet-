local skynet = require "skynet"
local socket = require "skynet.socket"

local protocol = require "protocol"
local msg_id = require "msg_id"

local LOGIN
local MATCH
local client_fd
local room_addr
local room_id = 0

local logged_in = false
local uid = 0
local username = ""
local token = ""

local function send_package(msg_id_value, payload)
	if not client_fd then
		return
	end
	local data = protocol.pack(msg_id_value, payload)
	socket.write(client_fd, data)
end

local function handle_login(payload)
	local user, pass = protocol.unpack_login_req(payload)
	if not user then
		send_package(msg_id.S2C_LoginResp, protocol.pack_login_resp(false, "bad login packet"))
		return
	end

	local ok, user_id, user_token, message = skynet.call(LOGIN, "lua", "login", user, pass)
	send_package(msg_id.S2C_LoginResp, protocol.pack_login_resp(ok, message, user_id, user_token))
	if ok then
		logged_in = true
		uid = user_id
		username = user
		token = user_token
		skynet.error(string.format("[agent] fd=%d login ok user=%s uid=%d", client_fd, user, user_id))
	end
end

local function handle_match(_payload)
	if not logged_in then
		send_package(msg_id.S2C_MatchResp, protocol.pack_match_resp(false, "login first"))
		return
	end

	local matched, status = skynet.call(MATCH, "lua", "enqueue", uid, username, skynet.self())
	if not matched then
		send_package(msg_id.S2C_MatchResp, protocol.pack_match_resp(false, status))
	end
end

local function handle_heartbeat(_payload)
	send_package(msg_id.S2C_Heartbeat, protocol.pack_heartbeat_resp())
end

local function handle_battle_ready(_payload)
	if room_addr then
		skynet.send(room_addr, "lua", "player_ready", uid)
	end
end

local function handle_play_card(payload)
	local hand_index = protocol.unpack_play_card(payload)
	if hand_index == nil or not room_addr then
		return
	end
	skynet.send(room_addr, "lua", "play_card", uid, hand_index)
end

local function handle_end_turn(_payload)
	if room_addr then
		skynet.send(room_addr, "lua", "end_turn", uid)
	end
end

local handlers = {
	[msg_id.C2S_LoginReq] = handle_login,
	[msg_id.C2S_MatchReq] = handle_match,
	[msg_id.C2S_PlayCard] = handle_play_card,
	[msg_id.C2S_EndTurn] = handle_end_turn,
	[msg_id.C2S_BattleReady] = handle_battle_ready,
	[msg_id.C2S_Heartbeat] = handle_heartbeat,
}

local function handle_packets(packets)
	for _, packet in ipairs(packets) do
		skynet.error(string.format("[agent] fd=%d recv msg_id=%d", client_fd, packet.msg_id))
		local handler = handlers[packet.msg_id]
		if handler then
			handler(packet.payload)
		else
			send_package(msg_id.S2C_Error, protocol.pack_login_resp(false, "unknown message"))
		end
	end
end

local CMD = {}

function CMD.start(conf)
	client_fd = conf.client
	LOGIN = conf.login
	MATCH = conf.match
	skynet.call(conf.gate, "lua", "accept", client_fd)
	skynet.error(string.format("[agent] fd=%d started", client_fd))
end

function CMD.recv(msg)
	if #msg < 2 then
		return
	end
	local packet_msg_id = string.unpack(">I2", msg)
	local payload = msg:sub(3)
	handle_packets({ { msg_id = packet_msg_id, payload = payload } })
end

function CMD.match_success(rid, opponent_uid, opponent_name)
	room_id = rid
	send_package(
		msg_id.S2C_MatchResp,
		protocol.pack_match_resp(true, "match success", rid, opponent_uid, opponent_name)
	)
	skynet.error(string.format(
		"[agent] fd=%d matched room=%d opponent=%s(%d)",
		client_fd, rid, opponent_name, opponent_uid
	))
end

function CMD.bind_room(addr, rid)
	room_addr = addr
	room_id = rid
	skynet.error(string.format("[agent] fd=%d bind room=%d", client_fd, rid))
end

function CMD.push(mid, payload)
	send_package(mid, payload)
end

function CMD.disconnect()
	if room_addr then
		skynet.send(room_addr, "lua", "player_leave", uid)
		room_addr = nil
	end
	if logged_in and MATCH then
		skynet.send(MATCH, "lua", "dequeue", skynet.self())
	end
	if token ~= "" then
		skynet.send(LOGIN, "lua", "logout", token)
	end
	skynet.error(string.format("[agent] fd=%d disconnected", client_fd))
	skynet.exit()
end

skynet.start(function()
	skynet.dispatch("lua", function(_, _, cmd, ...)
		local f = CMD[cmd]
		if not f then
			return
		end
		-- fire-and-forget commands
		if cmd == "match_success" or cmd == "recv" or cmd == "push"
			or cmd == "bind_room" or cmd == "disconnect" then
			f(...)
		else
			skynet.ret(skynet.pack(f(...)))
		end
	end)
end)
