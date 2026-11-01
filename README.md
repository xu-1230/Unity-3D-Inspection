# Unity-3D巡检

基于 Unity 的 3D 巡检项目：巡检人员在虚拟现场标记问题位置并上报，管理人员在网页端查看并处置。

## 系统组成

| 部分 | 技术栈 | 职责 |
| --- | --- | --- |
| 巡检客户端 | Unity LTS + C# | 虚拟场景、视角移动、射线点击、空间标记、问题上报 |
| 网页管理端 | React + TypeScript | 查看问题列表，修改处理状态 |
| 后端服务 | Go | 提供数据接口，持久化保存到 SQLite 文件 |

三端共用同一个后端实例与同一份数据。

## 业务流程

1. 巡检人员在客户端场景中移动、转动视角，点击设备或场景表面。
2. 客户端在射线命中位置放置问题标记，并弹出表单填写标题、描述与优先级。
3. 提交后端后生成唯一标识，状态默认为 `open`。
4. 管理人员在管理端查看列表，将状态更新为 `in_progress` 或 `resolved`。

## 接口一览

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/health` | 服务运行状态检查 |
| POST | `/api/issues` | 上报并保存问题 |
| GET | `/api/issues` | 获取问题列表 |
| PATCH | `/api/issues/{id}/status` | 修改问题状态 |
| DELETE | `/api/issues/{id}` | 删除问题记录 |

请求与响应均为 JSON，错误统一为 `{"error":{"code","message","field"}}`。

## 文档

- [需求规格说明书](docs/需求规格说明书.md)：功能需求、接口契约、数据模型与状态机
- [部署与打包说明](docs/部署与打包说明.md)：Docker Compose 编排、数据卷、迁移与 exe 打包
- [AGENT-整体技术规约.md](AGENT-整体技术规约.md)：全局技术约束与三端规约索引
- [unity 技术规约](unity/AGENT-unity整体技术规约.md)、[frontend 技术规约](frontend/AGENT-frontend整体技术规约.md)、[backend 技术规约](backend/AGENT-backend整体技术规约.md)：各端的实现约束与验证清单
- [CODEBUDDY.md](CODEBUDDY.md)：仓库开发约定

## 目录结构

```
.
├── unity/                # Unity 客户端：存虚拟场景、C# 脚本、预制体与工程设置
│   ├── Assets/           # Scripts / Scenes / Prefabs / Materials
│   ├── Packages/         # 包依赖清单（由 Unity 生成）
│   └── ProjectSettings/  # 工程设置（由 Unity 生成）
├── frontend/             # 前端代码：React + TypeScript 管理端，问题列表与状态修改
│   ├── src/              # api / components / pages / types
│   ├── index.html
│   └── package.json
├── backend/              # 后端服务代码：Go 接口、参数校验与持久化
│   ├── cmd/server/       # 服务入口
│   ├── cmd/migrate/      # 一次性迁移工具（旧 JSON -> SQLite）
│   ├── internal/         # api / model / store / migrate 分层
│   └── Dockerfile        # 后端镜像（多阶段构建）
├── frontend/             # 前端代码（含 Dockerfile 与 nginx.conf）
├── compose.yaml          # 一键编排：前端 + 后端
├── .env.example          # 端口与构建源的可覆盖默认值
├── Tools/                # 启动脚本的实际逻辑
├── docs/                 # 需求规格说明书、部署与打包说明
├── start-docker.cmd      # 一键：构建并启动 Web 两件套
├── start-unity.cmd       # 构建并运行 Unity 客户端 / 打包 release zip
├── 项目介绍.txt          # 项目背景与固定技术栈
├── CODEBUDDY.md          # 开发约定
├── README.md
└── .gitignore
```

三端目录相互独立，不共享源码，只通过 HTTP 接口与同一份后端数据交互。各目录内的 `README.md` 记录该端的用途、目录说明与命令。

## 快速开始

Web 两件套（React + Go）走 Docker Compose，宿主机不需要装 Go 或 Node：

| 脚本 / 命令 | 作用 |
| --- | --- |
| `start-docker.cmd` | 构建（镜像缺失时）并启动前端与后端，等健康后打开浏览器 |
| `start-docker.cmd web` | 同上，但不拉起 Unity 客户端 |
| `start-docker.cmd build` | 强制重建镜像后启动 |
| `start-docker.cmd down` | 停止容器（命名卷保留数据） |
| `start-docker.cmd logs` | 跟踪容器日志 |
| `start-docker.cmd check` | 检查 Docker、Compose、镜像、数据卷、端口、场景与运行包 |
| `start-unity.cmd` | 构建（源码有变化时）并运行 `unity/Build/UnityInspection.exe` |
| `start-unity.cmd package` | 构建并打包成 `release/UnityInspection-win64.zip` |
| `start-unity.cmd build` / `editor` | 强制重新构建再运行 / 打开 Unity 编辑器 |

启动后：

- 管理端 <http://localhost:8088>
- 后端 <http://localhost:8080/api/health>
- Unity 客户端直接连 <http://localhost:8080>，与浏览器同源无关

两个脚本都只是薄封装，实际逻辑在 `Tools/start-inspection.ps1`：输出分级为 `[OK]` / `[!!]` / `[XX]`，错误信息附带可操作提示，失败时返回非零退出码（依赖缺失为 2，运行失败为 1）。

等价的手动命令：

```bash
docker compose up -d --build   # 构建并启动
docker compose logs -f         # 跟踪日志
docker compose down            # 停止（数据卷保留）
```

端口、镜像标签与构建源都可以用根目录的 `.env` 覆盖，见 `.env.example`。

## 数据与部署

- 容器内数据落在命名卷 `unity-3d-inspection-data` 的 `/data/issues.db`，`docker compose down` 不会删除，`down -v` 才会。
- 只跑宿主机 `go run ./cmd/server` 时，数据库在 `backend/data/issues.db`；两份数据互不影响。
- 旧的 JSON 数据可用 `backend/cmd/migrate` 一次性导入 SQLite，详见[部署与打包说明](docs/部署与打包说明.md)。
- Unity 客户端不参与编排：它必须在 Windows 上用 Unity 编辑器构建成 exe，后端地址可通过 exe 同目录的 `backend-config.json` 指向别的机器。
