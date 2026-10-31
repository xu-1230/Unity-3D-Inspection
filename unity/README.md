# 虚拟巡检现场（Unity 客户端）

Unity 端承担两件事：**现场巡检**与**问题上报**。

- 进入 Play 后由代码实时搭建一座化工厂车间，第一人称沿巡检路线逐点检查设备参数，自动统计进度与异常并结算。
- 任何时候可以用准星对准设备**左键点击**，在该点留下标记、填写问题并提交到 Go 后端（`POST /api/issues`）。
- 另带一套 **AR 问题标记**分支（ARFoundation 平面/图像跟踪），用于手机端现场标记。

工程使用 **内置渲染管线 + 旧版 Input Manager + uGUI**，现场内容全部由代码生成，不依赖外部美术资源、预制体或第三方插件。

## 运行方式

仓库根目录的 `start-unity.cmd` 是入口（转调根目录 `Tools/start-inspection.ps1`）：

| 命令 | 作用 |
| --- | --- |
| `start-unity.cmd` | 构建（如需要）并运行 `unity/Build/UnityInspection.exe` |
| `start-unity.cmd build` | 强制重新构建并运行 |
| `start-unity.cmd editor` | 打开 Unity 编辑器 |
| `start-unity.cmd check` | 检查本机 Unity 版本、许可证、Windows 打包模块 |

用编辑器时打开 `Assets/Scenes/Main.unity` 直接 Play 即可——场景里只有相机、灯光与 EventSystem 占位对象
（运行时会关闭非自带相机与灯光），厂区几何全部由 `InspectionBootstrap` 在 `AfterSceneLoad` 阶段装配，
因此任意空场景按 Play 都能进入厂区。

构建入口：`Unity3DInspection.EditorTools.InspectionBuild.BuildFromCommandLine`
（菜单 `Tools/构建 Windows 运行包`），产物固定为 `Build/UnityInspection.exe`。

## 操作说明

| 操作 | 按键 |
| --- | --- |
| 移动 | `W` `A` `S` `D` |
| 奔跑 | `Shift` |
| 跳跃 | `空格` |
| 转向 | 鼠标 |
| 查看设备参数 | `F`（需靠近设备 3 米内） |
| **上报问题** | **鼠标左键点击设备或地面** |
| 关闭面板 / 释放鼠标 | `Esc` |

准星指向设备时设备会发光；靠近到 3 米内按 `F` 打开参数面板，可查看设备名称、所属区域、设备编号、
三项运行参数与巡检要点。参数面板只做查看，不记录、不统计完成状态，关闭后设备继续保持发光能力。

左键点击时以屏幕中心（准星）发出射线，命中表面后在该点生成红色标记并弹出上报表单：标题（必填）、
描述、优先级（低/中/高）、命中点世界坐标。提交成功要求后端返回 **201**，失败时表单保留并显示后端
`error.message`，可修正后重试；`Esc` 等同于取消，会立即销毁待提交标记。

启动时会 `GET /api/issues` 把已有问题还原成场景中的红色标记；准星指向标记会发光，左键点击打开问题详情
（标题、编号、优先级、描述、坐标），并即时请求后端核对最新状态（待处理 / 处理中 / 已解决）。

## 后端约定

上报地址由 `IssueApiClient.baseUrl` 决定，默认 `http://localhost:8080`：

```json
{
  "title": "P-101A 泵体渗油",
  "description": "机封处有油迹",
  "priority": "high",
  "location": { "x": 1.5, "y": 2.0, "z": -3.25 }
}
```

`priority` 取 `low` / `medium` / `high`；`id`、`status`、`createdAt` 由服务端生成。
坐标按 Unity 世界坐标原样上报，不做任何换算，由展示层自行解释。

## 目录结构

```
Assets/
  Editor/
    InspectionBuild.cs      # 构建入口 + Standard 着色器进包保护
    ARProjectSetup.cs       # 菜单生成 AR 场景、参考图库与 XR 配置
  Scenes/
    Main.unity              # 空场景，现场运行时装配
  Scripts/
    Core/        InspectionBootstrap / InspectionSiteRoot / SiteLayout
    World/       Shape / MaterialLibrary / ProceduralTextures / SiteGeometry
    Player/      FirstPersonPlayer
    Inspection/  InspectionDefinition / InspectionPoint / InspectionSystem
    UI/          UiFactory / HudController / MinimapRig
    Reporting/   IssueApiClient / IssueModels / IssueReportSystem   # 点击上报
    AR/          ARIssueApp / ARIssueUI / ARPlacementController / IssueMarker* / SiteAnchorTracker
```

## 注意事项

- `Library/`、`Build/`、`Logs/` 是自动生成的，可安全删除；删 `Library/` 后首次打开会重新导入（数分钟）。
- 工程使用旧版输入。若把 **Project Settings → Player → Active Input Handling** 改成
  “Input System Package (New)”，移动与点击会全部失效，需改回 “Input Manager (Old)” 或 “Both”。
- 现场材质在运行期用 `Shader.Find("Standard")` 创建，构建脚本会把它写入 Always Included Shaders；
  若手动用 Build Settings 打包，需自行确认这一点。
- `InspectionSiteRoot` 会关闭场景中已有的相机与灯光，统一使用自带的巡检相机与厂区照明。
- 中文界面依赖系统字体（优先“微软雅黑”），Windows 下默认可用。
- 后端起不来时，表单会停在「提交中…」直到超时（默认 10 秒），随后显示网络错误；本工程没有离线缓存。
