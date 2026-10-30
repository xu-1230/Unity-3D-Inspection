import { STATUS_LABELS, STATUS_OPTIONS, type IssueStatus } from '../types/issue';

interface StatusSelectProps {
  value: IssueStatus;
  disabled: boolean;
  onChange: (next: IssueStatus) => void;
}

export default function StatusSelect({ value, disabled, onChange }: StatusSelectProps) {
  return (
    <select
      aria-label="修改处理状态"
      value={value}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value as IssueStatus)}
    >
      {STATUS_OPTIONS.map((option) => (
        <option key={option} value={option}>
          {STATUS_LABELS[option]}
        </option>
      ))}
    </select>
  );
}
