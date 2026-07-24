#pragma once

#include "config.h"
#include "db.h"

class AuthServer {
public:
    explicit AuthServer(AuthConfig cfg);
    int run();

private:
    AuthConfig cfg_;
    Database db_;

    void handle_client(int client_fd);
};
