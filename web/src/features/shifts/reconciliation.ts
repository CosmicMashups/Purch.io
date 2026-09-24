import { formatPeso } from '../dashboard/format';
import type { Shift } from './types';

export type CashOutcome = { kind: 'matched' } | { kind: 'over'; amount: number } | { kind: 'short'; amount: number } | { kind: 'unknown' };

/** Reads the server's variance. Sub-centavo noise counts as matched. */
export function cashOutcome(shift: Pick<Shift, 'varianceAmount'>): CashOutcome {
  const variance = shift.varianceAmount;
  if (variance === null) return { kind: 'unknown' };
  if (Math.abs(variance) < 0.005) return { kind: 'matched' };
  return variance > 0 ? { kind: 'over', amount: variance } : { kind: 'short', amount: Math.abs(variance) };
}

export function describeOutcome(outcome: CashOutcome): string {
  switch (outcome.kind) {
    case 'matched':
      return 'Matched exactly';
    case 'over':
      return `Over by ${formatPeso(outcome.amount)}`;
    case 'short':
      return `Short by ${formatPeso(outcome.amount)}`;
    case 'unknown':
      return 'Not reconciled';
  }
}

/** A cash count is a peso amount that is not negative. Blank is not a count. */
export function parseCashAmount(text: string): { ok: true; value: number } | { ok: false; message: string } {
  const trimmed = text.trim();
  if (trimmed === '') return { ok: false, message: 'Enter the cash amount' };
  const value = Number(trimmed);
  if (!Number.isFinite(value)) return { ok: false, message: 'Enter a number' };
  if (value < 0) return { ok: false, message: 'Cannot be negative' };
  return { ok: true, value };
}
