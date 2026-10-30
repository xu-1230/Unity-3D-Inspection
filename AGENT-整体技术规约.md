# AGENT-整体技术规约

本文件是「Unity-3D巡检」的技术约束入口。约束分两层：本文件定义跨端不可协商项，各端目录内的规约定义本端实现细节。

## 1 适用范围

- 任何对 `unity/`、`frontend/`、`backend/`、`docs/` 的改动，先读本文件，再读所属端规约。
- 本文件与所属端规约冲突时，以本文件为准；本文件与 `docs/需求规格说明书.md` 冲突时，接口与状态相关以规格说明书为准。

## 2 文件索引

| 文件 | 约束范围 | 何时读取 |
| --- | --- | --- |
| `AGENT-整体技术规约.md`（本文件） | 技术栈、跨端契约、目录纪律、提交规范、验证基线 | 任何改动前 |
| `unity/AGENT-unity整体技术规约.md` | Unity 客户端 | 新增或修改场景、脚本、预制体 |
| `frontend/AGENT-frontend整体技术规约.md` | React 管理端 | 新增或修改页面、组件、接口调用 |
| `backend/AGENT-backend整体技术规约.md` | Go 后端服务 | 新增或修改接口、校验、持久化 |
| `docs/需求规格说明书.md` | 契约与状态的唯一定义处 | 需要字段、错误码、状态规则的确切定义时 |

## 3 固定技术栈

| 端 | 目录 | 技术 |
| --- | --- | --- |
| 巡检客户端 | `unity/` | Unity LTS + C# |
| 网页管理端 | `frontend/` | React + TypeScript + Vite |
| 后端服务 | `backend/` | Go |
| 持久化 | `backend/` | SQLite 或本地 JSON 文件，二选一 |

技术栈与存储选项来自 `项目介绍.txt`，属于固定项。

## 4 跨端契约

下面是字段与枚举的名称清单，用于判断改动影响面；定义与示例见 `docs/需求规格说明书.md` 第 5、6 章。

- 实体字段：`id`、`title`、`description`、`priority`、`status`、`location`（含 `x`、`y`、`z`）、`createdAt`、`updatedAt`
- 优先级取值：`low`、`medium`、`high`
- 状态取值：`open`、`in_progress`、`resolved`
- 端点：`GET /api/health`、`POST /api/issues`、`GET /api/issues`、`PATCH /api/issues/{id}/status`
- 错误响应：`{"error":{"code","message","field"}}`

字段或枚举的新增、改名、删除属于契约变更，在同一次改动内把后端 model、Unity 上报结构、管理端类型与规格说明书一起改完。

## 5 三条铁律

1. **坐标是不透明数据。** `location` 的分量表示客户端场景世界坐标；后端只存储与返回，管理端只展示，两端都不做单位换算、轴向翻转、取整或归一化。
2. **状态机由后端裁决。** 客户端只提交初始值，管理端只提交目标值；合法性判断与 `updatedAt` 刷新在 `backend/` 内完成，另两端不实现第二套规则。
3. **一段数据一个来源。** 问题数据只在后端持久化一次；另两端的本地状态仅服务当前会话展示，不回写为数据来源。

## 6 目录纪律

- 三端互不引用源码，跨端只经 HTTP 通信；`unity/` 不读 `frontend/`，`frontend/` 不读 `backend/`。
- 生成物不入库：Unity 的 `Library/`、`Temp/`、`Logs/`，前端的 `node_modules/`、`dist/`，后端的 `data/` 已由 `.gitignore` 覆盖；新增生成目录时同步补规则。
- 新增目录时，在所属端 `README.md` 的目录表中登记一行。

## 7 分支与提交

分支模型为 git flow：

| 分支 | 用途 | 起点 | 合并去向 |
| --- | --- | --- | --- |
| `main` | 存放已发布版本，合并后打标签 | — | — |
| `develop` | 日常集成 | `main` | — |
| `feature/<主题>` | 单个特性 | `develop` | `develop`，用 `--no-ff` |
| `release/<版本>` | 发布准备 | `develop` | `main` 与 `develop`，并在 `main` 打标签 |
| `hotfix/<主题>` | 线上修复 | `main` | `main` 与 `develop` |

提交信息使用 Conventional Commits：`<type>(<scope>): <摘要>`。

- `type` 取 `feat`、`fix`、`refactor`、`docs`、`test`、`chore`。
- `scope` 取 `unity`、`frontend`、`backend`、`docs` 或受影响模块名。
- 摘要在 50 字内说明改了什么；正文分条列要点与未验证项。
- 合并提交沿用 git 默认消息 `Merge branch '<源分支>' into <目标分支>`。

## 8 验证基线

改动后至少跑通所属端的最小验证：

| 端 | 命令 | 通过标准 |
| --- | --- | --- |
| `frontend/` | `npm run build` | `tsc --noEmit` 无错误且产出 `dist/` |
| `backend/` | `go vet ./...`、`go build ./...`、`go test ./...` | 无输出或全部 `ok`，退出码 0 |
| `unity/` | 在 Unity 编辑器中编译工程 | Console 无编译错误 |

当前开发机未安装 Go 与 Unity 编辑器，这两端的命令无法在此环境执行；提交说明中写明「未执行」及原因，不以推测代替验证结果。

## 9 全局禁止事项

- 引入外部数据库、缓存或消息队列替代 SQLite / 本地 JSON。
- 增加登录、鉴权、用户体系或角色权限。
- 让某一端绕过 HTTP 直连另一端的存储或源码。
- 新增范围外能力（图片附件、实时推送、多场景切换、报表导出等）。范围外清单以规格说明书第 7 章为准。
