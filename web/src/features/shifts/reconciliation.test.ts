import { describe, expect, it } from 'vitest';
import { cashOutcome, describeOutcome, parseCashAmount } from './reconciliation';

describe('cashOutcome', () => {
  it('reads the server variance', () => {
    expect(cashOutcome({ varianceAmount: 0 })).toEqual({ kind: 'matched' });
    expect(cashOutcome({ varianceAmount: 12.5 })).toEqual({ kind: 'over', amount: 12.5 });
    expect(cashOutcome({ varianceAmount: -40 })).toEqual({ kind: 'short', amount: 40 });
    expect(cashOutcome({ varianceAmount: null })).toEqual({ kind: 'unknown' });
  });

  it('treats floating-point noise as matched', () => {
    expect(cashOutcome({ varianceAmount: 0.0000001 })).toEqual({ kind: 'matched' });
  });
});

describe('describeOutcome', () => {
  it('words each outcome for the cashier', () => {
    expect(describeOutcome({ kind: 'matched' })).toBe('Matched exactly');
    expect(describeOutcome({ kind: 'over', amount: 10 })).toMatch(/^Over by ₱10\.00/);
    expect(describeOutcome({ kind: 'short', amount: 10 })).toMatch(/^Short by ₱10\.00/);
    expect(describeOutcome({ kind: 'unknown' })).toBe('Not reconciled');
  });
});

describe('parseCashAmount', () => {
  it('accepts zero and decimals', () => {
    expect(parseCashAmount('0')).toEqual({ ok: true, value: 0 });
    expect(parseCashAmount(' 1250.50 ')).toEqual({ ok: true, value: 1250.5 });
  });

  it('rejects blank, negative and non-numeric input', () => {
    expect(parseCashAmount('')).toEqual({ ok: false, message: 'Enter the cash amount' });
    expect(parseCashAmount('-1')).toEqual({ ok: false, message: 'Cannot be negative' });
    expect(parseCashAmount('abc')).toEqual({ ok: false, message: 'Enter a number' });
  });
});
