import { API_BASE_URL } from './api/client';

export default function App() {
  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', padding: 24, lineHeight: 1.6 }}>
      <h1>Unity-3D巡检 · 管理端</h1>
      <p>页面骨架已就绪，问题列表与状态修改待实现。</p>
      <p>
        后端基址：<code>{API_BASE_URL}</code>
      </p>
      <ul>
        <li>问题列表（标题、优先级、状态、坐标）</li>
        <li>状态修改：open / in_progress / resolved</li>
        <li>后端不可用时显示错误提示与重试入口</li>
      </ul>
    </main>
  );
}
