import {
  PRIORITY_LABELS,
  PRIORITY_OPTIONS,
  STATUS_LABELS,
  STATUS_OPTIONS,
  type IssuePriority,
  type IssueStatus,
} from '../types/issue';
import type { StatusCounts } from '../lib/stats';

export interface IssueFilters {
  keyword: string;
  status: IssueStatus | 'all';
  priority: IssuePriority | 'all';
  area: string | 'all';
}

export const EMPTY_FILTERS: IssueFilters = {
  keyword: '',
  status: 'all',
  priority: 'all',
  area: 'all',
};

interface FilterBarProps {
  filters: IssueFilters;
  counts: StatusCounts;
  areas: string[];
  visible: number;
  total: number;
  onChange: (patch: Partial<IssueFilters>) => void;
  onReset: () => void;
}

export default function FilterBar({
  filters,
  counts,
  areas,
  visible,
  total,
  onChange,
  onReset,
}: FilterBarProps) {
  const dirty =
    filters.keyword !== '' || filters.status !== 'all' || filters.priority !== 'all' || filters.area !== 'all';

  return (
    <div className="filters">
      <div className="segmented" role="group" aria-label="按状态筛选">
        <button
          type="button"
          className={filters.status === 'all' ? 'is-active' : undefined}
          aria-pressed={filters.status === 'all'}
          onClick={() => onChange({ status: 'all' })}
        >
          全部<span className="count">{counts.total}</span>
        </button>
        {STATUS_OPTIONS.map((status) => (
          <button
            key={status}
            type="button"
            className={filters.status === status ? 'is-active' : undefined}
            aria-pressed={filters.status === status}
            onClick={() => onChange({ status })}
          >
            {STATUS_LABELS[status]}
            <span className="count">{counts[status]}</span>
          </button>
        ))}
      </div>

      <select
        className="select filters__select"
        aria-label="按优先级筛选"
        value={filters.priority}
        onChange={(event) => onChange({ priority: event.target.value as IssuePriority | 'all' })}
      >
        <option value="all">全部优先级</option>
        {PRIORITY_OPTIONS.map((priority) => (
          <option key={priority} value={priority}>
            {PRIORITY_LABELS[priority]}优先级
          </option>
        ))}
      </select>

      <select
        className="select filters__select"
        aria-label="按区域筛选"
        value={filters.area}
        onChange={(event) => onChange({ area: event.target.value })}
      >
        <option value="all">全部区域</option>
        {areas.map((area) => (
          <option key={area} value={area}>
            {area}
          </option>
        ))}
      </select>

      <div className="filters__spacer" />

      <span className="filters__count" aria-live="polite">
        显示 <strong>{visible}</strong> / {total} 条
      </span>

      {dirty ? (
        <button type="button" className="filters__reset" onClick={onReset}>
          清除筛选
        </button>
      ) : null}
    </div>
  );
}
