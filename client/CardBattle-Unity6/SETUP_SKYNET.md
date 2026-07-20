# CardBattle Unity6 + Skynet 客户端

基于 [UnityRoguelikeCardGame](https://github.com/DimitrisFuzi/UnityRoguelikeCardGame)，已接入 Skynet 网络层。

## Windows 下载

```powershell
scp root@YOUR_SERVER_IP:/root/CardBattle-Unity6.tar.gz D:\Projects\
cd D:\Projects
tar -xzf CardBattle-Unity6.tar.gz
```

## 打开项目

1. 关闭 Unity
2. Unity Hub → Open → `D:\Projects\CardBattle-Unity6`
3. 选 **Unity 6.3 LTS**，首次导入等 10～20 分钟
4. 若提示 TMP：Window → TextMesh Pro → Import TMP Essential Resources

## 单机试玩（不用联网）

1. 打开 `Assets/Scenes/MainMenu.unity`
2. Play → 点原有 **Start Game** 按钮
3. 或点右上角 Skynet 面板的 **Offline Play** 隐藏联网 UI

## 联网试玩（对接你的 Skynet 服务端）

1. 服务器先启动：`/opt/skynet-card-battle/scripts/start.sh`
2. MainMenu 场景 Play
3. 右上角 **Skynet Online** 面板：
   - 账号 `test` / `123456`（或 `demo` / `123456`）
   - 点 **Login** → 再点 **Match**（需两个客户端同时匹配）
4. 匹配成功自动进入 `Battle1` 战斗场景

## 场景流程

MainMenu → Battle1 → Reward1 → BattleBoss1 → Victory

## 网络配置

- 脚本：`Assets/Scripts/Network/`
- 地址：`Assets/Resources/Network/ServerConfig.asset`（YOUR_SERVER_IP:8888）
- 无需手动拖引用，启动时自动创建 `GameNetwork`

## 常见问题

| 问题 | 处理 |
|------|------|
| DOTween | Tools → Demigiant → DOTween Utility Panel → Setup |
| TMP 报错 | Import TMP Essential Resources |
| 匹配一直等 | 开第二个客户端或 PowerShell 测 `test_client.ps1` |
| 联网 UI 方块字 | 先 Import TMP Essential Resources |

## 1v1 联机战斗（已接入）

匹配成功后会进入 **Battle1**，并自动切换为服务端权威 1v1 UI（不再打本地狼怪）。

1. 启动服务端：`/opt/skynet-card-battle/scripts/start.sh`
2. 两个客户端分别用 `test/123456` 和 `demo/123456`
3. 登录 → 匹配 → 进入联机战斗
4. 点手牌出牌，点「结束回合」换手
5. 一方血量归零后显示胜负

卡牌：猛击 / 防御 / 重击 / 专注（服务端结算）
