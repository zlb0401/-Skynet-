local crypt = require "skynet.crypt"

local M = {}

function M.hash(password)
	local salt = crypt.randomkey(8)
	local digest = crypt.hexencode(crypt.hashkey(salt .. password))
	return string.format("sha1:%s:%s", crypt.hexencode(salt), digest)
end

function M.verify(input, stored)
	if not stored or stored == "" then
		return false
	end

	if stored:sub(1, 5) == "sha1:" then
		local salt_hex, digest = stored:match("^sha1:([^:]+):(.+)$")
		if not salt_hex or not digest then
			return false
		end
		local salt = crypt.hexdecode(salt_hex)
		local computed = crypt.hexencode(crypt.hashkey(salt .. input))
		return computed == digest
	end

	-- Legacy demo accounts stored as plaintext.
	return input == stored
end

function M.validate_username(username)
	if not username or username == "" then
		return false, "username is empty"
	end
	if #username < 3 or #username > 16 then
		return false, "username length 3-16"
	end
	if not username:match("^[%w_]+$") then
		return false, "username only letters, digits, underscore"
	end
	return true
end

function M.validate_password(password)
	if not password or password == "" then
		return false, "password is empty"
	end
	if #password < 6 or #password > 32 then
		return false, "password length 6-32"
	end
	return true
end

return M
