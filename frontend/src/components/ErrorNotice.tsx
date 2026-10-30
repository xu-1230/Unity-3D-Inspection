interface ErrorNoticeProps {
  message: string;
  onRetry: () => void;
}

export default function ErrorNotice({ message, onRetry }: ErrorNoticeProps) {
  return (
    <div
      role="alert"
      style={{
        border: '1px solid #d93025',
        background: '#fdecea',
        color: '#a50e0e',
        borderRadius: 8,
        padding: 16,
        marginBottom: 16,
      }}
    >
      <strong>加载失败</strong>
      <p style={{ margin: '8px 0 12px' }}>{message}</p>
      <button type="button" onClick={onRetry}>
        重试
      </button>
    </div>
  );
}
