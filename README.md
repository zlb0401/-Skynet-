# Skynet × Unity 回合制卡牌对战 Demo

校招作品：**1v1 回合制卡牌联机 Demo**  
技术栈：`Skynet (Lua)` + `Unity (C#)` + `MySQL` + 自定义 TCP 二进制协议  

仓库：[github.com/zlb0401/-Skynet-](https://github.com/zlb0401/-Skynet-)

开发中使用 **Cursor** 辅助协议对接与联调排错。

---

## 演示与完整包（网盘）

> GitHub 为**精简源码仓库**（已去掉主菜单视频、背景音乐、大型图标素材包，减小体积）。  
> **演示视频 / 完整工程 / Windows 试玩包**请走网盘（体积较大，适合下载游玩）。

| 内容 | 链接 |
|------|------|
| 演示视频 | [百度网盘](https://pan.baidu.com/s/1vFJx_wG6VvVFDsTvT4gb0A) 提取码：`kgi9` |
| 完整 Unity 工程 zip | （上传后把链接贴这里） |
| Windows 试玩包 | （上传后把链接贴这里） |

联机试玩需要云端 Skynet 在线（默认 TCP `8888`）。人机「开始游戏」可本地游玩。

测试账号：`test` / `123456`，`demo` / `123456`

---

## 功能

| 模块 | 说明 |
|------|------|
| 登录 / 匹配 | MySQL 账号；FIFO 双人匹配进房 |
| 联机对战 | 服务端权威：出牌、能量、护甲、伤害、胜负同步 |
| 人机模式 | 普通关 → 选牌奖励 → Boss 关 |
| 客户端 | 登录门禁、主菜单、手牌拖拽、伤害/能量反馈 |

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
├── server/                      # Skynet 业务代码
├── scripts/                     # start/stop
├── tools/                       # 协议测试脚本
├── docs/                        # 文档（含打包说明）
│   └── media/README.md          # 网盘链接填写处
└── client/CardBattle-Unity6/    # Unity 精简工程（无 Library）
```

Skynet 引擎请自行克隆编译：<https://github.com/cloudwu/skynet>

---

## 运行

### 服务端

```bash
# 编译 Skynet 后，按 scripts 与 server/config 部署并启动
./scripts/start.sh
```

### 客户端

1. Unity Hub 打开 `client/CardBattle-Unity6`
2. 打开 `MainMenu` 场景，配置 `ServerConfig` 的 Host/Port
3. Play → 登录 → 匹配 / 开始游戏

精简版缺少主菜单视频与 BGM，不影响联机逻辑与代码阅读。完整视听资源见网盘「完整工程」。

### 协议自测

```bash
python3 tools/test_client.py --host <IP> --port 8888 --user test --password 123456
```

---

## Windows 打包

见 [docs/UNITY_BUILD.md](docs/UNITY_BUILD.md)。打好的包请上传网盘，勿提交进 Git。

---

## 体积说明

| 类型 | 大约 | 是否在本仓库 |
|------|------|----------------|
| 脚本 / 场景 / Prefab | ~数 MB | 是 |
| 卡牌/角色图集等 | 偏大 | 是（玩法需要） |
| 主菜单视频 / BGM / 图标包 | 数十 MB | **否**（网盘） |
| Unity Library | 很大 | **否**（本地生成） |

---

## License

自研代码可按作品集展示使用；第三方插件/素材遵循原 License；Skynet 归属原项目。
