# Skynet × Unity 回合制卡牌对战 Demo

校招作品：**1v1 回合制卡牌联机 Demo**  
技术栈：`Skynet (Lua)` + `Unity (C#)` + `MySQL` + 自定义 TCP 二进制协议  

仓库：[github.com/zlb0401/-Skynet-](https://github.com/zlb0401/-Skynet-)

开发中使用 **Cursor** 辅助协议对接与联调排错。

---

## 演示与下载（网盘）

> GitHub 为**精简源码仓库**（已去掉主菜单视频、背景音乐、大型图标素材包，减小体积）。  
> **演示视频 / Windows 试玩包**请走网盘（体积较大，适合直接下载游玩）。

| 内容 | 链接 |
|------|------|
| 演示视频 | [百度网盘](https://pan.baidu.com/s/1vFJx_wG6VvVFDsTvT4gb0A) 提取码：`kgi9` |
| Windows 试玩包 | [百度网盘](https://pan.baidu.com/s/1wF2KuajumsKwADf31T_41A) 提取码：`pp34`（`Builds.zip`） |
| 完整 Unity 工程 zip | （后续补充） |

测试账号（联机用）：`test` / `123456`，`demo` / `123456`

---

## 怎么玩（推荐：直接下试玩包）

1. 打开网盘链接，下载 **`Builds.zip`**，解压到任意目录  
2. 进入解压目录，双击 **与项目同名的 `.exe`**（不要点 `UnityCrashHandler64.exe`）  
3. 等待进入主菜单：
   - **开始游戏**：人机模式，**无需服务器**，本机即可玩  
   - **匹配对战**：联机 1v1，需要服务端在线（见下方「服务端：可选」）  
4. 联机时请使用上面的测试账号；两台机器（或两个客户端）各登一个号后再点「匹配对战」

> 若联机连不上：多半是服务端未启动，或客户端 `ServerConfig` 未指向可用服务器。人机模式不受影响。

---

## 服务端：可选

本仓库的 **Skynet 服务端不是必须部署** 才能体验全部内容：

| 玩法 | 是否需要服务端 |
|------|----------------|
| 人机「开始游戏」 | **不需要** |
| 联机「匹配对战」 | **需要** |

两种用法都可以：

1. **看演示 / 试玩联机**  
   博主可在自己的云服务器上运行 Skynet；试玩包在服务在线期间可联机匹配。  
2. **自己部署**  
   也可克隆本仓库，按下方流程在**自己的服务器 / 本机**编译 Skynet、配置 MySQL 后启动，并把客户端 `ServerConfig` 的 Host 改成你的 IP。

默认端口：**TCP `8888`**（云服务器请放行安全组）。

---

## 操作流程（从零到可玩）

### A. 只玩人机（最快）

1. 下载 [Windows 试玩包](https://pan.baidu.com/s/1wF2KuajumsKwADf31T_41A)（提取码 `pp34`）  
2. 解压 → 运行同名 `.exe` → 登录后点 **开始游戏**

### B. 玩联机（依赖服务端）

1. 确认 Skynet 已在目标机器启动（博主云服，或你自己按「服务端启动」部署）  
2. 试玩包或 Unity 工程里 `ServerConfig` 指向该服务器 IP，端口 `8888`  
3. 客户端 A 登录 `test`，客户端 B 登录 `demo`（密码均为 `123456`）  
4. 双方点 **匹配对战** → 进入房间对战

### C. 用源码跑 Unity 客户端（开发 / 阅读代码）

1. Unity Hub 打开本仓库 `client/CardBattle-Unity6`（精简版无主菜单视频/BGM）  
2. 打开 `MainMenu` 场景  
3. 配置 `Assets/Resources/Network/ServerConfig` 的 Host / Port  
4. Play → 登录 → 匹配 / 开始游戏  

完整视听资源体积较大，不进 Git；有需要时再下网盘「完整工程」。

### D. 自己部署服务端（可选）

```bash
# 1. 自行克隆并编译 Skynet 引擎
#    https://github.com/cloudwu/skynet

# 2. 配置 MySQL（账号库），按 server/lualib/db_conf.lua 填写
#    （公开仓库中密码为占位符 CHANGE_ME，请改成你自己的）

# 3. 按 scripts 与 server/config 部署后启动
./scripts/start.sh
```

协议自测：

```bash
python3 tools/test_client.py --host <IP> --port 8888 --user test --password 123456
```

Windows 从源码重新打包：见 [docs/UNITY_BUILD.md](docs/UNITY_BUILD.md)。打好的包请放网盘，勿提交进 Git。

---

## 当前功能

| 模块 | 说明 |
|------|------|
| 登录 / 匹配 | MySQL 账号；FIFO 双人匹配进房 |
| 联机对战 | 服务端权威：出牌、能量、护甲、伤害、胜负同步 |
| 人机模式 | 普通关 → 选牌奖励 → Boss 关 |
| 客户端 | 登录门禁、主菜单、手牌拖拽、伤害/能量反馈 |

---

## 后期期望（Roadmap）

当前版本侧重 **联机协议 + 回合制对战骨架**。后续计划往轻量肉鸽推进，例如：

- **通关掉落**：每局/每关胜利后获得物品或资源，用于养成  
- **卡牌升级**：用掉落资源强化已有卡牌（伤害、费用、效果等）  
- **卡槽构筑**：可调整自己的卡组槽位与上阵卡牌  
- **宝箱解锁**：探索/战斗后开宝箱，解锁新卡牌  
- **稀有度分级**：为卡牌增加清晰的品质分级（如普通 / 稀有 / 史诗等），方便构筑与成长反馈  

以上为规划方向，尚未全部实装；欢迎关注后续更新。

---

## 架构

```
Unity 客户端  --TCP:8888-->  Skynet gate
                              ├─ login (MySQL)
                              ├─ match (FIFO)
                              └─ room + agent（权威战斗）
```

协议：`[uint16 body_len][uint16 msg_id][payload...]`（大端），服务端处理粘包。

---

## 目录

```
├── server/                      # Skynet 业务代码（可选部署）
├── scripts/                     # start/stop
├── tools/                       # 协议测试脚本
├── docs/                        # 文档（含打包说明）
│   └── media/README.md          # 网盘链接
└── client/CardBattle-Unity6/    # Unity 精简工程（无 Library）
```

Skynet 引擎请自行克隆编译：<https://github.com/cloudwu/skynet>

---

## 体积说明

| 类型 | 大约 | 是否在本仓库 |
|------|------|----------------|
| 脚本 / 场景 / Prefab | ~数 MB | 是 |
| 卡牌/角色图集等 | 偏大 | 是（玩法需要） |
| 主菜单视频 / BGM / 图标包 | 数十 MB | **否**（网盘） |
| Windows 试玩包 | 数百 MB | **否**（网盘） |
| Unity Library | 很大 | **否**（本地生成） |

---

## License

自研代码可按作品集展示使用；第三方插件/素材遵循原 License；Skynet 归属原项目。
