import type { CSSProperties } from 'react';
import PriorityTag from './PriorityTag';
import StatusPill from './StatusPill';
import { IconChevronRight } from './Icons';
import { areaName, deviceName, formatRelative, formatTime } from '../lib/format';
import type { Issue, IssuePriority } from '../types/issue';

interface IssueListProps {
  issues: Issue[];
  onOpen: (issue: Issue) => void;
}

const TONES: Record<IssuePriority, string> = {
  high: 'var(--high)',
  medium: 'var(--medium)',
  low: 'var(--low)',
};

export default function IssueList({ issues, onOpen }: IssueListProps) {
  return (
    <div className="issue-list">
      <div className="issue-list__head" role="row">
        <span aria-hidden="true" />
        <span role="columnheader">问题</span>
        <span role="columnheader">区域 / 设备</span>
        <span role="columnheader">状态</span>
        <span role="columnheader">更新</span>
        <span aria-hidden="true" />
      </div>

      {issues.map((issue, index) => (
        <div
          key={issue.id}
          className="issue-row"
          style={{ '--tone': TONES[issue.priority], animationDelay: `${Math.min(index, 8) * 32}ms` } as CSSProperties}
          onClick={() => onOpen(issue)}
        >
          <span className="issue-row__meter" aria-hidden="true" />

          <div>
            <button
              type="button"
              className="issue-row__title"
              onClick={(event) => {
                event.stopPropagation();
                onOpen(issue);
              }}
            >
              {issue.title}
            </button>
            <p className="issue-row__desc">{issue.description.trim() === '' ? '（无描述）' : issue.description}</p>
            <div className="issue-row__tags">
              <PriorityTag priority={issue.priority} />
            </div>
          </div>

          <div className="issue-row__where">
            <b>{areaName(issue)}</b>
            <span>{deviceName(issue)}</span>
          </div>

          <div className="issue-row__status">
            <StatusPill status={issue.status} />
          </div>

          <div className="issue-row__time" title={formatTime(issue.updatedAt)}>
            {formatRelative(issue.updatedAt)}
          </div>

          <button
            type="button"
            className="issue-row__go"
            aria-label={`查看「${issue.title}」详情`}
            onClick={(event) => {
              event.stopPropagation();
              onOpen(issue);
            }}
          >
            <IconChevronRight />
          </button>
        </div>
      ))}

      <div className="list-foot">
        <span>共 {issues.length} 条 · 按上报时间倒序</span>
        <span>点击任意行查看详情并修改状态</span>
      </div>
    </div>
  );
}
