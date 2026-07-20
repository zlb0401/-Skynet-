local skynet = require "skynet"
local protocol = require "protocol"
local msg_id = require "msg_id"

-- Card catalog (server authoritative)
-- id -> {name, cost, dmg, armor, draw, energy_gain}
local CARDS = {
	[1] = { name = "猛击", cost = 1, dmg = 6, armor = 0, draw = 0, energy_gain = 0 },
	[2] = { name = "防御", cost = 1, dmg = 0, armor = 6, draw = 0, energy_gain = 0 },
	[3] = { name = "重击", cost = 2, dmg = 10, armor = 0, draw = 0, energy_gain = 0 },
	[4] = { name = "专注", cost = 0, dmg = 0, armor = 0, draw = 1, energy_gain = 1 },
}

local STARTING_DECK = { 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 4 }
local MAX_HP = 50
local MAX_ENERGY = 3
local DRAW_PER_TURN = 5

local room_id
local players = {} -- [1]=playerA, [2]=playerB  each: uid, username, agent, ready
local battle
local finished = false

local function shuffle(t)
	for i = #t, 2, -1 do
		local j = math.random(i)
		t[i], t[j] = t[j], t[i]
	end
end

local function copy_list(t)
	local r = {}
	for i, v in ipairs(t) do
		r[i] = v
	end
	return r
end

local function find_player(uid)
	for i, p in ipairs(players) do
		if p.uid == uid then
			return p, i
		end
	end
end

local function opponent_of(uid)
	for _, p in ipairs(players) do
		if p.uid ~= uid then
			return p
		end
	end
end

local function push_agent(agent, mid, payload)
	skynet.send(agent, "lua", "push", mid, payload)
end

local function build_view(viewer)
	local me = battle.sides[viewer.uid]
	local opp_player = opponent_of(viewer.uid)
	local opp = battle.sides[opp_player.uid]
	return {
		room_id = room_id,
		self_uid = viewer.uid,
		opp_uid = opp_player.uid,
		self_name = viewer.username,
		opp_name = opp_player.username,
		turn_uid = battle.turn_uid,
		turn_no = battle.turn_no,
		self_hp = me.hp,
		self_max_hp = me.max_hp,
		self_energy = me.energy,
		self_max_energy = me.max_energy,
		self_armor = me.armor,
		opp_hp = opp.hp,
		opp_max_hp = opp.max_hp,
		opp_energy = opp.energy,
		opp_max_energy = opp.max_energy,
		opp_armor = opp.armor,
		hand = copy_list(me.hand),
		opp_hand_n = #opp.hand,
		draw_n = #me.draw,
		discard_n = #me.discard,
		finished = finished,
		winner_uid = battle.winner_uid or 0,
		last_event = battle.last_event or "",
	}
end

local function broadcast_state()
	for _, p in ipairs(players) do
		local view = build_view(p)
		local payload = protocol.pack_battle_state(view)
		local mid = battle.started_once and msg_id.S2C_BattleState or msg_id.S2C_BattleStart
		push_agent(p.agent, mid, payload)
	end
	battle.started_once = true
end

local function draw_cards(side, n)
	for _ = 1, n do
		if #side.draw == 0 then
			if #side.discard == 0 then
				break
			end
			side.draw = copy_list(side.discard)
			side.discard = {}
			shuffle(side.draw)
		end
		if #side.draw > 0 then
			table.insert(side.hand, table.remove(side.draw))
		end
	end
end

local function start_turn(uid)
	local side = battle.sides[uid]
	battle.turn_uid = uid
	battle.turn_no = battle.turn_no + 1
	side.energy = side.max_energy
	side.armor = 0
	-- discard remaining hand then draw
	for _, cid in ipairs(side.hand) do
		table.insert(side.discard, cid)
	end
	side.hand = {}
	draw_cards(side, DRAW_PER_TURN)
	local p = find_player(uid)
	battle.last_event = string.format("%s 的回合", p.username)
end

local function check_death()
	for _, p in ipairs(players) do
		local side = battle.sides[p.uid]
		if side.hp <= 0 then
			side.hp = 0
			finished = true
			local winner = opponent_of(p.uid)
			battle.winner_uid = winner.uid
			battle.last_event = string.format("%s 获胜", winner.username)
			broadcast_state()
			for _, pl in ipairs(players) do
				push_agent(pl.agent, msg_id.S2C_BattleEnd,
					protocol.pack_battle_end(battle.winner_uid, battle.last_event))
			end
			return true
		end
	end
	return false
end

local function apply_card(actor, card_id)
	local def = CARDS[card_id]
	if not def then
		return false, "unknown card"
	end
	local me = battle.sides[actor.uid]
	local opp_p = opponent_of(actor.uid)
	local opp = battle.sides[opp_p.uid]

    if me.energy < def.cost then
		return false, "能量不足"
	end
	me.energy = me.energy - def.cost

	if def.armor > 0 then
		me.armor = me.armor + def.armor
	end
	if def.energy_gain > 0 then
		me.energy = math.min(me.max_energy, me.energy + def.energy_gain)
	end
	if def.draw > 0 then
		draw_cards(me, def.draw)
	end
	if def.dmg > 0 then
		local dmg = def.dmg
		if opp.armor >= dmg then
			opp.armor = opp.armor - dmg
			dmg = 0
		else
			dmg = dmg - opp.armor
			opp.armor = 0
			opp.hp = opp.hp - dmg
		end
	end

	battle.last_event = string.format("%s 打出 %s", actor.username, def.name)
	return true
end

local function create_side()
	local deck = copy_list(STARTING_DECK)
	shuffle(deck)
	return {
		hp = MAX_HP,
		max_hp = MAX_HP,
		energy = MAX_ENERGY,
		max_energy = MAX_ENERGY,
		armor = 0,
		draw = deck,
		hand = {},
		discard = {},
	}
end

local function begin_battle()
	math.randomseed(math.floor(skynet.time() * 1000) % 2147483647)
	battle = {
		sides = {},
		turn_uid = players[1].uid,
		turn_no = 0,
		winner_uid = 0,
		last_event = "战斗开始",
		started_once = false,
	}
	for _, p in ipairs(players) do
		battle.sides[p.uid] = create_side()
	end
	-- first player random
	if math.random(2) == 2 then
		players[1], players[2] = players[2], players[1]
	end
	start_turn(players[1].uid)
	skynet.error(string.format("[room] battle start room=%d first=%s", room_id, players[1].username))
	broadcast_state()
end

local CMD = {}

function CMD.init(id, player_list)
	room_id = id
	players = player_list
	for _, p in ipairs(players) do
		p.ready = false
		skynet.send(p.agent, "lua", "bind_room", skynet.self(), room_id)
	end
	skynet.error(string.format("[room] id=%d players=%d waiting ready", room_id, #players))
	return true
end

function CMD.player_ready(uid)
	if finished or (battle and battle.started_once) then
		return
	end
	local p = find_player(uid)
	if not p then
		return
	end
	p.ready = true
	skynet.error(string.format("[room] ready uid=%d", uid))
	local all = true
	for _, pl in ipairs(players) do
		if not pl.ready then
			all = false
			break
		end
	end
	if all and not battle then
		begin_battle()
	end
end

function CMD.play_card(uid, hand_index)
	if finished or not battle then
		return
	end
	if battle.turn_uid ~= uid then
		skynet.error(string.format("[room] play_card rejected uid=%d (turn=%d)", uid, battle.turn_uid))
		battle.last_event = "不是你的回合"
		broadcast_state()
		return
	end
	local actor = find_player(uid)
	local side = battle.sides[uid]
	hand_index = hand_index + 1 -- lua 1-based
	if hand_index < 1 or hand_index > #side.hand then
		skynet.error(string.format("[room] play_card bad index uid=%d idx=%d hand=%d", uid, hand_index, #side.hand))
		battle.last_event = "非法出牌"
		broadcast_state()
		return
	end
	local card_id = side.hand[hand_index]
	-- Remove from hand first so draw effects cannot shift the played card index.
	table.remove(side.hand, hand_index)
	local ok, err = apply_card(actor, card_id)
	if not ok then
		-- rollback: put card back
		table.insert(side.hand, hand_index, card_id)
		skynet.error(string.format("[room] play_card fail uid=%d card=%s err=%s", uid, tostring(card_id), tostring(err)))
		battle.last_event = err or "出牌失败"
		broadcast_state()
		return
	end
	skynet.error(string.format("[room] play_card ok uid=%d card=%s hp_opp=%d",
		uid, tostring(card_id), battle.sides[opponent_of(uid).uid].hp))
	table.insert(side.discard, card_id)
	if check_death() then
		return
	end
	broadcast_state()
end

function CMD.end_turn(uid)
	if finished or not battle then
		return
	end
	if battle.turn_uid ~= uid then
		battle.last_event = "不是你的回合"
		broadcast_state()
		return
	end
	local next_p = opponent_of(uid)
	start_turn(next_p.uid)
	broadcast_state()
end

function CMD.player_leave(uid)
	if finished then
		return
	end
	local winner = opponent_of(uid)
	if not winner then
		return
	end
	finished = true
	if battle then
		battle.winner_uid = winner.uid
		battle.last_event = "对手离线，你获胜"
		broadcast_state()
		push_agent(winner.agent, msg_id.S2C_BattleEnd,
			protocol.pack_battle_end(winner.uid, battle.last_event))
	end
end

function CMD.get_info()
	return {
		room_id = room_id,
		players = players,
		finished = finished,
	}
end

skynet.start(function()
	skynet.dispatch("lua", function(_, _, cmd, ...)
		local f = CMD[cmd]
		if not f then
			skynet.error("[room] unknown cmd " .. tostring(cmd))
			return
		end
		if cmd == "init" or cmd == "get_info" then
			skynet.ret(skynet.pack(f(...)))
		else
			f(...)
		end
	end)
end)
