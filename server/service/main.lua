local skynet = require "skynet"

local GATE_PORT = 8888
local MAX_CLIENT = 64

skynet.start(function()
	skynet.error("skynet-card-battle server starting")

	if not skynet.getenv("daemon") then
		-- console needs a TTY; skip under nohup
		-- skynet.newservice("console")
	end

	skynet.newservice("debug_console", 8000)

	local watchdog = skynet.newservice("watchdog")
	local addr, port = skynet.call(watchdog, "lua", "start", {
		address = "0.0.0.0",
		port = GATE_PORT,
		maxclient = MAX_CLIENT,
		nodelay = true,
	})

	skynet.error(string.format("gate listen on %s:%d", addr, port))
	skynet.error("test accounts: test/123456, demo/123456")
	skynet.exit()
end)
