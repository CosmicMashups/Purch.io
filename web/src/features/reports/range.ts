/** The business counts days in Philippine time (UTC+8, no daylight saving), as the API reports do. */
const BUSINESS_OFFSET = '+08:00';

export interface DayRange {
  /** Inclusive first day, `yyyy-MM-dd`. */
  fromDay: string;
  /** Inclusive last day, `yyyy-MM-dd`. */
  toDay: string;
}

export interface UtcRange {
  from: string;
  to: string;
}

function addDays(day: string, days: number): string {
  const date = new Date(`${day}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** Today's date in Philippine time as `yyyy-MM-dd`, whatever the browser's own time zone is. */
export function businessToday(now: Date = new Date()): string {
  return new Date(now.getTime() + 8 * 3_600_000).toISOString().slice(0, 10);
}

/**
 * Turns whole business days into the UTC instants the API expects: from the start of the first day to
 * the start of the day after the last, so the last day is fully included.
 */
export function dayRangeToUtc({ fromDay, toDay }: DayRange): UtcRange {
  return {
    from: new Date(`${fromDay}T00:00:00${BUSINESS_OFFSET}`).toISOString(),
    to: new Date(`${addDays(toDay, 1)}T00:00:00${BUSINESS_OFFSET}`).toISOString(),
  };
}

export type RangePreset = 'today' | '7d' | '30d';

export const PRESET_LABELS: Record<RangePreset, string> = { today: 'Today', '7d': 'Last 7 days', '30d': 'Last 30 days' };

export function presetRange(preset: RangePreset, now: Date = new Date()): DayRange {
  const today = businessToday(now);
  const back = preset === 'today' ? 0 : preset === '7d' ? 6 : 29;
  return { fromDay: addDays(today, -back), toDay: today };
}

/** A custom range is valid when both days are real dates and the end is not before the start. */
export function rangeProblem({ fromDay, toDay }: DayRange): string | null {
  const valid = (d: string) => /^\d{4}-\d{2}-\d{2}$/.test(d) && !Number.isNaN(new Date(`${d}T00:00:00Z`).getTime());
  if (!valid(fromDay) || !valid(toDay)) return 'Choose both dates';
  if (toDay < fromDay) return 'The end date is before the start date';
  return null;
}
