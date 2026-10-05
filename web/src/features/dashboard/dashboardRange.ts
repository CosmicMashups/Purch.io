import { addDays, businessToday, dayRangeToUtc, presetRange, rangeProblem, type DayRange } from '../reports/range';
import type { RangeParams } from '../reports/types';

export type DashboardPreset = '7d' | '30d' | '3m' | '1y' | 'custom';

export const DASHBOARD_PRESET_LABELS: Record<DashboardPreset, string> = {
  '7d': 'Last 7 days',
  '30d': 'Last 30 days',
  '3m': 'Last 3 months',
  '1y': 'Last 12 months',
  custom: 'Custom range',
};

export interface DashboardRangeChoice {
  preset: DashboardPreset;
  custom: DayRange;
}

export const defaultDashboardRange = (): DashboardRangeChoice => ({ preset: '30d', custom: presetRange('30d') });

/** The first day of a window ending today that reaches back the given number of calendar months. */
function monthsBack(today: string, months: number): string {
  const date = new Date(`${today}T00:00:00Z`);
  date.setUTCMonth(date.getUTCMonth() - months);
  return addDays(date.toISOString().slice(0, 10), 1);
}

export function dashboardDays(choice: DashboardRangeChoice, now: Date = new Date()): DayRange {
  const today = businessToday(now);
  switch (choice.preset) {
    case '7d':
      return presetRange('7d', now);
    case '30d':
      return presetRange('30d', now);
    case '3m':
      return { fromDay: monthsBack(today, 3), toDay: today };
    case '1y':
      return { fromDay: monthsBack(today, 12), toDay: today };
    default:
      return choice.custom;
  }
}

/** The API range for a choice, or null while a custom range is incomplete or reversed. */
export function dashboardParams(choice: DashboardRangeChoice, now: Date = new Date()): RangeParams | null {
  const days = dashboardDays(choice, now);
  if (choice.preset === 'custom' && rangeProblem(days)) return null;
  return dayRangeToUtc(days);
}
