import type { Issue } from '../types/issue';
import { zoneName } from './regions';

/** 未提供可选字段时的占位文案，列表与详情共用。 */
export const UNASSIGNED_DEVICE = '未标注设备';

/** 把任意异常收敛成可展示的文案。 */
export function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

function pad(value: number): string {
  return value < 10 ? `0${value}` : String(value);
}

function parseDate(value: string): Date | null {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

/** 绝对时间，本地时区，形如 2026-10-31 19:04。 */
export function formatTime(value: string): string {
  const parsed = parseDate(value);
  if (!parsed) {
    return value || '—';
  }
  return `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())} ${pad(parsed.getHours())}:${pad(
    parsed.getMinutes(),
  )}`;
}

/** 相对时间，用于列表里的轻量表达。 */
export function formatRelative(value: string, now: number = Date.now()): string {
  const parsed = parseDate(value);
  if (!parsed) {
    return value || '—';
  }

  const diff = now - parsed.getTime();
  if (diff < 0) {
    return formatTime(value);
  }
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 1) {
    return '刚刚';
  }
  if (minutes < 60) {
    return `${minutes} 分钟前`;
  }
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return `${hours} 小时前`;
  }
  const days = Math.floor(hours / 24);
  if (days < 8) {
    return `${days} 天前`;
  }
  return `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}`;
}

/** 坐标原样输出，不做换算、取整或重排。 */
export function formatAxis(value: number): string {
  return Number.isFinite(value) ? String(value) : '—';
}

/** 短标识，仅用于列表展示；详情抽屉给出完整值。 */
export function shortId(id: string): string {
  if (!id) {
    return '未同步';
  }
  return id.slice(0, 8);
}

export function deviceName(issue: Issue): string {
  const value = issue.deviceId?.trim();
  return value ? value : UNASSIGNED_DEVICE;
}

/** 区域展示名：命中固定区域用区域名，框外记录统一显示「厂区其他」。 */
export function areaName(issue: Issue): string {
  return zoneName(issue);
}

/** 百分比取整，仅用于展示进度。 */
export function percent(part: number, total: number): number {
  if (total <= 0) {
    return 0;
  }
  return Math.round((part / total) * 100);
}
