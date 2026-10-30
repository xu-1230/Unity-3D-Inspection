/** 与后端契约对应的类型定义，字段含义见 docs/需求规格说明书.md 第 6 章。 */

export type IssuePriority = 'low' | 'medium' | 'high';

export type IssueStatus = 'open' | 'in_progress' | 'resolved';

export interface IssueLocation {
  x: number;
  y: number;
  z: number;
}

export interface Issue {
  id: string;
  title: string;
  description: string;
  priority: IssuePriority;
  status: IssueStatus;
  location: IssueLocation;
  createdAt: string;
  updatedAt: string;
}

/** 上报/创建时提交的字段；id、status、时间由后端生成。 */
export interface CreateIssueInput {
  title: string;
  description?: string;
  priority: IssuePriority;
  location: IssueLocation;
}

/** 展示文案的唯一映射处，取值顺序与后端枚举一致。 */
export const PRIORITY_LABELS: Record<IssuePriority, string> = {
  low: '低',
  medium: '中',
  high: '高',
};

export const STATUS_LABELS: Record<IssueStatus, string> = {
  open: '待处理',
  in_progress: '处理中',
  resolved: '已解决',
};

export const STATUS_OPTIONS: IssueStatus[] = ['open', 'in_progress', 'resolved'];
