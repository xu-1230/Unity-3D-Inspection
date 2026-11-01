import type { ReactNode } from 'react';
import { IconInbox } from './Icons';

interface EmptyStateProps {
  title: string;
  description: string;
  action?: ReactNode;
}

export default function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="state">
      <span className="state__art" aria-hidden="true">
        <IconInbox />
      </span>
      <p className="state__title">{title}</p>
      <p className="state__desc">{description}</p>
      {action ? <div className="state__actions">{action}</div> : null}
    </div>
  );
}
