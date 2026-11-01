import type { Issue } from '../types/issue';

/**
 * 场景固定区域。范围来自 Unity 场景（SiteGeometry）的设备基础与地面框线，
 * 单位为场景世界坐标：X 向东增大，Z 向北增大。
 * 修改时需同步 unity/Assets/Scripts/Core/InspectionRegions.cs 与 docs/需求规格说明书 6.4。
 */
export interface RegionBounds {
  xMin: number;
  xMax: number;
  zMin: number;
  zMax: number;
}

export interface InspectionRegion {
  id: string;
  /** 平面图与列表中的固定序号，从 1 开始。 */
  order: number;
  name: string;
  subtitle: string;
  bounds: RegionBounds;
}

/** 落在 5 个区域框以外的记录统一归入此分组。 */
export const OUTSIDE_ZONE_NAME = '厂区其他';

export const INSPECTION_REGIONS: readonly InspectionRegion[] = [
  {
    id: 'pump',
    order: 1,
    name: '泵区',
    subtitle: 'P-101A / P-101B 泵组与出口集管',
    bounds: { xMin: -19.5, xMax: -9.2, zMin: -0.4, zMax: 7.2 },
  },
  {
    id: 'tank',
    order: 2,
    name: '储罐区',
    subtitle: 'T-201 / T-202 储罐与罐间管线',
    bounds: { xMin: -6.4, xMax: 8.4, zMin: 5, zMax: 12.8 },
  },
  {
    id: 'conveyor',
    order: 3,
    name: '输送区',
    subtitle: '输送带、端部滚筒与驱动电机',
    bounds: { xMin: 12.2, xMax: 16.4, zMin: -6, zMax: 6 },
  },
  {
    id: 'switchgear',
    order: 4,
    name: '配电区',
    subtitle: '低压配电柜与电缆桥架',
    bounds: { xMin: -0.4, xMax: 10.4, zMin: -11, zMax: -8 },
  },
  {
    id: 'control',
    order: 5,
    name: '控制区',
    subtitle: '控制台、大屏与值班座椅',
    bounds: { xMin: 13, xMax: 19, zMin: -12.6, zMax: -7.4 },
  },
];

/** 平面图视图框：比厂房略大一圈，1 米对应 10 个 SVG 单位。 */
export const PLANT_FRAME = {
  minX: -22,
  maxX: 22,
  minZ: -16,
  maxZ: 16,
  scale: 10,
} as const;

export const PLAN_WIDTH = (PLANT_FRAME.maxX - PLANT_FRAME.minX) * PLANT_FRAME.scale;
export const PLAN_HEIGHT = (PLANT_FRAME.maxZ - PLANT_FRAME.minZ) * PLANT_FRAME.scale;

export function toPlanX(x: number): number {
  return (x - PLANT_FRAME.minX) * PLANT_FRAME.scale;
}

export function toPlanY(z: number): number {
  return (PLANT_FRAME.maxZ - z) * PLANT_FRAME.scale;
}

export function regionByName(name: string | undefined): InspectionRegion | undefined {
  const trimmed = (name ?? '').trim();
  if (trimmed === '') {
    return undefined;
  }
  return INSPECTION_REGIONS.find((region) => region.name === trimmed);
}

export function regionOfPoint(x: number, z: number): InspectionRegion | undefined {
  if (!Number.isFinite(x) || !Number.isFinite(z)) {
    return undefined;
  }
  return INSPECTION_REGIONS.find(
    (region) =>
      x >= region.bounds.xMin && x <= region.bounds.xMax && z >= region.bounds.zMin && z <= region.bounds.zMax,
  );
}

/**
 * 区域归属：优先采用上报方写入的固定区域名；旧数据或未带区域的记录按命中点坐标落框判定。
 * 两者都不命中时返回 undefined，由调用方归入「厂区其他」。
 */
export function resolveRegion(issue: Issue): InspectionRegion | undefined {
  const byName = regionByName(issue.area);
  if (byName) {
    return byName;
  }
  return regionOfPoint(issue.location.x, issue.location.z);
}

export function zoneName(issue: Issue): string {
  return resolveRegion(issue)?.name ?? OUTSIDE_ZONE_NAME;
}
