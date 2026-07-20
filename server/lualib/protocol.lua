local M = {}

-- Packet: [uint16 body_len][uint16 msg_id][payload...]
-- body_len = 2 + #payload

function M.pack(msg_id, payload)
	payload = payload or ""
	local body = string.pack(">I2", msg_id) .. payload
	return string.pack(">I2", #body) .. body
end

function M.read_packets(buffer)
	local packets = {}
	local consumed = 0

	while true do
		local remain = #buffer - consumed
		if remain < 2 then
			break
		end

		local body_len = string.unpack(">I2", buffer, consumed + 1)
		if remain < 2 + body_len then
			break
		end

		local body_start = consumed + 3
		local body_end = consumed + 2 + body_len
		local body = buffer:sub(body_start, body_end)
		local msg_id = string.unpack(">I2", body)
		local payload = body:sub(3)

		packets[#packets + 1] = { msg_id = msg_id, payload = payload }
		consumed = body_end
	end

	local leftover = buffer:sub(consumed + 1)
	return packets, leftover
end

function M.pack_login_req(username, password)
	return string.pack(">B", #username) .. username
		.. string.pack(">B", #password) .. password
end

function M.unpack_login_req(payload)
	if #payload < 2 then
		return nil, nil
	end
	local ulen = string.unpack(">B", payload)
	if #payload < 1 + ulen + 1 then
		return nil, nil
	end
	local username = payload:sub(2, 1 + ulen)
	local plen_offset = 2 + ulen
	local plen = string.unpack(">B", payload, plen_offset)
	if #payload < plen_offset + plen then
		return nil, nil
	end
	local password = payload:sub(plen_offset + 1, plen_offset + plen)
	return username, password
end

function M.pack_login_resp(ok, message, uid, token)
	local code = ok and 1 or 0
	message = message or ""
	uid = uid or 0
	token = token or ""
	local payload = string.pack(">B", code)
		.. string.pack(">I2", #message) .. message
	if ok then
		payload = payload
			.. string.pack(">I4", uid)
			.. string.pack(">B", #token) .. token
	end
	return payload
end

function M.pack_heartbeat_resp()
	return string.pack(">I4", os.time())
end

function M.pack_match_resp(ok, message, room_id, opponent_uid, opponent_name)
	local code = ok and 1 or 0
	message = message or ""
	opponent_name = opponent_name or ""
	local payload = string.pack(">B", code)
		.. string.pack(">I2", #message) .. message
	if ok then
		payload = payload
			.. string.pack(">I4", room_id or 0)
			.. string.pack(">I4", opponent_uid or 0)
			.. string.pack(">B", #opponent_name) .. opponent_name
	end
	return payload
end

function M.pack_str8(s)
	s = s or ""
	if #s > 255 then
		s = s:sub(1, 255)
	end
	return string.pack(">B", #s) .. s
end

-- Battle snapshot for one viewer
-- See Docs for field layout
function M.pack_battle_state(view)
	local payload = string.pack(">I4", view.room_id or 0)
		.. string.pack(">I4", view.self_uid or 0)
		.. string.pack(">I4", view.opp_uid or 0)
		.. M.pack_str8(view.self_name)
		.. M.pack_str8(view.opp_name)
		.. string.pack(">I4", view.turn_uid or 0)
		.. string.pack(">I2", view.turn_no or 0)
		.. string.pack(">i2", view.self_hp or 0)
		.. string.pack(">i2", view.self_max_hp or 0)
		.. string.pack(">B", view.self_energy or 0)
		.. string.pack(">B", view.self_max_energy or 0)
		.. string.pack(">I2", view.self_armor or 0)
		.. string.pack(">i2", view.opp_hp or 0)
		.. string.pack(">i2", view.opp_max_hp or 0)
		.. string.pack(">B", view.opp_energy or 0)
		.. string.pack(">B", view.opp_max_energy or 0)
		.. string.pack(">I2", view.opp_armor or 0)

	local hand = view.hand or {}
	payload = payload .. string.pack(">B", #hand)
	for _, cid in ipairs(hand) do
		payload = payload .. string.pack(">I2", cid)
	end

	payload = payload
		.. string.pack(">B", view.opp_hand_n or 0)
		.. string.pack(">B", view.draw_n or 0)
		.. string.pack(">B", view.discard_n or 0)
		.. string.pack(">B", view.finished and 1 or 0)
		.. string.pack(">I4", view.winner_uid or 0)
		.. M.pack_str8(view.last_event or "")

	return payload
end

function M.pack_battle_end(winner_uid, message)
	message = message or ""
	return string.pack(">I4", winner_uid or 0) .. M.pack_str8(message)
end

function M.unpack_play_card(payload)
	if not payload or #payload < 1 then
		return nil
	end
	return string.unpack(">B", payload) -- hand_index (0-based)
end

return M
