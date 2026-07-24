local skynet = require "skynet"
local mysql = require "skynet.db.mysql"
local db_conf = require "db_conf"

local CMD = {}
local db

function CMD.query(sql)
	skynet.error("[db] sql:", sql)
	local res = db:query(sql)
	if not res then
		error("db query failed")
	end
	if res.badresult then
		error(res.err or "db badresult")
	end
	return res
end

function CMD.find_user(username)
	local sql = string.format(
		"SELECT id, username, password FROM users WHERE username=%s LIMIT 1",
		mysql.quote_sql_str(username)
	)
	local res = CMD.query(sql)
	if res[1] then
		return res[1]
	end
end

function CMD.create_user(username, password_hash)
	local sql = string.format(
		"INSERT INTO users (username, password) VALUES (%s, %s)",
		mysql.quote_sql_str(username),
		mysql.quote_sql_str(password_hash)
	)
	local res = CMD.query(sql)
	local uid = tonumber(res.insert_id)
	if uid and uid > 0 then
		return uid
	end
end

function CMD.find_session(token)
	local sql = string.format(
		"SELECT token, uid, username FROM auth_sessions WHERE token=%s AND expires_at > NOW() LIMIT 1",
		mysql.quote_sql_str(token)
	)
	local res = CMD.query(sql)
	if res[1] then
		return res[1]
	end
end

function CMD.delete_session(token)
	local sql = string.format(
		"DELETE FROM auth_sessions WHERE token=%s",
		mysql.quote_sql_str(token)
	)
	CMD.query(sql)
	return true
end

function CMD.save_session(token, uid, username, ttl_seconds)
	ttl_seconds = ttl_seconds or 86400
	local sql = string.format(
		"REPLACE INTO auth_sessions (token, uid, username, expires_at) VALUES (%s, %d, %s, DATE_ADD(NOW(), INTERVAL %d SECOND))",
		mysql.quote_sql_str(token),
		uid,
		mysql.quote_sql_str(username),
		ttl_seconds
	)
	CMD.query(sql)
	return true
end

skynet.start(function()
	db = assert(mysql.connect({
		host = db_conf.host,
		port = db_conf.port,
		database = db_conf.database,
		user = db_conf.user,
		password = db_conf.password,
		max_packet_size = 1024 * 1024,
		on_connect = function(conn)
			conn:query("SET NAMES utf8mb4")
		end,
	}), "mysql connect failed")

	skynet.error("[db] connected to card_battle")

	skynet.dispatch("lua", function(_, _, cmd, ...)
		local f = assert(CMD[cmd], cmd)
		skynet.ret(skynet.pack(f(...)))
	end)
end)
