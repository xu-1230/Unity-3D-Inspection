# frontend — React 网页管理端

存放前端代码，面向管理人员：从后端拉取问题列表、查看问题信息、修改处理状态。技术栈固定为 React + TypeScript，构建工具为 Vite。

## 用途

管理端不参与场景与坐标计算，只做问题展示与状态提交。

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
| `VITE_API_BASE_URL` | 空（同源） | Go 后端的基址，不含 `/api`；留空时走 Vite 代理 |

`vite.config.ts` 已把 `/api` 代理到 `http://localhost:8080`，因此开发时前端使用相对路径即可，不依赖跨域配置。

## 技术规约

类型契约、网络层纪律、视图状态与验证清单见 [AGENT-frontend整体技术规约.md](AGENT-frontend整体技术规约.md)；跨端约束见 [AGENT-整体技术规约.md](../AGENT-整体技术规约.md)。

## 当前状态

问题列表页已可用：展示标题、优先级、状态、坐标与更新时间；加载中/空/错误/有数据四态互斥；状态下拉修改以后端返回为准，失败时提示且控件回退；顶栏可刷新。
