# CODEBUDDY.md This file provides guidance to CodeBuddy when working with code in this repository.

## 仓库当前状态

三端均已落地并打通：`backend/` 是可运行可测试的 Go 服务（SQLite 持久化），`frontend/` 是 React + Vite 管理端，`unity/` 是可批处理构建成 exe 的工程。Web 两件套由根目录 `compose.yaml` 编排，一键入口是 `start-docker.cmd`。

存储选型已定为 SQLite（纯 Go 驱动，无 cgo）。改动存储层时先看 `backend/internal/store/storetest`：它是一致性套件，任何实现都必须逐条通过，`internal/*/characterization_test.go` 则固定了 HTTP 与模型层的既有行为，改完必须全绿再继续。

## 固定技术栈

| 部分 | 强制技术 | 职责 |
| --- | --- | --- |
| Unity 客户端 | Unity LTS + C# | 虚拟场景、射线点击、空间标记、问题上报 |
| 管理端 | React + TypeScript | 查看问题、修改问题状态 |
| 后端 | Go（框架不限） | 提供数据接口并持久化 |

持久化只允许 **SQLite 或本地 JSON 文件**（需求给定的两个选项），不要引入外部数据库服务。

## 常用命令

三端目录已就位，命令在各自目录内执行：

- **启动脚本**：根目录只剩两个 `.cmd`（`start-docker.cmd` / `start-unity.cmd`），都是薄封装，逻辑在 `Tools/start-inspection.ps1`（UTF-8 BOM + CRLF，PS 5.1 才能正确解析中文）。动作为 `docker`（子命令 `up`/`web`/`build`/`down`/`logs`/`check`）、`unity`/`run`/`build`/`editor`/`package`、`check`；输出用 `[OK]` / `[!!]` / `[XX]` 分级，依赖缺失返回 2，运行失败返回 1。原生的 `docker` 会把进度写到 stderr，`Invoke-Compose` 里必须临时放宽 `$ErrorActionPreference`，否则脚本会误判为终止性错误。
- **编排**：根目录 `compose.yaml` 定义 `backend`（对外 8080，Unity 直连）与 `frontend`（对外 8088，Nginx 反代 `/api`），数据在命名卷 `unity-3d-inspection-data`。基础镜像与依赖源通过 `BASE_REGISTRY` / `GOPROXY` / `NPM_REGISTRY` 构建参数覆盖，默认值面向国内网络。
- **Unity**：`start-unity.cmd` 默认走「时间戳判断 → 批处理构建 → 运行 `unity/Build/UnityInspection.exe`」；`build` 强制重建，`editor` 打开编辑器。构建方法为 `Unity3DInspection.EditorTools.InspectionBuild.BuildFromCommandLine`，场景可用菜单 `Tools > 生成巡检场景` 重建。命令行跑测试前先确认是否安装了 Unity Test Framework。
- **后端**：Go 装在 `D:\ctf\go`（未加入系统 PATH，用 `D:\ctf\go\bin\go.exe`）。在 `backend/` 内 `go run ./cmd/server` 启动；`go vet ./...`；`go build ./...`；`go test ./...`，单测用 `go test ./internal/<pkg> -run TestName`。引入依赖时需要 `GOPROXY=https://goproxy.cn,direct`（`proxy.golang.org` 在本机不可达）。旧数据迁移用 `go run ./cmd/migrate -from data/issues.json -to data/issues.db`。
- **管理端**：在 `frontend/` 内 `npm install` → `npm run dev`（5173 端口，`/api` 代理到 8080），`npm run build` 做类型检查并产出 `dist/`，`npm run preview` 预览产物。暂无 lint 与测试脚本，新增时以 `package.json` 中实际脚本为准。

## 架构（三端一条数据链路）

系统是「现场采集 → 后端持久化 → 桌面前端处置」的单向闭环，三端唯一共享的契约是**问题（Issue）实体**，而非任何代码。理解架构的关键是这条链路的职责切分：

1. **Unity 客户端只负责产生数据**。巡检人员在 3D 场景中通过射线（Raycast）拾取设备/区域网格，把命中点转换为场景坐标，在该坐标上实例化标记，并填写问题描述后上报。客户端不持有业务状态、不做状态流转，上报后即结束。
2. **Go 后端是唯一的真相来源**。它接收上报、生成问题 ID 与初始状态、持久化，并向管理端提供查询与状态更新接口。持久化已经落到 SQLite，但仍隔离在 `store.Store` 接口之后：handler 不拼 SQL、不碰文件；换实现时只需让 `storetest` 一致性套件继续全绿。
3. **React 管理端只做读取与状态变更**。列表展示问题、查看位置信息、推进状态。它不参与坐标计算，也不应重复实现客户端的字段校验逻辑。

由此产生两条隐含约束，改动时容易踩坑：

- **坐标系是跨端语义边界**。Unity 使用左手系、Y 轴向上，单位与场景缩放由工程决定。后端应把坐标当作不透明数据原样存储（如 `{x, y, z, sceneId}`），不要在后端或管理端做坐标变换；需要可视化时，由上报方附带场景标识，展示层自行解释。
- **状态机属于后端**。问题状态（待处理 / 处理中 / 已解决之类）的合法迁移必须由后端校验，客户端的初始状态与管理端的可选操作都应视为对后端规则的投影，而不是各自实现一套。

## 目录约定

三端按目录平行放置，互不引用对方的源码：

- `unity/`：Unity 工程根（`Assets/Scripts|Scenes|Prefabs|Materials`、`Packages/`、`ProjectSettings/`），C# 脚本按职责拆分。
- `frontend/`：React + TypeScript 工程（`src/api`、`src/components`、`src/pages`、`src/types`），接口调用集中在 `src/api/client.ts`。
- `backend/`：Go 服务，`cmd/server` 为入口，`cmd/migrate` 为一次性迁移工具，`internal/api`（路由与校验）、`internal/model`（实体与枚举）、`internal/store`（`Store` 接口 + SQLite 实现 + `storetest` 一致性套件）、`internal/migrate` 分层分离。
- 根目录：`compose.yaml`（编排）、`start-docker.cmd` / `start-unity.cmd`（入口）、`Tools/start-inspection.ps1`（实际逻辑）、`.env.example`（可覆盖参数）、`release/`（exe 打包产物，已忽略）。

各目录的 `README.md` 记录该端用途与目录说明，新增子目录时同步更新。技术约束集中在 `AGENT-整体技术规约.md`（全局）与各端的 `AGENT-<端>整体技术规约.md`，README 与本节不重复其内容。

## 修改时的注意事项

对齐需求说明而非直觉：新增能力前先回到 `项目介绍.txt` 确认是否属于既定范围（如「无需登录鉴权」这类简化若未被要求，不要自行添加）。三端任一侧的字段增删都会破坏另外两侧，修改 Issue 实体的字段或状态枚举时，应在同一次改动中同步后端 model、Unity 上报结构与管理端类型定义。
