import { apiRequest } from './client';
import type { Issue, IssueStatus } from '../types/issue';

/** 拉取全部问题，顺序由后端保证（创建时间倒序）。 */
export function listIssues(): Promise<Issue[]> {
  return apiRequest<Issue[]>('/api/issues');
}

/** 修改指定问题的处理状态，返回后端更新后的完整对象。 */
export function updateIssueStatus(id: string, status: IssueStatus): Promise<Issue> {
  return apiRequest<Issue>(`/api/issues/${encodeURIComponent(id)}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  });
}

/** 删除指定问题，后端成功时返回 204。 */
export function deleteIssue(id: string): Promise<void> {
  return apiRequest<void>(`/api/issues/${encodeURIComponent(id)}`, { method: 'DELETE' });
}
