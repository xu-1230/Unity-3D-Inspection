# Unity-3D巡检

巡检人员在 Unity 虚拟现场中点击设备、标记问题位置并上报；管理人员在 React 网页端查看问题、更新处理状态。三端共用同一个 Go 后端与同一份数据。

| 部分 | 技术栈 | 职责 |
| --- | --- | --- |
| 巡检客户端 | Unity LTS + C# | 虚拟场景、视角移动、射线点击、空间标记、问题上报 |
| 网页管理端 | React + TypeScript | 查看问题列表与详情、修改处理状态 |
| 后端服务 | Go | 提供数据接口，持久化到 SQLite 文件 |

## 演示视频

[![播放演示视频](https://img.shields.io/badge/%E6%92%AD%E6%94%BE%E6%BC%94%E7%A4%BA%E8%A7%86%E9%A2%91-49.9_MB-2f6feb?style=for-the-badge)](https://xu-1230.github.io/Unity-3D-Inspection/demo/)

- 在线观看：<https://xu-1230.github.io/Unity-3D-Inspection/demo/>，由 GitHub Pages 承载的播放页，浏览器内直接播放，支持拖动进度与全屏（页面源码 `demo/index.html`）。
- 文件位置 `video/演示视频.mp4`（MP4 / H.264，约 49.9 MB），也可以从仓库文件页打开，或克隆后用任意播放器离线观看。
- 为什么 README 里没有直接播放器：GitHub 会过滤 `<video>` 与 `<iframe>`，仓库内的 mp4 无法内嵌进 Markdown，所以改用 Pages 页面承载播放器，保证点开就能看。若一定要让视频出现在 README 正文中，只能在 issue 或评论里拖拽上传，拿到 `https://github.com/user-attachments/assets/...` 地址后替换上方链接。

## 1 使用的版本

| 部分 | 版本 | 关键选型 |
| --- | --- | --- |
| Unity 客户端 | Unity `2022.3.62f3c1`（LTS） | 内置渲染管线、旧版 Input Manager、uGUI；现场几何全部代码生成 |
| 网页管理端 | React `19.0` + TypeScript `5.7` + Vite `6` | 构建用 Node `22`，产物由 Nginx `1.27` 托管 |
| 后端服务 | Go `1.23` | 仅一个外部依赖 `modernc.org/sqlite v1.34.5`（纯 Go，无 cgo） |
| 编排 | Docker Compose（`compose.yaml`） | 运行镜像 `alpine:3.20` / `nginx:1.27-alpine` |

## 2 三个部分如何启动

| 部分 | 启动方式 | 地址 |
| --- | --- | --- |
| 后端 | `cd backend` → `go run ./cmd/server` | <http://localhost:8080>（`PORT`、`DATA_PATH` 可覆盖） |
| 管理端（开发） | `cd frontend` → `npm install` → `npm run dev` | <http://localhost:5173>，`/api` 代理到 8080 |
| 管理端（生产） | 根目录 `start-docker.cmd`（或 `docker compose up -d --build`） | <http://localhost:8088>，Nginx 反代 `/api` |
| Unity 客户端 | 根目录 `start-unity.cmd`（必要时构建并运行 `unity/Build/UnityInspection.exe`） | exe，直连 8080 |

## 3 已完成、未完成和已知问题

### 3.1 Unity 客户端

| # | 要求 | 状态 | 实现说明 |
| --- | --- | --- | --- |
| 1 | 用基础对象搭建虚拟现场（地面与墙面、机柜/设备/管线、至少两个可点击对象） | 已完成 | 运行时用立方体/圆柱体拼出 40×28 m 厂房：地面、四面墙、泵组、储罐、输送带、配电柜、控制台、管廊；带碰撞体、可点击的对象远超两个 |
| 2 | 移动并转动视角 | 已完成 | `W/A/S/D` 移动、`Shift` 奔跑、`空格` 跳跃，鼠标转向，俯仰限制在 ±85°，四面墙挡住越界 |
| 3 | 射线检测点击设备或场景表面 | 已完成 | 以相机为准星发射 `Physics.Raycast`，仅命中碰撞体时算有效点击，未命中不产生任何界面变化 |
| 4 | 在实际命中位置放置问题标记 | 已完成 | 标记直接取 `hit.point` 世界坐标生成，颜色与尺寸按优先级区分；待提交标记会移除碰撞体，避免遮挡射线，已保存的标记保留碰撞体供点击查看详情 |
| 5 | 填写并提交问题（标题必填、描述可选、优先级三档、位置为 x/y/z） | 已完成 | 表单校验标题去空白后非空，描述可空，优先级 `low`/`medium`/`high` 默认 `medium`，坐标只读展示 |
| 6 | 提交成功或失败给出明确提示 | 已完成 | `201` 显示编号、优先级与坐标并在 2.5 秒后关闭；失败显示后端 `error.message`，保留表单与标记可重试 |
| 7 | 点击按钮或输入框时不误放标记 | 已完成 | 放置前用 `EventSystem.current.IsPointerOverGameObject()` 判定，指针在任意 UI 元素上时直接返回 |
| 8 | 一次有效点击最多一个待提交标记，取消后移除 | 已完成 | 同一时刻只保留一个待提交标记，再次有效点击替换旧的；取消或关闭表单立即销毁未保存标记且不请求后端 |

### 3.2 React 管理端

| # | 要求 | 状态 | 实现说明 |
| --- | --- | --- | --- |
| 1 | 从后端获取问题列表 | 已完成 | 三个视图共用一次 `GET /api/issues`，顶栏可手动刷新 |
| 2 | 至少显示标题、优先级、状态 | 已完成 | 列表同时展示标题、优先级、状态、区域、设备编号与坐标，空字段显示占位文案 |
| 3 | 能改状态为 open / in_progress / resolved | 已完成 | 详情抽屉内三态切换是唯一入口，成功以后端返回值覆盖，失败回退并提示 |
| 4 | 后端不可用时显示错误提示、不白屏 | 已完成 | 显示「加载失败」+ 重试并给出接口地址，`ErrorBoundary` 兜住渲染异常 |

### 3.3 Go 后端

| # | 要求 | 状态 | 实现说明 |
| --- | --- | --- | --- |
| 1 | 服务运行状态检查 | 已完成 | `GET /api/health` 返回 `{"status":"ok"}`，不依赖存储 |
| 2 | 接收并保存 Unity 上报的问题 | 已完成 | `POST /api/issues` 校验后落库，返回 `201` 与完整对象，写入失败返回 `INTERNAL` |
| 3 | 返回已保存的问题列表 | 已完成 | `GET /api/issues` 按 `createdAt` 倒序返回全部记录，无数据为 `[]` |
| 4 | 按问题 ID 修改处理状态 | 已完成 | `PATCH /api/issues/{id}/status` 更新 `status` 与 `updatedAt`，ID 不存在返回 `404 ISSUE_NOT_FOUND` |
| 5 | 生成唯一 ID、默认状态 open | 已完成 | `crypto/rand` 生成 32 位十六进制标识，创建时固定 `open` 并写入两个时间字段；客户端自带的服务端字段一律忽略 |
| 6 | 标题为空或枚举值错误时返回明确参数错误 | 已完成 | 统一错误结构，覆盖 `INVALID_JSON`/`INVALID_TITLE`/`INVALID_PRIORITY`/`INVALID_STATUS`/`INVALID_LOCATION`，按 JSON→标题→优先级→坐标→状态 顺序返回首个错误 |
| 7 | 数据在服务重启后仍然存在 | 已完成 | SQLite 文件持久化（容器内 `/data/issues.db`，宿主机 `backend/data/issues.db`），启动时加载已有数据 |
| 8 | Unity 与 React 使用同一个后端和同一份数据 | 已完成 | 单实例单文件存储，两端读写同一份数据，无中间缓存或按端隔离 |
| 9 | React 在浏览器中能正常访问后端 | 已完成 | 放行本机任意端口的 CORS 预检与 `GET/POST/PATCH/DELETE`，生产形态由 Nginx 同源反代 `/api` |

### 未完成（含既定范围外）

- 无 CI 流水线；

### 已知问题

- 客户端缓存：后端不可用时表单会停在「提交中…」直到 10 秒超时，之后显示网络错误，需手动重试。


## 4 后端地址和接口设计

基址 `http://localhost:8080`，全部业务接口带 `/api` 前缀，请求响应均为 UTF-8 JSON，时间字段为 RFC3339 UTC。

| 方法 | 路径 | 用途 | 成功响应 |
| --- | --- | --- | --- |
| GET | `/api/health` | 服务状态检查 | 200 `{"status":"ok"}` |
| POST | `/api/issues` | 上报并保存问题 | 201，完整问题对象 |
| GET | `/api/issues` | 返回全部问题（按 `createdAt` 倒序） | 200，数组，无数据为 `[]` |
| PATCH | `/api/issues/{id}/status` | 修改处理状态 | 200，更新后的对象 |
| DELETE | `/api/issues/{id}` | 删除问题 | 204，无响应体 |

设计要点：

- **错误统一**为 `{"error":{"code","message","field"}}`，错误码 `INVALID_JSON` / `INVALID_TITLE` / `INVALID_PRIORITY` / `INVALID_STATUS` / `INVALID_LOCATION` / `ISSUE_NOT_FOUND` / `INTERNAL`，校验顺序为 JSON → 标题 → 优先级 → 坐标 → 状态。
- **枚举**：优先级 `low` / `medium` / `high`；状态 `open` / `in_progress` / `resolved`，创建时固定 `open`，`id` 与时间字段由服务端生成，客户端自带的服务端字段一律忽略。
- **状态机归后端**：九种迁移全部合法（含目标=当前值的幂等情形），只校验取值是否合法；每次变更刷新 `updatedAt`。
- **坐标视为不透明数据**：`location` 为 `{x,y,z}` 对象，后端与管理端只存取透传，不做单位换算或轴向变换。
- **跨域**：允许 `localhost` / `127.0.0.1` 任意端口的 `GET/POST/PATCH/DELETE/OPTIONS`，`Content-Type` 请求头，预检返回 2xx；生产形态由 Nginx 同源反代，不触发跨域。
- **两端接入**：Unity 走 exe 同目录的 `backend-config.json`；管理端开发用 `VITE_API_BASE_URL`（留空走 Vite 代理），生产走 `/api` 相对路径。

完整契约见[需求规格说明书](docs/需求规格说明书.md)第 5、6 章。

## 5 三个 AR 基础问题

**（1）改成真实 AR，平面检测和点击放置需要 AR Foundation 的哪些能力？**

`ARSession`（会话生命周期）+ `XROrigin`/`ARCameraManager`/`ARCameraBackground`（相机画面与虚拟内容叠加）+ `ARPlaneManager`（检测水平/垂直平面）+ `ARRaycastManager`（屏幕点击射线打到已检测平面，取 `TrackableType.PlaneWithinPolygon` 的 `Pose` 作为放置点，同时驱动准星预览）。`ARAnchorManager` 可选但推荐，用于把标记挂到 anchor 上减少漂移；若要跨启动还原，还需 `ARTrackedImageManager` + `XRReferenceImageLibrary`，并在 XR Plug-in Management 启用 ARCore / ARKit 加载器。

**（2）为什么只保存 x/y/z，通常不能保证下次启动对应同一个真实位置？**

AR 的世界原点是设备启动时由跟踪系统自建的，每次启动、每台设备、每次重定位都会重置，跟踪本身又只是靠视觉特征与 IMU 推算的相对量，环境变化还会触发重定位。因此同一组 x/y/z 在下一次会话里对应的真实位置并不相同。做法是保存成**相对稳定参照物**的局部坐标（现场基准图、二维码、持久化 anchor 等），启动后重新识别参照物再换算回世界坐标。

**（3）AR 初始化失败、设备不支持或 Tracking 异常时如何提示？**

原则是状态常驻可见、分级处理、恢复后自动收起，不靠一次性弹窗：

- 用 `ARSession.state` 判定：`Unsupported` 明确告知不支持并给出替代方案（换设备或退回虚拟场景流程），同时禁用放置入口；`NeedsInstall` 引导安装/更新 ARCore 服务；`SessionInitializing` 只显示「正在初始化，请缓慢移动设备」，不报错。
- 跟踪受限（`TrackingState.Limited`）时显示常驻横幅提示可能原因（光照不足、特征点少、镜头遮挡），并暂停放置与保存，避免写出错误坐标。
- 相机权限被拒、未识别到基准图等情况单独给文案；未定位时禁止提交，定位成功后自动恢复操作。

## 6 文档

- [需求规格说明书](docs/需求规格说明书.md)：功能需求、接口契约、数据模型与状态机
- [部署与打包说明](docs/部署与打包说明.md)：Compose 编排、数据卷、迁移与 exe 打包
- [AGENT-整体技术规约.md](AGENT-整体技术规约.md)：全局技术约束与三端规约索引
- 各端说明：[unity](unity/README.md) / [frontend](frontend/README.md) / [backend](backend/README.md)
- 演示视频：[在线播放页](https://xu-1230.github.io/Unity-3D-Inspection/demo/)（源码 `demo/index.html`，视频文件 `video/演示视频.mp4`）
