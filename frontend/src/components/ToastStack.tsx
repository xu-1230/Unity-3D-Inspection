import { IconAlert } from './Icons';

/** 轻提示只承载失败信息；成功操作直接体现在界面数据上，不再额外弹出通知。 */
export interface ToastItem {
  id: number;
  message: string;
}

interface ToastStackProps {
  items: ToastItem[];
  onDismiss: (id: number) => void;
}

export default function ToastStack({ items, onDismiss }: ToastStackProps) {
  return (
    <div className="toast-stack" role="status" aria-live="polite">
      {items.map((item) => (
        <div key={item.id} className="toast">
          <IconAlert size={16} />
          <span>{item.message}</span>
          <button type="button" className="toast__close" aria-label="关闭提示" onClick={() => onDismiss(item.id)}>
            ×
          </button>
        </div>
      ))}
    </div>
  );
}
