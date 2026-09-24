import { describe, expect, it } from 'vitest';
import { formatDay, formatPeso, greetingFor, stockRatio } from './format';

describe('formatPeso', () => {
  it('formats Philippine pesos with grouping and two decimals', () => {
    expect(formatPeso(1234.5)).toMatch(/₱1,234\.50/);
  });
});

describe('formatDay', () => {
  it('formats an ISO date without timezone drift', () => {
    expect(formatDay('2026-09-24')).toBe('Sep 24');
    expect(formatDay('2026-01-01')).toBe('Jan 1');
  });

  it('returns unparseable input unchanged', () => {
    expect(formatDay('nonsense')).toBe('nonsense');
  });
});

describe('greetingFor', () => {
  it('changes at noon and six', () => {
    expect(greetingFor(8)).toBe('Good morning');
    expect(greetingFor(12)).toBe('Good afternoon');
    expect(greetingFor(18)).toBe('Good evening');
  });
});

describe('stockRatio', () => {
  it('is the share of the threshold, clamped', () => {
    expect(stockRatio(5, 20)).toBe(0.25);
    expect(stockRatio(50, 20)).toBe(1);
    expect(stockRatio(-3, 20)).toBe(0);
  });

  it('treats a missing threshold as empty rather than dividing by zero', () => {
    expect(stockRatio(5, 0)).toBe(0);
  });
});
