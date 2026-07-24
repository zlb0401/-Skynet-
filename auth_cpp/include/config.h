#pragma once

#include <string>

struct AuthConfig {
    std::string listen_host = "0.0.0.0";
    int listen_port = 8889;

    std::string db_host = "127.0.0.1";
    int db_port = 3306;
    std::string db_name = "card_battle";
    std::string db_user = "card";
    std::string db_password = "CHANGE_ME";

    int session_ttl_seconds = 86400;
};
