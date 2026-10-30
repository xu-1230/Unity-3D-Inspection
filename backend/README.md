# backend — Go 后端服务

存放后端服务代码，对外提供问题上报、查询与状态修改接口，并负责持久化。技术栈固定为 Go，持久化只允许 SQLite 或本地 JSON 文件。

## 用途

Unity 客户端与管理端共用同一份数据，二者的读写都经过本服务，因此这里是唯一的数据来源与状态机裁决方。

## 目录说明

| 路径 | 用途 |
| --- | --- |
| `cmd/server/` | 服务入口 `main.go`：读取配置、装配依赖、启动 HTTP 服务 |
| `internal/api/` | HTTP 路由与处理器，负责请求解析、参数校验与错误响应 |
| `internal/model/` | 领域实体与枚举定义（Issue、优先级、状态） |
| `internal/store/` | 持久化层，用接口隔离 SQLite 与 JSON 文件两种实现 |

`data/` 目录用于存放运行时产生的数据库或 JSON 文件，已被 `.gitignore` 忽略，克隆后不会存在，由程序启动时创建。

## 运行

```bash
go run ./cmd/server
go build ./...
go test ./...
```

服务默认监听 `8080`，可用环境变量 `PORT` 覆盖。当前 `main.go` 只装配了 `/api/health`，其余接口在 `internal/api` 中实现。

> `go.mod` 中声明的 Go 版本为初始值，按本机工具链调整即可，不影响其他两端。

## 接口

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/health` | 返回 `{"status":"ok"}` |
| POST | `/api/issues` | 保存上报的问题，201 返回完整对象 |
| GET | `/api/issues` | 返回问题列表，按创建时间倒序 |
| PATCH | `/api/issues/{id}/status` | 修改状态，返回更新后的对象 |

字段、错误码与状态机规则见 [需求规格说明书](../docs/需求规格说明书.md) 第 5、6 章，不要在本目录另立一套规则。
