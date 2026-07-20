# 1v1 战斗状态同步（MVP）

## 协议新增

| ID | 方向 | 说明 |
|----|------|------|
| 1003 | C2S | PlayCard：`u8 hand_index`（从 0 起） |
| 1004 | C2S | EndTurn |
| 1005 | C2S | BattleReady（进入战斗场景后发送） |
| 2003 | S2C | BattleStart（首包完整状态） |
| 2004 | S2C | BattleState（之后每次操作同步） |
| 2005 | S2C | BattleEnd：`u32 winner_uid` + `str8 message` |

## 卡牌（服务端权威）

| ID | 名称 | 费用 | 效果 |
|----|------|------|------|
| 1 | 猛击 | 1 | 伤害 6 |
| 2 | 防御 | 1 | 护甲 6 |
| 3 | 重击 | 2 | 伤害 10 |
| 4 | 专注 | 0 | +1 能量，抽 1 |

初始：50 血 / 3 能量；每回合抽 5 张；结束回合清空护甲并换手。

## 联机怎么测

1. 服务器已跑：`/opt/skynet-card-battle/scripts/start.sh`
2. 两个客户端（Unity + Build，或两台机器）
3. `test/123456` 与 `demo/123456` 分别登录 → 匹配
4. 匹配成功进入 Battle1，出现联机战斗 UI（不是打狼）
5. 轮流出牌 / 结束回合，双方状态同步

服务端自测：`python3 /opt/skynet-card-battle/tools/test_battle_1v1.py`
