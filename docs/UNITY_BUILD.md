# Unity 导出 Windows 试玩包

## 环境

- Unity Hub + 与工程匹配的编辑器（建议 2022.3 LTS）
- 工程路径：`client/CardBattle-Unity6`

## 步骤

1. 用 Unity Hub **打开**工程，等导入结束  
2. 菜单 **File → Build Settings…**  
3. Platform 选 **PC, Mac & Linux Standalone**  
4. Target Platform：**Windows**，Architecture：**x86_64**  
5. **Scenes In Build** 按顺序加入（勾选）：
   - `Assets/Scenes/MainMenu.unity`
   - `Assets/Scenes/Battle1.unity`
   - `Assets/Scenes/Reward1.unity`
   - `Assets/Scenes/BattleBoss1.unity`
   - `Assets/Scenes/Victory.unity`
6. 点 **Player Settings**：
   - Company / Product Name 自定（如 `CardBattleDemo`）
   - Fullscreen Mode 可选 Windowed，方便演示
7. 点 **Build**，输出目录建议：`Builds/Windows/`  
8. 生成 `YourGame.exe` + `_Data` 文件夹，**整夹打包 zip** 即可分发

## 联机说明（务必写在 zip 说明里）

- 需要云服务器上 Skynet 已启动，安全组放行 TCP **8888**
- 默认账号：`test` / `123456`，`demo` / `123456`（两台机器各登一个才能匹配）
- 若服务器关闭，联机不可用；人机「开始游戏」仍可本地游玩

## 体积与上传

- Windows 包通常数百 MB，**不要**提交进 Git  
- 可上传到网盘，在 README 放下载链接  
- GitHub Release 也可挂 zip（注意单文件建议 < 2GB）

## 可选优化

- 正式展示可打 **Development Build** 关掉，减小包体  
- 去掉未用场景/大视频资源后再 Build，体积更友好
