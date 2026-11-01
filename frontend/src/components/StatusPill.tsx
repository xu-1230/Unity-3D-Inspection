import { STATUS_LABELS, type IssueStatus } from '../types/issue';

interface StatusPillProps {
  status: IssueStatus;
}

/** 只读状态标记，颜色语义与批量修改控件保持一致。 */
export default function StatusPill({ status }: StatusPillProps) {
  return <span className={`pill pill--${status}`}>{STATUS_LABELS[status]}</span>;
}
