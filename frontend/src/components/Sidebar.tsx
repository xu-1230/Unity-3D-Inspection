import type { StatusCounts } from '../lib/stats';
import type { ViewKey } from '../types/view';
import { IconCube, IconLayers, IconList, IconOverview } from './Icons';

export type { ViewKey };

export type ConnectionState = 'loading' | 'online' | 'offline';

interface SidebarProps {
  view: ViewKey;
  connection: ConnectionState;
  counts: StatusCounts;
  areaCount: number;
  endpoint: string;
  mobileOpen: boolean;
  onNavigate: (view: ViewKey) => void;
  onCloseMobile: () => void;
}

const CONNECTION_TEXT: Record<ConnectionState, string> = {
  loading: '正在连接后端',
  online: '后端已连接',
  offline: '后端未连接',
};

export default function Sidebar({
  view,
  connection,
  counts,
  areaCount,
  endpoint,
  mobileOpen,
  onNavigate,
  onCloseMobile,
}: SidebarProps) {
  const unresolved = counts.open + counts.in_progress;

  const go = (next: ViewKey) => {
    onNavigate(next);
    onCloseMobile();
  };

  return (
    <>
      <aside className={`sidebar${mobileOpen ? ' is-open' : ''}`} aria-label="主导航">
        <div className="brand">
          <span className="brand__mark" aria-hidden="true">
            <IconCube />
          </span>
          <span>
            <span className="brand__name">INSPECT / 3D</span>
            <span className="brand__sub">巡检中台</span>
          </span>
        </div>

        <nav className="nav">
          <p className="nav__label">工作台</p>
          <button
            type="button"
            className={`nav__item${view === 'overview' ? ' is-active' : ''}`}
            aria-current={view === 'overview' ? 'page' : undefined}
            onClick={() => go('overview')}
          >
            <IconOverview />
            巡检态势
          </button>
          <button
            type="button"
            className={`nav__item${view === 'issues' ? ' is-active' : ''}`}
            aria-current={view === 'issues' ? 'page' : undefined}
            onClick={() => go('issues')}
          >
            <IconList />
            问题与工单
            <span className="nav__count">{unresolved}</span>
          </button>
          <button
            type="button"
            className={`nav__item${view === 'areas' ? ' is-active' : ''}`}
            aria-current={view === 'areas' ? 'page' : undefined}
            onClick={() => go('areas')}
          >
            <IconLayers />
            区域分布
            <span className="nav__count">{areaCount}</span>
          </button>
        </nav>

        <div className="sidebar__foot">
          <div className="link-card">
            <div className="link-card__row">
              <span className={`status-dot status-dot--${connection}`} aria-hidden="true" />
              {CONNECTION_TEXT[connection]}
            </div>
            <div className="link-card__endpoint" title={endpoint}>
              {endpoint}
            </div>
          </div>

          <div className="user-chip">
            <span className="user-chip__avatar" aria-hidden="true">
              IN
            </span>
            <span>
              <span className="user-chip__name">巡检管理员</span>
              <span className="user-chip__role">本地会话 · 无需登录</span>
            </span>
          </div>

          <p className="sidebar__version">v0.2 · Unity → Go → Web</p>
        </div>
      </aside>

      <div
        className={`sidebar-scrim${mobileOpen ? ' is-open' : ''}`}
        role="presentation"
        onClick={onCloseMobile}
      />
    </>
  );
}
