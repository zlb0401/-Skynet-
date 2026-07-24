#include "auth_server.h"

#include <cstdlib>
#include <iostream>

int main() {
    AuthConfig cfg;
    if (const char* p = std::getenv("AUTH_PORT")) {
        cfg.listen_port = std::atoi(p);
    }
    if (const char* p = std::getenv("DB_HOST")) {
        cfg.db_host = p;
    }
    if (const char* p = std::getenv("DB_USER")) {
        cfg.db_user = p;
    }
    if (const char* p = std::getenv("DB_PASSWORD")) {
        cfg.db_password = p;
    }
    if (const char* p = std::getenv("DB_NAME")) {
        cfg.db_name = p;
    }

    std::cout << "Skynet CardBattle — C++ Auth Service\n";
    AuthServer server(std::move(cfg));
    return server.run();
}
