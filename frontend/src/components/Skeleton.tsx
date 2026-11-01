import type { CSSProperties } from 'react';

function bar(width: string, extra?: CSSProperties) {
  return <span className="skeleton" style={{ display: 'block', height: 10, width, borderRadius: 999, ...extra }} />;
}

export function IssueListSkeleton() {
  return (
    <div className="issue-list" aria-busy="true" aria-label="正在加载问题列表">
      {[0, 1, 2, 3, 4].map((index) => (
        <div className="skeleton-row" key={index}>
          <span className="skeleton" style={{ display: 'block', height: 34, borderRadius: 999 }} />
          <span style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
            {bar('52%', { height: 13 })}
            {bar('76%')}
            {bar('24%')}
          </span>
          <span style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {bar('70%')}
            {bar('46%')}
          </span>
          {bar('72px', { height: 26, borderRadius: 999 })}
          {bar('48px')}
        </div>
      ))}
    </div>
  );
}

export function OverviewSkeleton() {
  return (
    <div className="view" aria-busy="true" aria-label="正在加载巡检态势">
      <div className="skeleton" style={{ height: 168, borderRadius: 26 }} />
      <div className="kpi-grid">
        {[0, 1, 2, 3].map((index) => (
          <div key={index} className="skeleton" style={{ height: 118, borderRadius: 20 }} />
        ))}
      </div>
      <div className="split">
        <div className="skeleton" style={{ height: 232, borderRadius: 20 }} />
        <div className="skeleton" style={{ height: 232, borderRadius: 20 }} />
      </div>
    </div>
  );
}
