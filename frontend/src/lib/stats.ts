import type { Issue, IssuePriority, IssueStatus } from '../types/issue';
import { areaName, deviceName } from './format';
import { INSPECTION_REGIONS, OUTSIDE_ZONE_NAME, resolveRegion } from './regions';

export interface StatusCounts {
  total: number;
  open: number;
  in_progress: number;
  resolved: number;
}

/** 区域视图的一行：5 个固定区域始终存在，框外记录合并为「厂区其他」。 */
export interface ZoneSummary {
  id: string;
  order: number;
  name: string;
  subtitle: string;
  /** 是否属于固定 5 区域，框外分组为 false。 */
  fixed: boolean;
  counts: StatusCounts;
  /** 该区域实际出现过的设备编号，空白值不参与统计。 */
  devices: string[];
}

/** 一次遍历统计各状态数量。 */
export function countStatuses(issues: Issue[]): StatusCounts {
  const counts: StatusCounts = { total: issues.length, open: 0, in_progress: 0, resolved: 0 };
  for (const issue of issues) {
    counts[issue.status] += 1;
  }
  return counts;
}

export function countPriorities(issues: Issue[]): Record<IssuePriority, number> {
  const counts: Record<IssuePriority, number> = { low: 0, medium: 0, high: 0 };
  for (const issue of issues) {
    counts[issue.priority] += 1;
  }
  return counts;
}

/** 未解决 = 待处理 + 处理中。 */
export function unresolvedCount(counts: StatusCounts): number {
  return counts.open + counts.in_progress;
}

/** 已解决占比，四舍五入到整数百分比。 */
export function resolutionRate(counts: StatusCounts): number {
  if (counts.total === 0) {
    return 0;
  }
  return Math.round((counts.resolved / counts.total) * 100);
}

function collectDevices(issues: Issue[]): string[] {
  const devices: string[] = [];
  for (const issue of issues) {
    const id = issue.deviceId?.trim();
    if (id && !devices.includes(id)) {
      devices.push(id);
    }
  }
  return devices;
}

/**
 * 固定区域汇总：5 个区域无论有没有上报都会返回，保证区域分布是稳定的五项；
 * 命中点落在所有区域框之外的记录追加为「厂区其他」，避免数据从区域视图里消失。
 */
export function summarizeZones(issues: Issue[]): ZoneSummary[] {
  const buckets = new Map<string, Issue[]>();
  for (const region of INSPECTION_REGIONS) {
    buckets.set(region.id, []);
  }

  const outside: Issue[] = [];
  for (const issue of issues) {
    const region = resolveRegion(issue);
    if (region) {
      buckets.get(region.id)?.push(issue);
    } else {
      outside.push(issue);
    }
  }

  const zones: ZoneSummary[] = INSPECTION_REGIONS.map((region) => {
    const bucket = buckets.get(region.id) ?? [];
    return {
      id: region.id,
      order: region.order,
      name: region.name,
      subtitle: region.subtitle,
      fixed: true,
      counts: countStatuses(bucket),
      devices: collectDevices(bucket),
    };
  });

  if (outside.length > 0) {
    zones.push({
      id: 'outside',
      order: INSPECTION_REGIONS.length + 1,
      name: OUTSIDE_ZONE_NAME,
      subtitle: '命中点不在 5 个固定区域框内',
      fixed: false,
      counts: countStatuses(outside),
      devices: collectDevices(outside),
    });
  }

  return zones;
}

/** 按状态与优先级、关键词过滤，保持后端返回的原始顺序。 */
export interface IssueQuery {
  keyword: string;
  status: IssueStatus | 'all';
  priority: IssuePriority | 'all';
  area: string | 'all';
}

export function filterIssues(issues: Issue[], query: IssueQuery): Issue[] {
  const keyword = query.keyword.trim().toLowerCase();
  return issues.filter((issue) => {
    if (query.status !== 'all' && issue.status !== query.status) {
      return false;
    }
    if (query.priority !== 'all' && issue.priority !== query.priority) {
      return false;
    }
    if (query.area !== 'all' && areaName(issue) !== query.area) {
      return false;
    }
    if (keyword === '') {
      return true;
    }
    const haystack = [issue.title, issue.description, deviceName(issue), areaName(issue), issue.id]
      .join(' ')
      .toLowerCase();
    return haystack.includes(keyword);
  });
}
