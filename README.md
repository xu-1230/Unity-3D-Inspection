# Unity-3D巡检

基于 Unity 的 3D 巡检项目：巡检人员在虚拟现场标记问题位置并上报，管理人员在网页端查看并处置。

## 系统组成

| 部分 | 技术栈 | 职责 |
| --- | --- | --- |
| 巡检客户端 | Unity LTS + C# | 虚拟场景、视角移动、射线点击、空间标记、问题上报 |
| 网页管理端 | React + TypeScript | 查看问题列表，修改处理状态 |
| 后端服务 | Go | 提供数据接口，持久化保存（SQLite 或本地 JSON 文件） |

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

请求与响应均为 JSON，错误统一为 `{"error":{"code","message","field"}}`。

## 文档

- [需求规格说明书](docs/需求规格说明书.md)：功能需求、接口契约、数据模型与状态机
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
│   └── internal/         # api / model / store 分层
├── docs/                 # 需求规格说明书
├── start-all.cmd         # 一键启动后端与前端
├── start-backend.cmd     # 启动 Go 服务
├── start-frontend.cmd    # 启动 React 开发服务器
├── start-unity.cmd       # 打开 Unity 工程
├── 项目介绍.txt          # 项目背景与固定技术栈
├── CODEBUDDY.md          # 开发约定
├── README.md
└── .gitignore
```

三端目录相互独立，不共享源码，只通过 HTTP 接口与同一份后端数据交互。各目录内的 `README.md` 记录该端的用途、目录说明与命令。

## 快速开始

双击或命令行执行仓库根目录的脚本：

| 脚本 | 作用 |
| --- | --- |
| `start-all.cmd` | 探测端口后分别拉起后端与前端，并打开浏览器 |
| `start-backend.cmd` | `go run ./cmd/server`，监听 `http://localhost:8080` |
| `start-frontend.cmd` | 首次自动 `npm install`，随后 `npm run dev`，监听 `http://localhost:5173` |
| `start-unity.cmd` | 用本机 Unity 编辑器打开 `unity/` 工程 |
| `start-all.cmd check` | 检查 Go / Node / Unity、端口占用、数据文件与场景 |

四个脚本都只是薄封装，实际逻辑在 `Tools/start-inspection.ps1`：输出分级为 `[OK]` / `[!!]` / `[XX]`，错误信息附带可操作提示，失败时返回非零退出码（依赖缺失为 2，运行失败为 1）。

手动执行等价命令：

```bash
# 后端（Go 位于 D:\ctf\go，脚本会自动前置到 PATH）
cd backend && "D:\ctf\go\bin\go.exe" run ./cmd/server   # http://localhost:8080/api/health

# 前端
cd frontend && npm install && npm run dev                # http://localhost:5173

# Unity 客户端
# 用 Unity Hub 以 LTS 版本打开 unity/，在菜单执行 Tools > 生成巡检场景
```
