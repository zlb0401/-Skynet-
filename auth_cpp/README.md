# C++ Auth Service

独立鉴权服务（注册 / 登录），与 Skynet 战斗服分离。

## 职责

| 服务 | 端口 | 语言 | 职责 |
|------|------|------|------|
| **auth_server** | **8889** | **C++** | 注册、登录、密码哈希、Session 写入 MySQL |
| Skynet gate | 8888 | Lua | Token 入场、匹配、对战 |

## 协议

与客户端共用二进制帧：`[uint16 body_len][uint16 msg_id][payload]`

- `C2S_RegisterReq=1006` / `S2C_RegisterResp=2006`
- `C2S_LoginReq=1001` / `S2C_LoginResp=2001`
- payload 与登录包相同（username + password）

成功后写入表 `auth_sessions`；Unity 再连 Skynet 发 `C2S_TokenLoginReq=1007`。

## 编译与启动

```bash
cd /opt/skynet-card-battle/auth_cpp
cmake -S . -B build && cmake --build build -j
bash scripts/start_auth.sh
# bash scripts/stop_auth.sh
```

环境变量可选：`AUTH_PORT` `DB_HOST` `DB_USER` `DB_PASSWORD` `DB_NAME`

## 依赖

- g++ / CMake
- OpenSSL (`libssl-dev`)
- MySQL client (`libmysqlclient-dev`)
