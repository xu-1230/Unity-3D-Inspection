# unity — Unity 巡检客户端

存放 Unity 客户端的工程资源与脚本。该目录最终应是一个可直接被 Unity Hub 打开的工程根目录。

## 用途

用 Unity 基础几何对象搭建虚拟巡检现场，支持视角移动、射线点击、空间标记与问题上报。技术栈固定为 Unity LTS + C#。

## 目录说明

| 路径 | 用途 |
| --- | --- |
| `Assets/Scripts/` | C# 脚本，按职责拆分（视角控制、射线拾取与标记、表单与上报） |
| `Assets/Scenes/` | 场景文件，主场景约定命名为 `Main.unity` |
| `Assets/Prefabs/` | 预制体，问题标记等可复用对象 |
| `Assets/Materials/` | 材质资源 |
| `Packages/` | 包依赖清单 `manifest.json`，由 Unity 生成 |
| `ProjectSettings/` | 工程设置，由 Unity 生成 |

## 打开工程

本目录已是完整工程根，含 `ProjectSettings/ProjectVersion.txt` 与 `Packages/manifest.json`。用根目录的 `start-unity.cmd` 或 Unity Hub 打开 `Assets/Scenes/Main.unity`，按 Play 即可点击设备上报。

场景已随仓库提供；需要重建时执行菜单 `Tools > 生成巡检场景`，会覆盖保存 `Assets/Scenes/Main.unity`。

工程版本记录为 `2022.3.62f3c1`，与当前开发机的编辑器一致；换用其他 LTS 版本时 Hub 会提示升级，接受即可。

首次打开后 Unity 会生成 `.meta` 与 `Library/`：`.meta` 属于工程资源需要提交，`Library/`、`Logs/`、`Temp/`、`obj/`、`*.csproj`、`*.sln` 已被根目录 `.gitignore` 忽略。

## 技术规约

场景构件、脚本分层、输入隔离、标记生命周期与上报规则见 [AGENT-unity整体技术规约.md](AGENT-unity整体技术规约.md)；跨端约束见 [AGENT-整体技术规约.md](../AGENT-整体技术规约.md)。
