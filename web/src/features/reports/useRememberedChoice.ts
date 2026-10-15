import { useCallback, useState } from 'react';
import { defaultChoice, type RangeChoice } from './params';
import { rangeProblem } from './range';

const KEY = 'purch.reports.range.v1';
const PRESETS = ['today', '7d', '30d', 'custom'];

function isDay(value: unknown): value is string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value);
}

/** Reads the saved period, ignoring anything that is not a well-formed choice (storage can be cleared, blocked or edited). */
function load(): RangeChoice {
  try {
    const raw = window.localStorage.getItem(KEY);
    if (!raw) return defaultChoice('30d');
    const saved = JSON.parse(raw) as Partial<RangeChoice>;
    const fallback = defaultChoice('30d');
    if (typeof saved.preset !== 'string' || !PRESETS.includes(saved.preset)) return fallback;
    const custom = isDay(saved.custom?.fromDay) && isDay(saved.custom?.toDay) ? saved.custom : fallback.custom;
    const preset = saved.preset === 'custom' && rangeProblem(custom) ? '30d' : saved.preset;
    return { preset: preset as RangeChoice['preset'], custom, branchId: typeof saved.branchId === 'string' ? saved.branchId : '' };
  } catch {
    return defaultChoice('30d');
  }
}

/** The reporting period, kept across report tabs and visits so a manager does not re-pick it for every report. */
export function useRememberedChoice(): [RangeChoice, (next: RangeChoice) => void] {
  const [choice, setChoice] = useState<RangeChoice>(load);
  const update = useCallback((next: RangeChoice) => {
    setChoice(next);
    try {
      window.localStorage.setItem(KEY, JSON.stringify(next));
    } catch {
      // Private window or blocked storage: the choice still holds for this visit.
    }
  }, []);
  return [choice, update];
}
