cmake_minimum_required(VERSION 3.16)
project(cardbattle_auth_cpp LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(OpenSSL REQUIRED)
find_library(MYSQLCLIENT_LIB mysqlclient REQUIRED)

add_executable(auth_server
    src/main.cpp
    src/auth_server.cpp
    src/db.cpp
    src/password.cpp
    src/protocol.cpp
    src/captcha.cpp
)

target_include_directories(auth_server PRIVATE include)
target_link_libraries(auth_server PRIVATE OpenSSL::Crypto ${MYSQLCLIENT_LIB} pthread)

install(TARGETS auth_server RUNTIME DESTINATION bin)
