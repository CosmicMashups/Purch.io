import { dayRangeToUtc, presetRange, rangeProblem, type DayRange, type RangePreset } from './range';
import type { RangeParams } from './types';

export interface RangeChoice {
  preset: RangePreset | 'custom';
  custom: DayRange;
  branchId: string;
}

export function defaultChoice(preset: RangePreset = '30d'): RangeChoice {
  return { preset, custom: presetRange('30d'), branchId: '' };
}

/** The query parameters for a choice, or null while a custom range is incomplete or reversed. */
export function choiceToParams(choice: RangeChoice): RangeParams | null {
  const days = choice.preset === 'custom' ? choice.custom : presetRange(choice.preset);
  if (choice.preset === 'custom' && rangeProblem(days)) return null;
  const { from, to } = dayRangeToUtc(days);
  return choice.branchId ? { from, to, branchId: choice.branchId } : { from, to };
}
