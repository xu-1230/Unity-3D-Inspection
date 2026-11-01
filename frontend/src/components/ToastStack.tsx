import { IconAlert } from './Icons';

/** 轻提示承载失败信息与后台同步摘要；成功操作本身直接体现在界面数据上。 */
export interface ToastItem {
  id: number;
  message: string;
  /** error（默认）用于失败，info 用于后台同步提示。 */
  tone?: 'error' | 'info';
}

interface ToastStackProps {
  items: ToastItem[];
  onDismiss: (id: number) => void;
}

export default function ToastStack({ items, onDismiss }: ToastStackProps) {
  return (
    <div className="toast-stack" role="status" aria-live="polite">
      {items.map((item) => (
        <div key={item.id} className={item.tone === 'info' ? 'toast toast--info' : 'toast'}>
          {item.tone === 'info' ? null : <IconAlert size={16} />}
          <span>{item.message}</span>
          <button type="button" className="toast__close" aria-label="关闭提示" onClick={() => onDismiss(item.id)}>
            ×
          </button>
        </div>
      ))}
    </div>
  );
}
