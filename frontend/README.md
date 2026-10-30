# frontend — React 网页管理端

存放前端代码，面向管理人员：从后端拉取问题列表、查看问题信息、修改处理状态。技术栈固定为 React + TypeScript，构建工具为 Vite。

## 用途

管理端不参与场景与坐标计算，只做展示与状态提交；一切数据校验与状态流转以 [需求规格说明书](../docs/需求规格说明书.md) 第 5、6 章为准，不在前端重复实现另一套规则。

## 目录说明

| 路径 | 用途 |
| --- | --- |
| `src/api/` | 后端接口调用封装，统一 base URL 与错误处理 |
| `src/components/` | 可复用展示组件（问题表格、状态选择器、错误提示） |
| `src/pages/` | 页面级组件（问题列表页） |
| `src/types/` | 与后端契约对应的类型定义（Issue、枚举） |
| `public/` | 不参与打包编译的静态资源 |

## 命令

```bash
npm install      # 安装依赖
npm run dev      # 本地开发，默认 http://localhost:5173
npm run build    # 类型检查 + 产出 dist/
npm run preview  # 预览构建产物
```

## 环境变量

复制 `.env.example` 为 `.env.local`，按需修改后端地址：

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `VITE_API_BASE_URL` | `http://localhost:8080` | Go 后端的基址，不含 `/api` |

跨域由后端开放，前端不配置代理。

## 当前状态

`src/main.tsx` 与 `src/App.tsx` 是可运行的占位页面；列表、状态修改与错误提示尚未实现。`src/types/issue.ts` 已按契约定义好字段，可直接复用。
