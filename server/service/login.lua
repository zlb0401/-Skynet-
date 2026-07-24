local skynet = require "skynet"
local crypt = require "skynet.crypt"
local password_util = require "password_util"

local CMD = {}
local DB

-- In-memory cache for active tokens.
local sessions = {}

local function make_token(uid, username)
	local raw = string.format("%d:%s:%.0f", uid, username, skynet.time())
	return crypt.hexencode(crypt.hashkey(raw))
end

local function session_login(uid, username)
	local token = make_token(uid, username)
	sessions[token] = {
		uid = uid,
		username = username,
		login_time = skynet.time(),
	}
	skynet.call(DB, "lua", "save_session", token, uid, username, 86400)
	return token
end

-- Preferred: Unity uses C++ Auth (:8889), then enters Skynet with token.
-- Returns: ok, uid, token, message, username
function CMD.token_login(token)
	if not token or token == "" then
		return false, 0, "", "token is empty", ""
	end

	local mem = sessions[token]
	if mem then
		return true, mem.uid, token, "login success", mem.username
	end

	local row = skynet.call(DB, "lua", "find_session", token)
	if not row then
		return false, 0, "", "invalid or expired token", ""
	end

	local uid = tonumber(row.uid)
	local username = row.username
	sessions[token] = {
		uid = uid,
		username = username,
		login_time = skynet.time(),
	}

	skynet.error(string.format("[login] token_login user=%s uid=%d", username, uid))
	return true, uid, token, "login success", username
end

-- Legacy password login (tools). Client UI should use C++ Auth.
function CMD.login(username, password)
	local ok, err = password_util.validate_username(username)
	if not ok then
		return false, 0, "", err
	end
	if not password or password == "" then
		return false, 0, "", "password is empty"
	end

	local row = skynet.call(DB, "lua", "find_user", username)
	if not row or not password_util.verify(password, row.password) then
		return false, 0, "", "invalid username or password"
	end

	local uid = tonumber(row.id)
	local token = session_login(uid, username)
	skynet.error(string.format("[login] user=%s uid=%d (legacy lua)", username, uid))
	return true, uid, token, "login success"
end

function CMD.register(_username, _password)
	return false, 0, "", "use C++ Auth service :8889"
end

function CMD.verify_token(token)
	local mem = sessions[token]
	if mem then
		return mem
	end
	local row = skynet.call(DB, "lua", "find_session", token)
	if not row then
		return nil
	end
	return {
		uid = tonumber(row.uid),
		username = row.username,
	}
end

function CMD.logout(token)
	-- Only clear Skynet memory. MySQL auth_sessions is owned by C++ Auth (:8889).
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
