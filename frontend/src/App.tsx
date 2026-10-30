import IssueListPage from './pages/IssueListPage';

export default function App() {
  return (
    <main
      style={{
        fontFamily: 'system-ui, sans-serif',
        lineHeight: 1.6,
        margin: '0 auto',
        maxWidth: 1080,
        padding: 24,
      }}
    >
      <h1 style={{ marginBottom: 4 }}>Unity-3D巡检 · 管理端</h1>
      <p style={{ color: '#5f6368', marginTop: 0 }}>查看巡检上报的问题，并推进处理状态。</p>
      <IssueListPage />
    </main>
  );
}
