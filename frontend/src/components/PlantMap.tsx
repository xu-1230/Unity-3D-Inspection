import {
  PLANT_FRAME,
  PLAN_HEIGHT,
  PLAN_WIDTH,
  INSPECTION_REGIONS,
  toPlanX,
  toPlanY,
  type InspectionRegion,
} from '../lib/regions';
import type { ZoneSummary } from '../lib/stats';
import { STATUS_LABELS, type Issue } from '../types/issue';

interface PlantMapProps {
  zones: ZoneSummary[];
  issues: Issue[];
  hovered: string | null;
  onHover: (id: string | null) => void;
  onSelect: (name: string) => void;
}

const SHELL = { xMin: -20.2, xMax: 20.2, zMin: -14.2, zMax: 14.2 };

/** 设备剪影位置取自 SiteGeometry，仅作空间参照，不参与业务判断。 */
const EQUIPMENT: Array<
  { kind: 'circle'; x: number; z: number; r: number } | { kind: 'rect'; x: number; z: number; w: number; h: number }
> = [
  { kind: 'circle', x: -3, z: 9, r: 2.3 },
  { kind: 'circle', x: 5, z: 9, r: 2.3 },
  { kind: 'rect', x: -15.4, z: 5.2, w: 3.4, h: 1.5 },
  { kind: 'rect', x: -15.4, z: 1.8, w: 3.4, h: 1.5 },
  { kind: 'rect', x: 14.25, z: 0, w: 1.7, h: 10.4 },
  { kind: 'rect', x: 2, z: -9.5, w: 1.7, h: 0.9 },
  { kind: 'rect', x: 4, z: -9.5, w: 1.7, h: 0.9 },
  { kind: 'rect', x: 6, z: -9.5, w: 1.7, h: 0.9 },
  { kind: 'rect', x: 8, z: -9.5, w: 1.7, h: 0.9 },
  { kind: 'rect', x: 16, z: -8.6, w: 3.4, h: 1.4 },
];

function regionRect(region: InspectionRegion) {
  const { xMin, xMax, zMin, zMax } = region.bounds;
  return {
    x: toPlanX(xMin),
    y: toPlanY(zMax),
    w: (xMax - xMin) * PLANT_FRAME.scale,
    h: (zMax - zMin) * PLANT_FRAME.scale,
  };
}

export default function PlantMap({ zones, issues, hovered, onHover, onSelect }: PlantMapProps) {
  const byId = new Map(zones.map((zone) => [zone.id, zone]));
  const plottable = issues.filter((issue) => {
    const { x, z } = issue.location;
    return x >= PLANT_FRAME.minX && x <= PLANT_FRAME.maxX && z >= PLANT_FRAME.minZ && z <= PLANT_FRAME.maxZ;
  });

  return (
    <svg
      className="plant"
      viewBox={`0 0 ${PLAN_WIDTH} ${PLAN_HEIGHT}`}
      role="img"
      aria-label="厂区固定区域平面图，点击区域可筛选工单"
    >
      <defs>
        <pattern id="plant-grid" width="10" height="10" patternUnits="userSpaceOnUse">
          <path d="M10 0H0V10" fill="none" stroke="rgba(255,255,255,0.045)" strokeWidth="1" />
        </pattern>
      </defs>

      <rect width={PLAN_WIDTH} height={PLAN_HEIGHT} fill="url(#plant-grid)" />

      <rect
        className="plant__shell"
        x={toPlanX(SHELL.xMin)}
        y={toPlanY(SHELL.zMax)}
        width={(SHELL.xMax - SHELL.xMin) * PLANT_FRAME.scale}
        height={(SHELL.zMax - SHELL.zMin) * PLANT_FRAME.scale}
        rx="4"
      />

      {/* 主通道 */}
      <line
        className="plant__aisle"
        x1={toPlanX(-7.5)}
        y1={toPlanY(13)}
        x2={toPlanX(-7.5)}
        y2={toPlanY(-13)}
      />
      <line className="plant__aisle" x1={toPlanX(0.5)} y1={toPlanY(13)} x2={toPlanX(0.5)} y2={toPlanY(-13)} />
      <line
        className="plant__aisle plant__aisle--dim"
        x1={toPlanX(-19)}
        y1={toPlanY(-12.3)}
        x2={toPlanX(15)}
        y2={toPlanY(-12.3)}
      />

      {/* 设备剪影 */}
      {EQUIPMENT.map((item, index) =>
        item.kind === 'circle' ? (
          <circle
            key={`eq-${index}`}
            className="plant__equipment"
            cx={toPlanX(item.x)}
            cy={toPlanY(item.z)}
            r={item.r * PLANT_FRAME.scale}
          />
        ) : (
          <rect
            key={`eq-${index}`}
            className="plant__equipment"
            x={toPlanX(item.x - item.w / 2)}
            y={toPlanY(item.z + item.h / 2)}
            width={item.w * PLANT_FRAME.scale}
            height={item.h * PLANT_FRAME.scale}
            rx="2"
          />
        ),
      )}

      {/* 固定区域 */}
      {INSPECTION_REGIONS.map((region) => {
        const zone = byId.get(region.id);
        const counts = zone?.counts ?? { total: 0, open: 0, in_progress: 0, resolved: 0 };
        const rect = regionRect(region);
        const cx = rect.x + rect.w / 2;
        const cy = rect.y + rect.h / 2;
        const vertical = rect.h > rect.w * 1.5;
        const nameBelow = rect.h >= 60 && !vertical;
        const state =
          counts.open + counts.in_progress > 0 ? 'is-open' : counts.total > 0 ? 'is-done' : 'is-idle';

        return (
          <g
            key={region.id}
            className={`plant-zone ${state}${hovered === region.id ? ' is-hover' : ''}`}
            tabIndex={0}
            role="button"
            aria-label={`${region.name}，${counts.total} 条问题，点击筛选`}
            onClick={() => onSelect(region.name)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                onSelect(region.name);
              }
            }}
            onMouseEnter={() => onHover(region.id)}
            onMouseLeave={() => onHover(null)}
            onFocus={() => onHover(region.id)}
            onBlur={() => onHover(null)}
          >
            <title>{`${region.name} · ${counts.total} 条问题`}</title>
            <rect className="plant-zone__frame" x={rect.x} y={rect.y} width={rect.w} height={rect.h} rx="5" />

            <text
              className="plant-zone__name"
              x={cx}
              y={nameBelow ? cy + 26 : rect.y - 8}
              textAnchor="middle"
            >
              {region.name}
            </text>

            <g className="plant-zone__badge" transform={`translate(${cx}, ${cy})`}>
              <circle r="11" />
              <text textAnchor="middle" dominantBaseline="central">
                {counts.total}
              </text>
            </g>
          </g>
        );
      })}

      {/* 上报点位置，颜色对应处理状态 */}
      <g className="plant__issues">
        {plottable.map((issue) => (
          <circle
            key={issue.id}
            className={`plant__issue plant__issue--${issue.status}`}
            cx={toPlanX(issue.location.x)}
            cy={toPlanY(issue.location.z)}
            r="3"
          >
            <title>{`${issue.title} · ${STATUS_LABELS[issue.status]}`}</title>
          </circle>
        ))}
      </g>
    </svg>
  );
}
