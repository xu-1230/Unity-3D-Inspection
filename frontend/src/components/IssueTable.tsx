import StatusSelect from './StatusSelect';
import {
  PRIORITY_LABELS,
  STATUS_LABELS,
  type Issue,
  type IssueStatus,
} from '../types/issue';

interface IssueTableProps {
  issues: Issue[];
  busyId: string | null;
  onStatusChange: (issue: Issue, next: IssueStatus) => void;
}

function formatLocation(issue: Issue): string {
  const { x, y, z } = issue.location;
  return `(${x.toFixed(2)}, ${y.toFixed(2)}, ${z.toFixed(2)})`;
}

function formatTime(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }
  return parsed.toLocaleString('zh-CN', { hour12: false });
}

const cellStyle = {
  borderBottom: '1px solid #e0e0e0',
  padding: '10px 12px',
  textAlign: 'left' as const,
  verticalAlign: 'middle' as const,
};

export default function IssueTable({ issues, busyId, onStatusChange }: IssueTableProps) {
  return (
    <table style={{ borderCollapse: 'collapse', width: '100%' }}>
      <thead>
        <tr style={{ background: '#f1f3f4' }}>
          <th style={cellStyle}>标题</th>
          <th style={cellStyle}>优先级</th>
          <th style={cellStyle}>状态</th>
          <th style={cellStyle}>坐标</th>
          <th style={cellStyle}>更新时间</th>
          <th style={cellStyle}>操作</th>
        </tr>
      </thead>
      <tbody>
        {issues.map((issue) => (
          <tr key={issue.id}>
            <td style={cellStyle}>
              <div>{issue.title}</div>
              <div style={{ color: '#5f6368', fontSize: 12 }}>
                {issue.description || '（无描述）'}
              </div>
            </td>
            <td style={cellStyle}>{PRIORITY_LABELS[issue.priority]}</td>
            <td style={cellStyle}>{STATUS_LABELS[issue.status]}</td>
            <td style={{ ...cellStyle, fontFamily: 'monospace', fontSize: 13 }}>
              {formatLocation(issue)}
            </td>
            <td style={{ ...cellStyle, fontSize: 13 }}>{formatTime(issue.updatedAt)}</td>
            <td style={cellStyle}>
              <StatusSelect
                value={issue.status}
                disabled={busyId === issue.id}
                onChange={(next) => onStatusChange(issue, next)}
              />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
