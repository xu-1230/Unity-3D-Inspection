import { useEffect, useRef } from 'react';
import type { CSSProperties } from 'react';
import PriorityTag from './PriorityTag';
import StatusPill from './StatusPill';
import { IconClose, IconTrash } from './Icons';
import { areaName, deviceName, formatAxis, formatTime, shortId } from '../lib/format';
import { STATUS_LABELS, STATUS_OPTIONS, type Issue, type IssueStatus } from '../types/issue';

interface IssueDrawerProps {
  issue: Issue;
  busy: boolean;
  /** 删除确认框打开时置为 false，避免一次 Esc 同时关闭两层浮层。 */
  escEnabled: boolean;
  onClose: () => void;
  onStatusChange: (next: IssueStatus) => void;
  onRequestDelete: () => void;
}

const STATUS_TONES: Record<IssueStatus, string> = {
  open: 'var(--open)',
  in_progress: 'var(--progress)',
  resolved: 'var(--resolved)',
};

export default function IssueDrawer({
  issue,
  busy,
  escEnabled,
  onClose,
  onStatusChange,
  onRequestDelete,
}: IssueDrawerProps) {
  const closeRef = useRef<HTMLButtonElement>(null);
  const closeHandler = useRef(onClose);
  const escAllowed = useRef(escEnabled);

  useEffect(() => {
    closeHandler.current = onClose;
    escAllowed.current = escEnabled;
  }, [onClose, escEnabled]);

  // 只在挂载时接管焦点与页面滚动，避免每次父组件重渲染都把焦点抢回关闭按钮。
  useEffect(() => {
    closeRef.current?.focus();
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && escAllowed.current) {
        closeHandler.current();
      }
    };
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  return (
    <>
      <div className="overlay" role="presentation" onClick={onClose} />
      <aside className="drawer" role="dialog" aria-modal="true" aria-labelledby="issue-detail-title">
        <header className="drawer__head">
          <div>
            <span className="detail-head__id" title={issue.id}>
              ISSUE / {shortId(issue.id)}
            </span>
            <h2 className="detail-title" id="issue-detail-title">
              {issue.title}
            </h2>
            <div className="detail-tags">
              <StatusPill status={issue.status} />
              <PriorityTag priority={issue.priority} />
            </div>
          </div>
          <button ref={closeRef} type="button" className="icon-btn" aria-label="关闭详情" onClick={onClose}>
            <IconClose />
          </button>
        </header>

        <div className="drawer__body">
          <p className="section-title">问题描述</p>
          <p className={`detail-desc${issue.description.trim() === '' ? ' detail-desc--empty' : ''}`}>
            {issue.description.trim() === '' ? '上报时未填写描述，可结合现场坐标复核。' : issue.description}
          </p>

          <p className="section-title">处理状态</p>
          <div className="status-choice" role="group" aria-label="修改处理状态">
            {STATUS_OPTIONS.map((status) => (
              <button
                key={status}
                type="button"
                className={issue.status === status ? 'is-active' : undefined}
                style={{ '--tone': STATUS_TONES[status] } as CSSProperties}
                aria-pressed={issue.status === status}
                disabled={busy}
                onClick={() => onStatusChange(status)}
              >
                {STATUS_LABELS[status]}
                <span className="status-choice__code">{status}</span>
              </button>
            ))}
          </div>

          <p className="section-title">现场信息</p>
          <div className="meta-grid">
            <div className="meta">
              <div className="meta__label">区域</div>
              <div className="meta__value">{areaName(issue)}</div>
            </div>
            <div className="meta">
              <div className="meta__label">设备编号</div>
              <div className="meta__value">{deviceName(issue)}</div>
            </div>
            <div className="meta">
              <div className="meta__label">上报时间</div>
              <div className="meta__value meta__value--mono">{formatTime(issue.createdAt)}</div>
            </div>
            <div className="meta">
              <div className="meta__label">最后更新</div>
              <div className="meta__value meta__value--mono">{formatTime(issue.updatedAt)}</div>
            </div>
          </div>

          <p className="section-title">Unity 世界坐标</p>
          <div className="coord-grid">
            <div className="coord">
              <div className="coord__axis">X</div>
              <div className="coord__value">{formatAxis(issue.location.x)}</div>
            </div>
            <div className="coord">
              <div className="coord__axis">Y</div>
              <div className="coord__value">{formatAxis(issue.location.y)}</div>
            </div>
            <div className="coord">
              <div className="coord__axis">Z</div>
              <div className="coord__value">{formatAxis(issue.location.z)}</div>
            </div>
          </div>

          <p className="section-title">完整标识</p>
          <div className="state__code" style={{ marginTop: 0 }}>
            {issue.id}
          </div>
        </div>

        <footer className="drawer__foot">
          <button type="button" className="btn btn--danger btn--sm" onClick={onRequestDelete} disabled={busy}>
            <IconTrash size={14} />
            删除问题
          </button>
          <button type="button" className="btn btn--primary btn--sm" onClick={onClose}>
            完成
          </button>
        </footer>
      </aside>
    </>
  );
}
