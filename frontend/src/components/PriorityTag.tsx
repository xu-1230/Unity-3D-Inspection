import { PRIORITY_LABELS, PRIORITY_HINTS, type IssuePriority } from '../types/issue';

interface PriorityTagProps {
  priority: IssuePriority;
}

const FILLED: Record<IssuePriority, number> = { low: 1, medium: 2, high: 3 };

/** 用三段竖条表达优先级强度，避免只靠颜色区分。 */
export default function PriorityTag({ priority }: PriorityTagProps) {
  return (
    <span className={`priority priority--${priority}`} title={PRIORITY_HINTS[priority]}>
      <span className="priority__bars" aria-hidden="true">
        {[0, 1, 2].map((index) => (
          <i key={index} className={index < FILLED[priority] ? 'on' : undefined} />
        ))}
      </span>
      {PRIORITY_LABELS[priority]}优先级
    </span>
  );
}
