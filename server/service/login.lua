local skynet = require "skynet"
local crypt = require "skynet.crypt"

local CMD = {}
local DB

local sessions = {}

local function make_token(uid, username)
	local raw = string.format("%d:%s:%.0f", uid, username, skynet.time())
	return crypt.hexencode(crypt.hashkey(raw))
end

function CMD.login(username, password)
	if not username or username == "" then
		return false, 0, "", "username is empty"
	end

	local row = skynet.call(DB, "lua", "find_user", username)
	if not row or row.password ~= password then
		return false, 0, "", "invalid username or password"
	end

	local uid = tonumber(row.id)
	local token = make_token(uid, username)
	sessions[token] = {
		uid = uid,
		username = username,
		login_time = skynet.time(),
	}

	skynet.error(string.format("[login] user=%s uid=%d (mysql)", username, uid))
	return true, uid, token, "login success"
end

function CMD.verify_token(token)
	return sessions[token]
end

function CMD.logout(token)
	sessions[token] = nil
	return true
end

skynet.start(function()
	DB = skynet.uniqueservice("db")

	skynet.dispatch("lua", function(_, _, cmd, ...)
		local f = assert(CMD[cmd], cmd)
		skynet.ret(skynet.pack(f(...)))
	end)
end)
