#pragma once

#include <cstdint>
#include <string>

namespace password_util {

std::string hash(const std::string& password);
bool verify(const std::string& input, const std::string& stored);
bool validate_username(const std::string& username, std::string& err);
bool validate_password(const std::string& password, std::string& err);
std::string make_token(uint32_t uid, const std::string& username);

}  // namespace password_util
