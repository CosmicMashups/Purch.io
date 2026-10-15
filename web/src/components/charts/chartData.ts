export interface WaterfallStep {
  key: string;
  label: string;
  /** A total is drawn from zero; a delta moves the running figure up or down by its (signed) value. */
  value: number;
  kind: 'total' | 'delta';
}

export interface WaterfallBar {
  key: string;
  label: string;
  kind: 'total' | 'delta';
  /** The signed amount the step represents. */
  value: number;
  /** The running figure before and after this step. For a total, `from` is 0 and `to` is the total. */
  from: number;
  to: number;
}

/** Turns steps into bars on a running total: a delta starts where the last bar ended. */
export function waterfallBars(steps: WaterfallStep[]): WaterfallBar[] {
  let running = 0;
  return steps.map((step) => {
    if (step.kind === 'total') {
      running = step.value;
      return { key: step.key, label: step.label, kind: 'total', value: step.value, from: 0, to: step.value };
    }
    const from = running;
    running += step.value;
    return { key: step.key, label: step.label, kind: 'delta', value: step.value, from, to: running };
  });
}

export interface ParetoRow {
  key: string;
  label: string;
  value: number;
  /** Running share of the total, in percent, up to and including this row. */
  cumulativePercent: number;
}

/** Biggest first, each row carrying the running share. Rows past `keep` fold into one "Other" row. */
export function paretoRows(items: { key: string; label: string; value: number }[], keep = 10, otherLabel = 'Other'): ParetoRow[] {
  const positive = items.filter((i) => i.value > 0).sort((a, b) => b.value - a.value);
  const total = positive.reduce((sum, i) => sum + i.value, 0);
  if (total <= 0) return [];
  const head = positive.slice(0, keep);
  const rest = positive.slice(keep);
  const rows = rest.length > 0 ? [...head, { key: 'other', label: `${otherLabel} (${rest.length})`, value: rest.reduce((s, i) => s + i.value, 0) }] : head;
  let running = 0;
  return rows.map((row) => {
    running += row.value;
    return { ...row, cumulativePercent: Math.round((running / total) * 1000) / 10 };
  });
}

/** The middle value, or 0 for nothing. Used to split a scatter into quadrants. */
export function median(values: number[]): number {
  if (values.length === 0) return 0;
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
}

export type Quadrant = 'star' | 'volume' | 'niche' | 'review';

/** Higher than the median on both axes is a star; high x, low y sells a lot for little; and so on. */
export function quadrantOf(x: number, y: number, medianX: number, medianY: number): Quadrant {
  if (x >= medianX) return y >= medianY ? 'star' : 'volume';
  return y >= medianY ? 'niche' : 'review';
}
