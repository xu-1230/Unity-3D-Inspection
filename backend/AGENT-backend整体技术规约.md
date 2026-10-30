# AGENT-backend整体技术规约

## 1 适用范围

约束 `backend/` 目录内的全部内容：HTTP 路由、参数校验、领域实体、持久化。不涉及 Unity 客户端与管理端实现。

## 2 与其他规约的关系

- 技术栈、跨端契约、目录纪律、提交规范见 `../AGENT-整体技术规约.md`。
- 接口路径、请求与响应字段、错误码、状态迁移规则见 `../docs/需求规格说明书.md` 第 5、6 章；本端是实现方，不另立定义。
- 本文件第 6 节只写实现约束，契约本体不在此复制。

## 3 技术栈与依赖

- Go，模块名 `unity-3d-inspection/backend`，`go.mod` 中声明的版本不低于 1.22（现有路由写法依赖 1.22+ 的方法路由）。
- HTTP 使用标准库 `net/http`；引入第三方路由、ORM 或校验库需在提交说明中写明用途与替代方案。
- 持久化只用 SQLite 驱动或 `encoding/json` 写本地文件，不引入外部数据库服务、缓存或消息队列。

## 4 目录与分层

| 路径 | 职责 | 依赖 |
| --- | --- | --- |
| `cmd/server/` | 读取配置、装配依赖、注册路由、启动服务 | `internal/api`、`internal/store` |
| `internal/api/` | 路由处理器、请求解析、参数校验、错误响应 | `internal/model`、`internal/store` |
| `internal/model/` | `Issue` 实体、优先级与状态常量、字段校验函数 | 仅标准库 |
| `internal/store/` | 存储接口与实现（JSON 文件或 SQLite） | `internal/model` |

依赖方向固定为 `cmd/api → model + store`、`store → model`；`model` 不依赖项目内其他包，`store` 不依赖 `api`。

处理器通过接口使用存储：

```go
type Store interface {
    List() ([]model.Issue, error)
    Create(issue model.Issue) (model.Issue, error)
    UpdateStatus(id string, status string) (model.Issue, error)
}
```

新增存储实现时新增一个文件并实现该接口，`cmd/server` 决定注入哪一个。

## 5 编码约定

- 路由使用方法路由写法，与现有 `main.go` 一致：`mux.HandleFunc("POST /api/issues", ...)`。
- 处理器返回前设置 `Content-Type: application/json; charset=utf-8`。
- 错误统一用结构体构造，字段顺序固定为 `code`、`message`、`field`；`message` 指出具体原因，例如「标题不能为空」。
- 校验顺序固定：JSON 可解析性 → `title` → `priority` → `location` → `status`（仅改状态接口），命中即返回，不再继续校验。
- 请求体解析使用 `json.Decoder` 并拒绝未知字段之外的类型错误；请求体大小限制在 1 MB 以内。
- 时间统一 `time.Now().UTC()`，序列化为 RFC3339 字符串。
- 日志记录启动端口、监听失败、存储读写失败、请求方法与路径与状态码；不记录完整请求体。
- 测试文件与被测文件同包同目录，命名 `<file>_test.go`；存储层与校验函数需覆盖正常与错误分支，处理器用 `httptest` 覆盖 4 个端点。

## 6 契约与数据

**端点与状态码**（路径与字段以规格说明书第 5 章为准）：

| 方法 | 路径 | 成功 | 失败 |
| --- | --- | --- | --- |
| GET | `/api/health` | 200 `{"status":"ok"}` | 500 |
| POST | `/api/issues` | 201 完整对象 | 400 |
| GET | `/api/issues` | 200 数组，按 `createdAt` 倒序 | 500 |
| PATCH | `/api/issues/{id}/status` | 200 更新后对象 | 400 / 404 |

**标识**：创建时由服务端生成，取 UUID v4 或 `crypto/rand` 生成的 32 位十六进制串；同一进程内不与已有标识重复。

**服务端字段**：`status` 创建时固定 `open`；`createdAt` 与 `updatedAt` 创建时同值写入，状态变更时只刷新 `updatedAt`。请求体中的 `id`、`status`、`createdAt`、`updatedAt` 一律忽略。

**状态机**：三态任意互转（含目标与当前相同的幂等情形），合法性只要求取值属于 `open`、`in_progress`、`resolved`；非法值返回 400 `INVALID_STATUS`。不实现单向流转限制。

**持久化**：

- 启动时从固定路径加载数据，文件或表不存在时以空集合启动。
- JSON 实现：全量写入临时文件后原子重命名替换，避免半写损坏。
- SQLite 实现：写操作放在事务内。
- 存储路径默认 `data/`（已在 `.gitignore` 中），可用环境变量覆盖；路径不随启动目录变化。
- 对写操作加互斥（互斥量或单写入协程），并发创建时每个问题都获得唯一标识并全部保存。

**跨域**：允许 `http://localhost` 与 `http://127.0.0.1` 的任意端口来源，允许方法 `GET`、`POST`、`PATCH`、`OPTIONS`，允许请求头 `Content-Type`；`OPTIONS` 返回 204。不使用 Cookie，不开启凭据模式。

**错误处理**：存储失败返回 500 `INTERNAL`，响应体不包含文件路径、SQL 语句或驱动错误原文，只记入日志。

## 7 验证方式

```bash
go vet ./...
go build ./...
go test ./...            # 单测：go test ./internal/store -run TestCreate
```

启动后冒烟（Windows 使用 `curl.exe`）：

```bash
curl.exe -s http://localhost:8080/api/health
curl.exe -s -X POST http://localhost:8080/api/issues -H "Content-Type: application/json" -d "{\"title\":\"cabinet-a-leak\",\"priority\":\"high\",\"location\":{\"x\":1.0,\"y\":2.0,\"z\":3.0}}"
curl.exe -s http://localhost:8080/api/issues
curl.exe -s -X PATCH http://localhost:8080/api/issues/<id>/status -H "Content-Type: application/json" -d "{\"status\":\"resolved\"}"
```

冒烟通过标准：创建返回 201 且 `status` 为 `open`；列表包含刚创建的问题；改状态返回 200 且 `updatedAt` 变化；重启服务后列表内容不变；`title` 为空返回 400 `INVALID_TITLE`；`priority` 取 `urgent` 返回 400 `INVALID_PRIORITY`。

当前开发机未安装 Go 工具链，上述命令无法在本环境执行；提交说明中标注「未执行」及原因。

## 8 禁止事项

- 在处理器里直接读写文件或拼 SQL，绕过 `internal/store`。
- 把参数校验失败合并成通用 400 或 500，或省略 `field`。
- 用 200 表示创建或更新失败。
- 接受请求体中的 `id`、`status`、`createdAt`、`updatedAt`。
- 修改状态时改写 `createdAt`、`id` 或 `location`。
- 在接口层或存储层对 `location` 的分量做换算、取整或校验之外的变换。
- 把存储错误的原始信息返回给调用方。
