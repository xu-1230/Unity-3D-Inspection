import type { CSSProperties } from 'react';
import AnimatedNumber from './AnimatedNumber';
import EmptyState from './EmptyState';
import PriorityTag from './PriorityTag';
import StatusPill from './StatusPill';
import { IconChevronRight } from './Icons';
import { percent, areaName, formatRelative } from '../lib/format';
import { resolutionRate, unresolvedCount, type StatusCounts, type ZoneSummary } from '../lib/stats';
import type { Issue, IssuePriority } from '../types/issue';
import { PRIORITY_LABELS } from '../types/issue';
import type { ViewKey } from '../types/view';

interface OverviewViewProps {
  issues: Issue[];
  counts: StatusCounts;
  priorities: Record<IssuePriority, number>;
  zones: ZoneSummary[];
  onOpen: (issue: Issue) => void;
  onNavigate: (view: ViewKey) => void;
}

const RING_RADIUS = 58;
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS;

const PRIORITY_TONES: Record<IssuePriority, string> = {
  high: 'var(--high)',
  medium: 'var(--medium)',
  low: 'var(--low)',
};

export default function OverviewView({ issues, counts, priorities, zones, onOpen, onNavigate }: OverviewViewProps) {
  if (counts.total === 0) {
    return (
      <div className="view">
        <div className="panel">
          <EmptyState
            title="还没有任何巡检上报"
            description="在 Unity 场景中点击设备表面，填写标题与优先级后提交，问题会实时出现在这里。"
          />
        </div>
      </div>
    );
  }

  const unresolved = unresolvedCount(counts);
  const rate = resolutionRate(counts);
  const recent = issues.slice(0, 5);
  const coveredZones = zones.filter((zone) => zone.fixed && zone.counts.total > 0).length;
  const rankedZones = [...zones]
    .filter((zone) => zone.counts.total > 0)
    .sort(
      (a, b) =>
        unresolvedCount(b.counts) - unresolvedCount(a.counts) || b.counts.total - a.counts.total,
    )
    .slice(0, 4);
  const maxZone = Math.max(...rankedZones.map((zone) => zone.counts.total), 1);

  const kpis: Array<{ label: string; value: number; tone: string; foot: string; ratio: number }> = [
    {
      label: '全部问题',
      value: counts.total,
      tone: 'var(--accent)',
      foot: `覆盖 ${coveredZones} / 5 个区域`,
      ratio: 100,
    },
    {
      label: '待处理',
      value: counts.open,
      tone: 'var(--open)',
      foot: `占全部 ${percent(counts.open, counts.total)}%`,
      ratio: percent(counts.open, counts.total),
    },
    {
      label: '处理中',
      value: counts.in_progress,
      tone: 'var(--progress)',
      foot: `占全部 ${percent(counts.in_progress, counts.total)}%`,
      ratio: percent(counts.in_progress, counts.total),
    },
    {
      label: '已解决',
      value: counts.resolved,
      tone: 'var(--resolved)',
      foot: `占全部 ${percent(counts.resolved, counts.total)}%`,
      ratio: percent(counts.resolved, counts.total),
    },
  ];

  return (
    <div className="view">
      <section className="hero">
        <div>
          <p className="eyebrow">巡检态势 · 实时快照</p>
          <p className="hero__label" style={{ marginTop: 14 }}>
            待处置问题
          </p>
          <p className="hero__value">
            <AnimatedNumber value={unresolved} />
            <small>条待推进</small>
          </p>
          <div className="hero__meta">
            <span className="tag">共 {counts.total} 条上报</span>
            <span className="tag">已解决 {counts.resolved} 条</span>
            <span className="tag tag--accent">{coveredZones} / 5 个区域</span>
            <span className="tag">{priorities.high} 条高优先级</span>
          </div>
        </div>

        <div className="hero__ring">
          <div className="ring">
            <svg viewBox="0 0 132 132" aria-hidden="true">
              <circle className="ring__track" cx="66" cy="66" r={RING_RADIUS} />
              <circle
                className="ring__value"
                cx="66"
                cy="66"
                r={RING_RADIUS}
                strokeDasharray={RING_CIRCUMFERENCE}
                strokeDashoffset={RING_CIRCUMFERENCE * (1 - rate / 100)}
              />
            </svg>
            <div className="ring__center">
              <strong className="numeric">{rate}%</strong>
              <span>已解决率</span>
            </div>
          </div>
        </div>
      </section>

      <section className="kpi-grid" aria-label="状态概览">
        {kpis.map((kpi) => (
          <article key={kpi.label} className="kpi" style={{ '--tone': kpi.tone } as CSSProperties}>
            <p className="kpi__label">
              <span className="dot" aria-hidden="true" />
              {kpi.label}
            </p>
            <p className="kpi__value">
              <AnimatedNumber value={kpi.value} />
              <small>条</small>
            </p>
            <p className="kpi__foot">{kpi.foot}</p>
            <div className="kpi__bar" aria-hidden="true">
              <span style={{ width: `${kpi.ratio}%` }} />
            </div>
          </article>
        ))}
      </section>

      <section className="split">
        <article className="panel">
          <div className="panel__head">
            <div>
              <h2 className="panel__title">处理进度</h2>
              <p className="panel__sub">状态分布来自后端返回值，不做本地推断</p>
            </div>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => onNavigate('issues')}>
              去处置
              <IconChevronRight size={12} />
            </button>
          </div>
          <div className="panel__body">
            <div className="stack" aria-hidden="true">
              {counts.open > 0 ? (
                <span style={{ width: `${percent(counts.open, counts.total)}%`, background: 'var(--open)' }} />
              ) : null}
              {counts.in_progress > 0 ? (
                <span
                  style={{ width: `${percent(counts.in_progress, counts.total)}%`, background: 'var(--progress)' }}
                />
              ) : null}
              {counts.resolved > 0 ? (
                <span style={{ width: `${percent(counts.resolved, counts.total)}%`, background: 'var(--resolved)' }} />
              ) : null}
            </div>

            <div className="legend">
              {(
                [
                  ['待处理', counts.open, 'var(--open)'],
                  ['处理中', counts.in_progress, 'var(--progress)'],
                  ['已解决', counts.resolved, 'var(--resolved)'],
                ] as const
              ).map(([label, value, tone]) => (
                <div className="legend__row" key={label}>
                  <span className="legend__swatch" style={{ '--tone': tone } as CSSProperties} />
                  {label}
                  <strong>
                    {value}
                    <span style={{ color: 'var(--text-muted)', fontSize: 11 }}> 条 · {percent(value, counts.total)}%</span>
                  </strong>
                </div>
              ))}
            </div>
          </div>
        </article>

        <article className="panel">
          <div className="panel__head">
            <div>
              <h2 className="panel__title">优先级分布</h2>
              <p className="panel__sub">高优先级建议当日响应</p>
            </div>
          </div>
          <div className="panel__body">
            <div className="bars">
              {(['high', 'medium', 'low'] as const).map((priority) => (
                <div className="bars__row" key={priority}>
                  <span>{PRIORITY_LABELS[priority]}优先级</span>
                  <span className="bars__track">
                    <span
                      className="bars__fill"
                      style={
                        {
                          '--tone': PRIORITY_TONES[priority],
                          width: `${percent(priorities[priority], counts.total)}%`,
                        } as CSSProperties
                      }
                    />
                  </span>
                  <span className="bars__value">{priorities[priority]}</span>
                </div>
              ))}
            </div>
            <div style={{ marginTop: 22, display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              {(['high', 'medium', 'low'] as const).map((priority) => (
                <PriorityTag key={priority} priority={priority} />
              ))}
            </div>
          </div>
        </article>
      </section>

      <section className="split">
        <article className="panel">
          <div className="panel__head">
            <div>
              <h2 className="panel__title">区域负载</h2>
              <p className="panel__sub">按未解决问题数量排序</p>
            </div>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => onNavigate('areas')}>
              全部区域
              <IconChevronRight size={12} />
            </button>
          </div>
          <div className="panel__body">
            <div className="bars">
              {rankedZones.map((zone) => (
                <div className="bars__row" key={zone.id} style={{ gridTemplateColumns: '132px minmax(0, 1fr) 34px' }}>
                  <span
                    style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                    title={zone.name}
                  >
                    {zone.name}
                  </span>
                  <span className="bars__track">
                    <span
                      className="bars__fill"
                      style={
                        {
                          '--tone': 'var(--accent)',
                          width: `${percent(zone.counts.total, maxZone)}%`,
                        } as CSSProperties
                      }
                    />
                  </span>
                  <span className="bars__value">{zone.counts.total}</span>
                </div>
              ))}
            </div>
          </div>
        </article>

        <article className="panel">
          <div className="panel__head">
            <div>
              <h2 className="panel__title">最近上报</h2>
              <p className="panel__sub">最新 5 条，点击查看详情</p>
            </div>
          </div>
          <div className="panel__body">
            <div className="recent">
              {recent.map((issue) => (
                <button key={issue.id} type="button" className="recent__item" onClick={() => onOpen(issue)}>
                  <div className="recent__main">
                    <p className="recent__title">{issue.title}</p>
                    <p className="recent__meta">
                      {areaName(issue)} · {formatRelative(issue.updatedAt)}
                    </p>
                  </div>
                  <StatusPill status={issue.status} />
                </button>
              ))}
            </div>
          </div>
        </article>
      </section>
    </div>
  );
}
