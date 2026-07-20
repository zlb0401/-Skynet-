local skynet = require "skynet"

local CMD = {}
local queue = {}
local room_seq = 1000

local function remove_from_queue(agent)
	for i, item in ipairs(queue) do
		if item.agent == agent then
			table.remove(queue, i)
			skynet.error(string.format("[match] dequeue uid=%d", item.uid))
			return true
		end
	end
end

local function start_room(player_a, player_b)
	room_seq = room_seq + 1
	local room = skynet.newservice("room")
	skynet.call(room, "lua", "init", room_seq, {
		{ uid = player_a.uid, username = player_a.username, agent = player_a.agent },
		{ uid = player_b.uid, username = player_b.username, agent = player_b.agent },
	})

	skynet.send(player_a.agent, "lua", "match_success", room_seq, player_b.uid, player_b.username)
	skynet.send(player_b.agent, "lua", "match_success", room_seq, player_a.uid, player_a.username)
	skynet.error(string.format(
		"[match] room=%d %s(%d) vs %s(%d)",
		room_seq,
		player_a.username, player_a.uid,
		player_b.username, player_b.uid
	))
end

function CMD.enqueue(uid, username, agent)
	remove_from_queue(agent)

	local item = {
		uid = uid,
		username = username,
		agent = agent,
		time = skynet.time(),
	}
	table.insert(queue, item)
	skynet.error(string.format("[match] enqueue uid=%d queue=%d", uid, #queue))

	if #queue >= 2 then
		local a = table.remove(queue, 1)
		local b = table.remove(queue, 1)
		start_room(a, b)
		return true, "matched"
	end

	return false, "waiting"
end

function CMD.dequeue(agent)
	if remove_from_queue(agent) then
		return true
	end
	return false
end

function CMD.queue_size()
	return #queue
end

skynet.start(function()
	skynet.dispatch("lua", function(_, _, cmd, ...)
		local f = assert(CMD[cmd], cmd)
		skynet.ret(skynet.pack(f(...)))
	end)
end)
