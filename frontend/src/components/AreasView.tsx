import { useState, type CSSProperties } from 'react';
import PlantMap from './PlantMap';
import { IconChevronRight } from './Icons';
import type { ZoneSummary } from '../lib/stats';
import type { Issue } from '../types/issue';

interface AreasViewProps {
  zones: ZoneSummary[];
  issues: Issue[];
  total: number;
  onSelectArea: (area: string) => void;
}

export default function AreasView({ zones, issues, total, onSelectArea }: AreasViewProps) {
  const [hovered, setHovered] = useState<string | null>(null);
  const fixedZones = zones.filter((zone) => zone.fixed);
  const outside = zones.find((zone) => !zone.fixed);

  return (
    <div className="view">
      <div className="filters">
        <span className="filters__count">
          <strong>{fixedZones.length}</strong> 个固定区域 · 共 <strong>{total}</strong> 条问题
        </span>
        <div className="filters__spacer" />
        <span className="filters__count">区域归属按命中点坐标判定，框外记录归入「厂区其他」</span>
      </div>

      <div className="zone-layout">
        <section className="panel">
          <div className="panel__head">
            <div>
              <h2 className="panel__title">厂区平面 · 固定区域</h2>
              <p className="panel__sub">5 个区域固定划分 · 点击区域框或条目查看工单</p>
            </div>
            <span className="zone-map__axis">上北 · 右东</span>
          </div>
          <div className="panel__body panel__body--flush">
            <PlantMap
              zones={fixedZones}
              issues={issues}
              hovered={hovered}
              onHover={setHovered}
              onSelect={onSelectArea}
            />
          </div>
          <div className="list-foot">
            <span className="map-legend">
              <span>
                <i style={{ background: 'var(--open)' }} />
                待处理
              </span>
              <span>
                <i style={{ background: 'var(--progress)' }} />
                处理中
              </span>
              <span>
                <i style={{ background: 'var(--resolved)' }} />
                已解决
              </span>
            </span>
            <span>{outside ? `另有 ${outside.counts.total} 条框外记录` : '全部记录均落在区域内'}</span>
          </div>
        </section>

        <div className="zone-list" role="list">
          {zones.map((zone) => {
            const unresolved = zone.counts.open + zone.counts.in_progress;
            const tone =
              zone.counts.open > 0
                ? 'var(--open)'
                : zone.counts.in_progress > 0
                  ? 'var(--progress)'
                  : zone.counts.resolved > 0
                    ? 'var(--resolved)'
                    : 'var(--line-strong)';

            return (
              <button
                key={zone.id}
                type="button"
                role="listitem"
                className={`zone-row${hovered === zone.id ? ' is-hover' : ''}${zone.fixed ? '' : ' zone-row--outside'}`}
                style={{ '--zone-tone': tone } as CSSProperties}
                onMouseEnter={() => setHovered(zone.id)}
                onMouseLeave={() => setHovered(null)}
                onFocus={() => setHovered(zone.id)}
                onBlur={() => setHovered(null)}
                onClick={() => onSelectArea(zone.name)}
              >
                <span className="zone-row__index">{zone.fixed ? String(zone.order).padStart(2, '0') : '··'}</span>

                <span className="zone-row__body">
                  <span className="zone-row__title">
                    {zone.name}
                    {zone.fixed ? null : <em className="zone-row__tag">框外</em>}
                  </span>
                  <span className="zone-row__sub">{zone.subtitle}</span>
                  <span className="zone-row__meta">
                    {zone.counts.total === 0
                      ? '暂无上报'
                      : [
                          zone.devices.length > 0 ? `${zone.devices.length} 台设备` : null,
                          `${unresolved} 条未解决`,
                        ]
                          .filter(Boolean)
                          .join(' · ')}
                  </span>
                </span>

                <span className="zone-row__counts">
                  <span>
                    <em style={{ color: 'var(--open)' }}>{zone.counts.open}</em>
                    <i>待处理</i>
                  </span>
                  <span>
                    <em style={{ color: 'var(--progress)' }}>{zone.counts.in_progress}</em>
                    <i>处理中</i>
                  </span>
                  <span>
                    <em style={{ color: 'var(--resolved)' }}>{zone.counts.resolved}</em>
                    <i>已解决</i>
                  </span>
                </span>

                <span className="zone-row__go" aria-hidden="true">
                  <IconChevronRight size={13} />
                </span>
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
