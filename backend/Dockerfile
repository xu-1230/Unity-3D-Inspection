# 后端镜像：Go 静态编译 -> 精简运行层。
#
# BASE_REGISTRY 是基础镜像前缀：留空走 Docker Hub，国内网络可换成镜像加速站
# （compose.yaml 已默认指向加速站，直连 Docker Hub 时把它设为空即可）。
# GOPROXY 用于构建期下载 Go 模块。
ARG BASE_REGISTRY=

FROM ${BASE_REGISTRY}golang:1.23-alpine AS build

ARG GOPROXY=https://goproxy.cn,direct

ENV CGO_ENABLED=0 \
    GOOS=linux \
    GOPROXY=${GOPROXY}

WORKDIR /src

# 先只拷贝依赖清单，让 go mod download 的层可以被缓存。
COPY go.mod go.sum ./
RUN go mod download

COPY . .
RUN go build -trimpath -ldflags="-s -w" -o /out/server ./cmd/server \
 && go build -trimpath -ldflags="-s -w" -o /out/migrate ./cmd/migrate

FROM ${BASE_REGISTRY}alpine:3.20

# busybox 自带 wget，供 compose 的健康检查在容器内探测 /api/health。
RUN adduser -D -H -u 10001 inspection \
 && mkdir -p /data \
 && chown inspection:inspection /data

COPY --from=build /out/server /usr/local/bin/server
COPY --from=build /out/migrate /usr/local/bin/migrate

USER inspection

ENV PORT=8080 \
    DATA_PATH=/data/issues.db

EXPOSE 8080

ENTRYPOINT ["/usr/local/bin/server"]
