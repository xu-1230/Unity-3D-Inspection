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

## 初始化方式

当前只提交了目录骨架与空占位文件，`Packages/`、`ProjectSettings/` 尚未生成。用 Unity Hub 以 LTS 版本新建 3D 工程后，把本目录的 `Assets` 内容迁入，或将工程生成物放到本目录下，即可作为独立工程使用。

生成物（`Library/`、`Logs/`、`Temp/`、`obj/`、`*.csproj`、`*.sln`）已被仓库根目录的 `.gitignore` 忽略，不需要手动处理。

## 开发约定

- 脚本放在 `Assets/Scripts/`，一个脚本一个职责，避免把拾取、表单与网络请求写在同一文件。
- 点击界面元素时不得触发射线检测，判定入口统一走 `EventSystem.current.IsPointerOverGameObject()`。
- 场景中同一时刻只允许存在一个待提交标记，标记对象不挂载碰撞体。
- 上报坐标为命中点的世界坐标，原样提交，不做单位换算。
- 接口地址与字段以 [需求规格说明书](../docs/需求规格说明书.md) 第 5 章为准。
