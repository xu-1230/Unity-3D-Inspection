# CODEBUDDY.md This file provides guidance to CodeBuddy when working with code in this repository.

## 仓库当前状态

本仓库处于**需求阶段**，除 `项目介绍.txt`（唯一的需求来源）外无任何代码、脚手架或配置。因此当前不存在可执行的 build / lint / test 命令。

在首次接入任务时，请先确认三端子项目是否已落地（Unity 场景、`backend`、`admin` 等目录），再决定使用下文的命令还是需要先搭建脚手架。不要假设某个 Go 框架或存储方案已被选定 —— 需求说明明确写为「不限」。

## 固定技术栈

| 部分 | 强制技术 | 职责 |
| --- | --- | --- |
| Unity 客户端 | Unity LTS + C# | 虚拟场景、射线点击、空间标记、问题上报 |
| 管理端 | React + TypeScript | 查看问题、修改问题状态 |
| 后端 | Go（框架不限） | 提供数据接口并持久化 |

持久化只允许 **SQLite 或本地 JSON 文件**（需求给定的两个选项），不要引入外部数据库服务。

## 常用命令

三端目录已就位，命令在各自目录内执行：

- **启动脚本**：根目录 `start-all.cmd` 拉起后端与前端，`start-backend.cmd`、`start-frontend.cmd`、`start-unity.cmd` 单独启动某一端；脚本会把 `D:\ctf\go\bin` 前置到 PATH。
- **Unity**：用 `start-unity.cmd` 或 Unity Hub 打开 `unity/`，菜单 `Tools > 生成巡检场景` 生成 `Assets/Scenes/Main.unity`。命令行跑测试前先确认是否安装了 Unity Test Framework。
- **后端**：Go 装在 `D:\ctf\go`（未加入系统 PATH，用 `D:\ctf\go\bin\go.exe`）。在 `backend/` 内 `go run ./cmd/server` 启动；`go vet ./...`；`go build ./...`；`go test ./...`，单测用 `go test ./internal/<pkg> -run TestName`。
- **管理端**：在 `frontend/` 内 `npm install` → `npm run dev`（5173 端口，`/api` 代理到 8080），`npm run build` 做类型检查并产出 `dist/`，`npm run preview` 预览产物。暂无 lint 与测试脚本，新增时以 `package.json` 中实际脚本为准。

## 架构（三端一条数据链路）

系统是「现场采集 → 后端持久化 → 桌面前端处置」的单向闭环，三端唯一共享的契约是**问题（Issue）实体**，而非任何代码。理解架构的关键是这条链路的职责切分：

1. **Unity 客户端只负责产生数据**。巡检人员在 3D 场景中通过射线（Raycast）拾取设备/区域网格，把命中点转换为场景坐标，在该坐标上实例化标记，并填写问题描述后上报。客户端不持有业务状态、不做状态流转，上报后即结束。
2. **Go 后端是唯一的真相来源**。它接收上报、生成问题 ID 与初始状态、持久化，并向管理端提供查询与状态更新接口。需求允许 SQLite 或 JSON 文件，意味着持久化层应当被隔离在一个小接口后（如 `Store`），使两种实现可互换 —— 不要在 handler 里直接写文件或拼 SQL。
3. **React 管理端只做读取与状态变更**。列表展示问题、查看位置信息、推进状态。它不参与坐标计算，也不应重复实现客户端的字段校验逻辑。

由此产生两条隐含约束，改动时容易踩坑：

- **坐标系是跨端语义边界**。Unity 使用左手系、Y 轴向上，单位与场景缩放由工程决定。后端应把坐标当作不透明数据原样存储（如 `{x, y, z, sceneId}`），不要在后端或管理端做坐标变换；需要可视化时，由上报方附带场景标识，展示层自行解释。
- **状态机属于后端**。问题状态（待处理 / 处理中 / 已解决之类）的合法迁移必须由后端校验，客户端的初始状态与管理端的可选操作都应视为对后端规则的投影，而不是各自实现一套。

## 目录约定

三端按目录平行放置，互不引用对方的源码：

- `unity/`：Unity 工程根（`Assets/Scripts|Scenes|Prefabs|Materials`、`Packages/`、`ProjectSettings/`），C# 脚本按职责拆分。
- `frontend/`：React + TypeScript 工程（`src/api`、`src/components`、`src/pages`、`src/types`），接口调用集中在 `src/api/client.ts`。
- `backend/`：Go 服务，`cmd/server` 为入口，`internal/api`（路由与校验）、`internal/model`（实体与枚举）、`internal/store`（持久化接口）三层分离，使 SQLite 与 JSON 文件实现可互换。

各目录的 `README.md` 记录该端用途与目录说明，新增子目录时同步更新。技术约束集中在 `AGENT-整体技术规约.md`（全局）与各端的 `AGENT-<端>整体技术规约.md`，README 与本节不重复其内容。

## 修改时的注意事项

对齐需求说明而非直觉：新增能力前先回到 `项目介绍.txt` 确认是否属于既定范围（如「无需登录鉴权」这类简化若未被要求，不要自行添加）。三端任一侧的字段增删都会破坏另外两侧，修改 Issue 实体的字段或状态枚举时，应在同一次改动中同步后端 model、Unity 上报结构与管理端类型定义。
