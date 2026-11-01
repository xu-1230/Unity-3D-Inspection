# 前端镜像：Node 构建 -> Nginx 托管静态产物并反代 /api。
#
# BASE_REGISTRY 是基础镜像前缀：留空走 Docker Hub，国内网络可换成镜像加速站。
# NPM_REGISTRY 用于构建期安装依赖。
ARG BASE_REGISTRY=

FROM ${BASE_REGISTRY}node:22-alpine AS build

ARG NPM_REGISTRY=https://registry.npmmirror.com

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm config set registry "$NPM_REGISTRY" \
 && npm ci --no-audit --no-fund

COPY . .
RUN npm run build

FROM ${BASE_REGISTRY}nginx:1.27-alpine

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
