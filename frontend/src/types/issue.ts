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
