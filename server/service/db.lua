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
