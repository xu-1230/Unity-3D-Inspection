import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import AreasView from '../components/AreasView';
import ConfirmDialog from '../components/ConfirmDialog';
import EmptyState from '../components/EmptyState';
import ErrorNotice from '../components/ErrorNotice';
import FilterBar, { EMPTY_FILTERS, type IssueFilters } from '../components/FilterBar';
import IssueDrawer from '../components/IssueDrawer';
import IssueList from '../components/IssueList';
import OverviewView from '../components/OverviewView';
import Sidebar, { type ConnectionState } from '../components/Sidebar';
import { IssueListSkeleton, OverviewSkeleton } from '../components/Skeleton';
import ToastStack, { type ToastItem } from '../components/ToastStack';
import Topbar from '../components/Topbar';
import { API_DISPLAY_TARGET } from '../api/client';
import { deleteIssue, listIssues, updateIssueStatus } from '../api/issues';
import { describeError, formatTime } from '../lib/format';
import { countPriorities, countStatuses, filterIssues, resolutionRate, summarizeZones } from '../lib/stats';
import type { Issue, IssueStatus } from '../types/issue';
import type { ViewKey } from '../types/view';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; issues: Issue[] };

interface ViewMeta {
  title: string;
  subtitle: string;
}

const TOAST_DURATION_MS = 4200;

export default function DashboardPage() {
  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [view, setView] = useState<ViewKey>('overview');
  const [filters, setFilters] = useState<IssueFilters>(EMPTY_FILTERS);
  const [refreshing, setRefreshing] = useState(false);
  const [lastSync, setLastSync] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [drawerId, setDrawerId] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<Issue | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const toastSeq = useRef(0);

  const notifyError = useCallback((message: string) => {
    toastSeq.current += 1;
    const id = toastSeq.current;
    setToasts((previous) => [...previous, { id, message }]);
    window.setTimeout(() => {
      setToasts((previous) => previous.filter((item) => item.id !== id));
    }, TOAST_DURATION_MS);
  }, []);

  const load = useCallback(async () => {
    setRefreshing(true);
    try {
      const issues = await listIssues();
      setState({ kind: 'ready', issues });
      setLastSync(new Date().toISOString());
    } catch (error) {
      setState({ kind: 'error', message: describeError(error) });
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const issues = state.kind === 'ready' ? state.issues : [];
  const counts = useMemo(() => countStatuses(issues), [issues]);
  const priorities = useMemo(() => countPriorities(issues), [issues]);
  const zones = useMemo(() => summarizeZones(issues), [issues]);
  const visible = useMemo(() => filterIssues(issues, filters), [issues, filters]);

  const activeIssue = useMemo(
    () => (drawerId === null ? null : issues.find((issue) => issue.id === drawerId) ?? null),
    [drawerId, issues],
  );

  const changeStatus = async (issue: Issue, next: IssueStatus) => {
    if (next === issue.status || busyId !== null) {
      return;
    }
    setBusyId(issue.id);
    try {
      const updated = await updateIssueStatus(issue.id, next);
      setState((previous) =>
        previous.kind === 'ready'
          ? { kind: 'ready', issues: previous.issues.map((item) => (item.id === updated.id ? updated : item)) }
          : previous,
      );
    } catch (error) {
      notifyError(`状态修改失败：${describeError(error)}`);
    } finally {
      setBusyId(null);
    }
  };

  const confirmDelete = async () => {
    if (!pendingDelete) {
      return;
    }
    const target = pendingDelete;
    setDeleting(true);
    try {
      await deleteIssue(target.id);
      setState((previous) =>
        previous.kind === 'ready'
          ? { kind: 'ready', issues: previous.issues.filter((item) => item.id !== target.id) }
          : previous,
      );
      setDrawerId((current) => (current === target.id ? null : current));
      setPendingDelete(null);
    } catch (error) {
      notifyError(`删除失败：${describeError(error)}`);
    } finally {
      setDeleting(false);
    }
  };

  const handleQueryChange = (value: string) => {
    setFilters((previous) => ({ ...previous, keyword: value }));
    if (value !== '' && view !== 'issues') {
      setView('issues');
    }
  };

  const handleSelectArea = (area: string) => {
    setFilters({ ...EMPTY_FILTERS, area });
    setView('issues');
  };

  const resetFilters = () => setFilters(EMPTY_FILTERS);

  const connection: ConnectionState =
    state.kind === 'ready' ? 'online' : state.kind === 'error' ? 'offline' : 'loading';

  const fixedZoneCount = zones.filter((zone) => zone.fixed).length;

  const meta = useMemo<ViewMeta>(() => {
    if (view === 'issues') {
      return {
        title: '问题与工单',
        subtitle:
          state.kind === 'ready'
            ? `${visible.length} / ${counts.total} 条 · 按上报时间倒序`
            : '从 Go 后端读取巡检上报',
      };
    }
    if (view === 'areas') {
      return {
        title: '区域分布',
        subtitle:
          state.kind === 'ready'
            ? `${fixedZoneCount} 个固定区域 · ${counts.total} 条问题`
            : '5 个固定区域 · 按命中点归属',
      };
    }
    return {
      title: '巡检态势',
      subtitle:
        state.kind === 'ready'
          ? `待处置 ${counts.open + counts.in_progress} 条 · 已解决率 ${resolutionRate(counts)}%`
          : 'Unity 现场采集 · Go 持久化 · Web 处置',
    };
  }, [view, state.kind, visible.length, counts, fixedZoneCount]);

  return (
    <div className="app">
      <Sidebar
        view={view}
        connection={connection}
        counts={counts}
        areaCount={fixedZoneCount}
        endpoint={API_DISPLAY_TARGET}
        mobileOpen={mobileNavOpen}
        onNavigate={setView}
        onCloseMobile={() => setMobileNavOpen(false)}
      />

      <div className="main">
        <Topbar
          title={meta.title}
          subtitle={lastSync ? `${meta.subtitle} · 同步于 ${formatTime(lastSync)}` : meta.subtitle}
          query={filters.keyword}
          refreshing={refreshing}
          onQueryChange={handleQueryChange}
          onRefresh={() => void load()}
          onOpenMobileNav={() => setMobileNavOpen(true)}
        />

        {state.kind === 'loading' ? (
          view === 'overview' ? (
            <OverviewSkeleton />
          ) : (
            <div className="view">
              <IssueListSkeleton />
            </div>
          )
        ) : null}

        {state.kind === 'error' ? (
          <div className="view">
            <ErrorNotice message={state.message} endpoint={API_DISPLAY_TARGET} onRetry={() => void load()} />
          </div>
        ) : null}

        {state.kind === 'ready' && view === 'overview' ? (
          <OverviewView
            issues={issues}
            counts={counts}
            priorities={priorities}
            zones={zones}
            onOpen={(issue) => setDrawerId(issue.id)}
            onNavigate={setView}
          />
        ) : null}

        {state.kind === 'ready' && view === 'issues' ? (
          <div className="view">
            <FilterBar
              filters={filters}
              counts={counts}
              areas={zones.map((zone) => zone.name)}
              visible={visible.length}
              total={counts.total}
              onChange={(patch) => setFilters((previous) => ({ ...previous, ...patch }))}
              onReset={resetFilters}
            />

            {visible.length === 0 ? (
              <div className="panel">
                <EmptyState
                  title={counts.total === 0 ? '暂无问题' : '没有符合筛选条件的问题'}
                  description={
                    counts.total === 0
                      ? '在 Unity 场景中点击设备表面并上报，问题会出现在这里。'
                      : '尝试切换状态、优先级或区域，也可以清除筛选查看全部问题。'
                  }
                  action={
                    counts.total === 0 ? (
                      <button type="button" className="btn btn--sm" onClick={() => void load()}>
                        重新加载
                      </button>
                    ) : (
                      <button type="button" className="btn btn--sm" onClick={resetFilters}>
                        清除筛选
                      </button>
                    )
                  }
                />
              </div>
            ) : (
              <IssueList issues={visible} onOpen={(issue) => setDrawerId(issue.id)} />
            )}
          </div>
        ) : null}

        {state.kind === 'ready' && view === 'areas' ? (
          <AreasView zones={zones} issues={issues} total={counts.total} onSelectArea={handleSelectArea} />
        ) : null}
      </div>

      {activeIssue ? (
        <IssueDrawer
          issue={activeIssue}
          busy={busyId === activeIssue.id || deleting}
          escEnabled={pendingDelete === null}
          onClose={() => setDrawerId(null)}
          onStatusChange={(next) => void changeStatus(activeIssue, next)}
          onRequestDelete={() => setPendingDelete(activeIssue)}
        />
      ) : null}

      {pendingDelete ? (
        <ConfirmDialog
          title="删除这条问题记录？"
          description={`「${pendingDelete.title}」将从后端 JSON 数据中移除，无法撤销。Unity 客户端的标记会在下次刷新后消失。`}
          confirmLabel="确认删除"
          busy={deleting}
          onCancel={() => setPendingDelete(null)}
          onConfirm={() => void confirmDelete()}
        />
      ) : null}

      <ToastStack
        items={toasts}
        onDismiss={(id) => setToasts((previous) => previous.filter((item) => item.id !== id))}
      />
    </div>
  );
}
