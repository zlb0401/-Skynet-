local skynet = require "skynet"

local CMD = {}
local SOCKET = {}
local gate
local agent = {}
local login_service
local match_service

function SOCKET.open(fd, addr)
	skynet.error(string.format("[watchdog] new client fd=%d addr=%s", fd, addr))
	agent[fd] = skynet.newservice("agent")
	skynet.call(agent[fd], "lua", "start", {
		gate = gate,
		client = fd,
		watchdog = skynet.self(),
		login = login_service,
		match = match_service,
	})
end

local function close_agent(fd)
	local a = agent[fd]
	agent[fd] = nil
	if a then
		skynet.call(gate, "lua", "kick", fd)
		skynet.send(a, "lua", "disconnect")
	end
end

function SOCKET.close(fd)
	skynet.error(string.format("[watchdog] socket close fd=%d", fd))
	close_agent(fd)
end

function SOCKET.error(fd, msg)
	skynet.error(string.format("[watchdog] socket error fd=%d msg=%s", fd, msg))
	close_agent(fd)
end

function SOCKET.warning(fd, size)
	skynet.error(string.format("[watchdog] socket warning fd=%d pending=%dKB", fd, size))
end

-- With gate.accept (no forward), client data arrives here already length-stripped.
function SOCKET.data(fd, msg)
	local a = agent[fd]
	if a then
		skynet.send(a, "lua", "recv", msg)
	end
end

function CMD.start(conf)
	return skynet.call(gate, "lua", "open", conf)
end

function CMD.close(fd)
	close_agent(fd)
end

skynet.start(function()
	login_service = skynet.uniqueservice("login")
	match_service = skynet.uniqueservice("match")

	skynet.dispatch("lua", function(session, source, cmd, subcmd, ...)
		if cmd == "socket" then
			local f = SOCKET[subcmd]
			f(...)
		else
			local f = assert(CMD[cmd])
			skynet.ret(skynet.pack(f(subcmd, ...)))
		end
	end)

	gate = skynet.newservice("gate")
end)
