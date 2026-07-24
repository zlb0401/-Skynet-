#include "password.h"

#include <openssl/evp.h>
#include <openssl/rand.h>
#include <openssl/sha.h>

#include <cctype>
#include <cstdio>
#include <iomanip>
#include <sstream>
#include <vector>

namespace password_util {
namespace {

std::string to_hex(const unsigned char* data, size_t n) {
    std::ostringstream oss;
    oss << std::hex << std::setfill('0');
    for (size_t i = 0; i < n; ++i) {
        oss << std::setw(2) << static_cast<int>(data[i]);
    }
    return oss.str();
}

bool from_hex(const std::string& hex, std::vector<unsigned char>& out) {
    if (hex.size() % 2 != 0) {
        return false;
    }
    out.clear();
    out.reserve(hex.size() / 2);
    for (size_t i = 0; i < hex.size(); i += 2) {
        unsigned int v = 0;
        if (std::sscanf(hex.c_str() + i, "%02x", &v) != 1) {
            return false;
        }
        out.push_back(static_cast<unsigned char>(v));
    }
    return true;
}

std::string sha1_hex(const std::string& data) {
    unsigned char digest[SHA_DIGEST_LENGTH];
    SHA1(reinterpret_cast<const unsigned char*>(data.data()), data.size(), digest);
    return to_hex(digest, SHA_DIGEST_LENGTH);
}

}  // namespace

std::string hash(const std::string& password) {
    unsigned char salt[8];
    RAND_bytes(salt, sizeof(salt));
    const std::string salt_hex = to_hex(salt, sizeof(salt));
    const std::string mixed(reinterpret_cast<const char*>(salt), sizeof(salt));
    const std::string digest = sha1_hex(mixed + password);
    return "sha1:" + salt_hex + ":" + digest;
}

bool verify(const std::string& input, const std::string& stored) {
    if (stored.rfind("sha1:", 0) == 0) {
        const size_t p1 = stored.find(':', 5);
        if (p1 == std::string::npos) {
            return false;
        }
        const std::string salt_hex = stored.substr(5, p1 - 5);
        const std::string digest = stored.substr(p1 + 1);
        std::vector<unsigned char> salt;
        if (!from_hex(salt_hex, salt)) {
            return false;
        }
        const std::string mixed(reinterpret_cast<const char*>(salt.data()), salt.size());
        return sha1_hex(mixed + input) == digest;
    }
    // Legacy plaintext demo accounts.
    return input == stored;
}

bool validate_username(const std::string& username, std::string& err) {
    if (username.empty()) {
        err = "username is empty";
        return false;
    }
    if (username.size() < 3 || username.size() > 16) {
        err = "username length 3-16";
        return false;
    }
    for (unsigned char c : username) {
        if (!(std::isalnum(c) || c == '_')) {
            err = "username only letters, digits, underscore";
            return false;
        }
    }
    return true;
}

bool validate_password(const std::string& password, std::string& err) {
    if (password.empty()) {
        err = "password is empty";
        return false;
    }
    if (password.size() < 6 || password.size() > 32) {
        err = "password length 6-32";
        return false;
    }
    return true;
}

std::string make_token(uint32_t uid, const std::string& username) {
    unsigned char rnd[8];
    RAND_bytes(rnd, sizeof(rnd));
    std::ostringstream raw;
    raw << uid << ':' << username << ':' << to_hex(rnd, sizeof(rnd));
    return sha1_hex(raw.str()).substr(0, 32);
}

}  // namespace password_util
