# backend — Go 后端服务

存放后端服务代码，对外提供问题上报、查询与状态修改接口，并负责持久化。技术栈固定为 Go，持久化使用单个 SQLite 文件（驱动为纯 Go 的 `modernc.org/sqlite`，不需要 cgo）。

## 用途

Unity 客户端与管理端共用同一份数据，二者的读写都经过本服务，因此这里是唯一的数据来源与状态机裁决方。

## 目录说明

| 路径 | 用途 |
| --- | --- |
| `cmd/server/` | 服务入口 `main.go`：读取配置、装配依赖、启动 HTTP 服务 |
| `cmd/migrate/` | 一次性迁移工具：把旧的 `data/issues.json` 导入 SQLite |
| `internal/api/` | HTTP 路由与处理器，负责请求解析、参数校验与错误响应 |
| `internal/model/` | 领域实体与枚举定义（Issue、优先级、状态） |
| `internal/store/` | 持久化层：`Store` 接口 + SQLite 实现 |
| `internal/store/storetest/` | 存储一致性测试套件，任何实现都必须逐条通过 |
| `internal/migrate/` | 迁移逻辑，供 `cmd/migrate` 调用 |
| `Dockerfile` | 多阶段构建：Go 静态编译 → `alpine`，以非 root 用户运行 |

`data/` 目录用于存放运行时的数据库文件，已被 `.gitignore` 忽略，克隆后不会存在，由程序启动时创建。

## 运行

```bash
go run ./cmd/server
go build ./...
go vet ./...
go test ./...
```

服务默认监听 `8080`，可用环境变量 `PORT` 覆盖；数据库默认 `data/issues.db`，可用 `DATA_PATH` 覆盖。五个接口（健康检查、创建、列表、改状态、删除）已全部实现，进程收到 `Ctrl+C` 时优雅退出。

测试分两层：`internal/*/characterization_test.go` 固定 HTTP/模型层的既有行为（路由表、错误码、校验顺序、CORS 判定、时间格式等），`internal/store/storetest` 是与实现无关的存储一致性套件，`internal/store` 内只有一个 Factory 把它接到 SQLite 上。

## 数据迁移

旧版本把问题存成 `data/issues.json`，现在统一用 SQLite。迁移是一次显式动作，可重复执行（按「标识 + 创建时间」跳过已导入记录）：

```bash
go run ./cmd/migrate -from data/issues.json -to data/issues.db
```

## 容器

后端镜像可用 `docker build` 或根目录的 `compose.yaml` 构建。容器内 `DATA_PATH=/data/issues.db`，`/data` 由命名卷挂载；镜像同时携带 `migrate` 二进制，可在容器内执行迁移：

```bash
docker compose cp backend/data/issues.json backend:/data/
docker compose exec backend /usr/local/bin/migrate -from /data/issues.json -to /data/issues.db
```

> `go.mod` 中声明的 Go 版本为初始值，按本机工具链调整即可，不影响其他两端。

## 技术规约

分层与依赖方向、端点实现约束、状态机、持久化纪律与跨域配置见 [AGENT-backend整体技术规约.md](AGENT-backend整体技术规约.md)。

接口路径、字段、错误码与状态取值见 [需求规格说明书](../docs/需求规格说明书.md) 第 5、6 章。
