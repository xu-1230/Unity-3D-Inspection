import { useEffect, useRef } from 'react';

interface ConfirmDialogProps {
  title: string;
  description: string;
  confirmLabel: string;
  busy: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export default function ConfirmDialog({
  title,
  description,
  confirmLabel,
  busy,
  onCancel,
  onConfirm,
}: ConfirmDialogProps) {
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    dialogRef.current?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !busy) {
        onCancel();
      }
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [busy, onCancel]);

  return (
    <>
      <div className="overlay overlay--top" role="presentation" onClick={() => (busy ? undefined : onCancel())} />
      <div
        className="dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-title"
        aria-describedby="confirm-desc"
        tabIndex={-1}
        ref={dialogRef}
        style={{ outline: 'none' }}
      >
        <h2 className="dialog__title" id="confirm-title">
          {title}
        </h2>
        <p className="dialog__desc" id="confirm-desc">
          {description}
        </p>
        <div className="dialog__actions">
          <button type="button" className="btn btn--ghost btn--sm" onClick={onCancel} disabled={busy}>
            取消
          </button>
          <button type="button" className="btn btn--danger btn--sm" onClick={onConfirm} disabled={busy}>
            {busy ? '删除中…' : confirmLabel}
          </button>
        </div>
      </div>
    </>
  );
}
