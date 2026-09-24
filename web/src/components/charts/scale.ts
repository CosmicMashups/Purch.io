/** Rounds a positive number up to 1, 2, 2.5, 5 or 10 times a power of ten, so axes end on clean values. */
export function niceCeil(value: number): number {
  if (!(value > 0)) return 1;
  const exponent = Math.floor(Math.log10(value));
  const base = 10 ** exponent;
  const fraction = value / base;
  const step = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10;
  return step * base;
}

/** Evenly spaced tick values from 0 up to the first clean value at or above `max`. */
export function niceTicks(max: number, target = 4): number[] {
  if (!(max > 0)) return [0, 1];
  const step = niceCeil(max / target);
  const ticks: number[] = [];
  for (let v = 0; v < max + step * 0.999; v += step) ticks.push(Number(v.toFixed(10)));
  return ticks;
}

/** Maps a value from a domain onto a range. A flat domain maps everything to the range start. */
export function linear(domain: [number, number], range: [number, number]): (value: number) => number {
  const [d0, d1] = domain;
  const [r0, r1] = range;
  return (value) => (d1 === d0 ? r0 : r0 + ((value - d0) / (d1 - d0)) * (r1 - r0));
}

/** A share as text. Anything above zero but under one percent says so, instead of rounding to nothing. */
export function shareLabel(part: number, total: number): string {
  if (!(total > 0) || !(part > 0)) return '0%';
  const pct = (part / total) * 100;
  return pct < 1 ? '<1%' : `${Math.round(pct)}%`;
}

export interface Slice {
  start: number;
  end: number;
}

/** Splits a full turn into slices proportional to the values, starting at twelve o'clock (angles in radians). */
export function sliceAngles(values: number[]): Slice[] {
  const total = values.reduce((sum, v) => sum + Math.max(0, v), 0);
  if (total <= 0) return values.map(() => ({ start: -Math.PI / 2, end: -Math.PI / 2 }));
  let at = -Math.PI / 2;
  return values.map((v) => {
    const start = at;
    at += (Math.max(0, v) / total) * Math.PI * 2;
    return { start, end: at };
  });
}

const point = (cx: number, cy: number, r: number, angle: number) => `${(cx + r * Math.cos(angle)).toFixed(2)} ${(cy + r * Math.sin(angle)).toFixed(2)}`;

/** An annular sector path. A full-circle slice is drawn as two halves so the arc renders. */
export function arcPath(cx: number, cy: number, outer: number, inner: number, start: number, end: number): string {
  const sweep = end - start;
  if (sweep <= 0) return '';
  if (sweep >= Math.PI * 2 - 1e-6) {
    const mid = start + Math.PI;
    return `${arcPath(cx, cy, outer, inner, start, mid)} ${arcPath(cx, cy, outer, inner, mid, end)}`;
  }
  const large = sweep > Math.PI ? 1 : 0;
  return [
    `M ${point(cx, cy, outer, start)}`,
    `A ${outer} ${outer} 0 ${large} 1 ${point(cx, cy, outer, end)}`,
    `L ${point(cx, cy, inner, end)}`,
    `A ${inner} ${inner} 0 ${large} 0 ${point(cx, cy, inner, start)}`,
    'Z',
  ].join(' ');
}

/** 0 for nothing, then 1..levels by how much of the maximum the value is. Anything above zero is at least level 1. */
export function heatLevel(value: number, max: number, levels = 5): number {
  if (!(value > 0) || !(max > 0)) return 0;
  return Math.min(levels, Math.max(1, Math.ceil((value / max) * levels)));
}

/** Keeps the largest `keep` entries and folds the rest into one "Other" entry, so a chart never needs more colours. */
export function foldTail<T>(items: T[], keep: number, valueOf: (item: T) => number, other: (rest: T[], total: number) => T): T[] {
  if (items.length <= keep) return items;
  const sorted = [...items].sort((a, b) => valueOf(b) - valueOf(a));
  const head = sorted.slice(0, keep);
  const rest = sorted.slice(keep);
  return [...head, other(rest, rest.reduce((sum, item) => sum + valueOf(item), 0))];
}

const utcDay = (iso: string): number => {
  const [y, m, d] = iso.split('-').map(Number);
  return Date.UTC(y, (m ?? 1) - 1, d ?? 1);
};

export interface CalendarCell {
  date: string;
  /** Week column, counted from the week (Sunday) that holds the first date. */
  col: number;
  /** Weekday row, 0 = Sunday. */
  row: number;
}

/** Lays ISO dates out as a calendar: one column per week, one row per weekday. Parsed as text so time zones cannot shift a day. */
export function calendarGrid(dates: string[]): CalendarCell[] {
  if (dates.length === 0) return [];
  const first = utcDay(dates[0]);
  const firstWeekStart = first - new Date(first).getUTCDay() * 86_400_000;
  return dates.map((date) => {
    const at = utcDay(date);
    return { date, row: new Date(at).getUTCDay(), col: Math.floor((at - firstWeekStart) / (7 * 86_400_000)) };
  });
}

/** "just now", "5 min ago", "3 h ago", "2 days ago". */
export function relativeAge(iso: string, now: number = Date.now()): string {
  const seconds = Math.max(0, Math.round((now - new Date(iso).getTime()) / 1000));
  if (seconds < 60) return 'just now';
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  const days = Math.round(hours / 24);
  return days === 1 ? '1 day ago' : `${days} days ago`;
}

/** One hue, light to dark: each level is the accent mixed into the surface, so it follows the tenant's colour. */
export const heatColor = (level: number): string =>
  level <= 0 ? 'transparent' : `color-mix(in srgb, var(--viz-accent) ${[0, 16, 36, 58, 80, 100][level]}%, var(--surface))`;
