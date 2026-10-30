import { useCallback, useEffect, useState } from 'react';
import EmptyState from '../components/EmptyState';
import ErrorNotice from '../components/ErrorNotice';
import IssueTable from '../components/IssueTable';
import { listIssues, updateIssueStatus } from '../api/issues';
import type { Issue, IssueStatus } from '../types/issue';

type ViewState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; issues: Issue[] };

function describe(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

export default function IssueListPage() {
  const [state, setState] = useState<ViewState>({ kind: 'loading' });
  const [busyId, setBusyId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    setActionError(null);
    try {
      const issues = await listIssues();
      setState({ kind: 'ready', issues });
    } catch (error) {
      setState({ kind: 'error', message: describe(error) });
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const changeStatus = async (issue: Issue, next: IssueStatus) => {
    if (next === issue.status) {
      return;
    }
    setBusyId(issue.id);
    setActionError(null);
    try {
      const updated = await updateIssueStatus(issue.id, next);
      setState((previous) =>
        previous.kind === 'ready'
          ? {
              kind: 'ready',
              issues: previous.issues.map((item) => (item.id === updated.id ? updated : item)),
            }
          : previous,
      );
    } catch (error) {
      setActionError(`状态修改失败：${describe(error)}`);
    } finally {
      setBusyId(null);
    }
  };

  return (
    <section>
      <header
        style={{
          alignItems: 'center',
          display: 'flex',
          gap: 12,
          justifyContent: 'space-between',
          marginBottom: 16,
        }}
      >
        <div>
          <h2 style={{ margin: 0 }}>问题列表</h2>
          {state.kind === 'ready' ? (
            <p style={{ color: '#5f6368', margin: '4px 0 0' }}>共 {state.issues.length} 条</p>
          ) : null}
        </div>
        <button type="button" onClick={() => void load()} disabled={state.kind === 'loading'}>
          刷新
        </button>
      </header>

      {actionError ? (
        <p
          role="status"
          style={{
            background: '#fef7e0',
            border: '1px solid #f9ab00',
            borderRadius: 8,
            color: '#7a4f01',
            padding: '10px 12px',
          }}
        >
          {actionError}
        </p>
      ) : null}

      {state.kind === 'loading' ? <p>加载中…</p> : null}

      {state.kind === 'error' ? (
        <ErrorNotice message={state.message} onRetry={() => void load()} />
      ) : null}

      {state.kind === 'ready' && state.issues.length === 0 ? <EmptyState /> : null}

      {state.kind === 'ready' && state.issues.length > 0 ? (
        <IssueTable
          issues={state.issues}
          busyId={busyId}
          onStatusChange={(issue, next) => void changeStatus(issue, next)}
        />
      ) : null}
    </section>
  );
}
