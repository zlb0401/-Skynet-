# 卡牌对战（Skynet + C++ Auth + Unity）

本仓库为当前可运行基线：**登录 / 卡牌 / 卡组 / 抽卡 / 宝箱 / 背包** 等系统功能已打通；**战斗内应用升级数值等后续功能尚未全部接入**。

## 架构

| 组件 | 端口 | 说明 |
|------|------|------|
| **C++ Auth** (`auth_cpp/`) | **8889** | 注册、登录、钱包、卡组、升级、抽卡、开箱 |
| **Skynet Gate** (`server/`) | **8888** | Token 入场、匹配、对战 |
| **Unity 客户端** (`client/CardBattle-Unity6/`) | — | 主菜单 UI + 网络 |

流程：`Unity → Auth(8889) 登录拿 Token → Skynet(8888) TokenLogin → 进游戏`

## 目录

```
auth_cpp/                 # C++ 鉴权与经营逻辑
server/                   # Skynet Lua 服务
client/CardBattle-Unity6/ # Unity 6 工程（不含 Library）
scripts/                  # 启停与建库脚本
docs/                     # 文档
```

Skynet 引擎本体不在本仓库内（见 `.gitignore` 中的 `skynet/`），需自行放置或软链到服务器目录。

## 配置（重要）

仓库内数据库密码已脱敏为 `CHANGE_ME`，部署前请修改：

客户端默认地址为 `127.0.0.1`（见 `ServerConfig`）。连远程服务器时请在本地改 Auth/Gate 主机，**不要把公网 IP 再提交回仓库**。

- `server/lualib/db_conf.lua`
- `auth_cpp/include/config.h`（或用环境变量 `DB_PASSWORD` 等覆盖）

## 服务端启动（示例）

```bash
# 1) MySQL 建库（按需执行 scripts/init_db.sql）
# 2) 编译并启动 Auth
cd auth_cpp
cmake -S . -B build && cmake --build build -j
bash scripts/start_auth.sh   # 监听 8889

# 3) 启动 Skynet（需本机已有 skynet 引擎与 config）
bash scripts/start.sh        # 监听 8888
```

## Unity 客户端

1. 用 Unity 6 打开 `client/CardBattle-Unity6`
2. 确认 `Assets/Resources/Network/ServerConfig` 中 Auth=`8889`、Gate=`8888`
3. 首次导入后若卡面空白，对 `CardSprites` / `Cards/Art` 执行 Reimport

## 当前进度（本版）

已完成（大厅 / 经营侧）：

- 账号登录注册（C++ Auth）
- 卡牌列表 / 升级 / 筛选下拉
- 卡组编辑（等级决定可带张数，默认 1 张）
- 日常卡池 + 宝箱池抽卡（十连展示、跳过）
- 背包开箱（数量选择、进度条、结果弹窗）
- 卡面统一竖版、中文描述占位符替换

待完成：

- 将升级等级等数值**完整应用到战斗**
- 其它战斗内表现与平衡

## License

见 `client/CardBattle-Unity6/LICENSE`。
