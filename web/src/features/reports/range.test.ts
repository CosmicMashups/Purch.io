import { describe, expect, it } from 'vitest';
import { businessToday, dayRangeToUtc, presetRange, rangeProblem } from './range';

describe('dayRangeToUtc', () => {
  it('starts at local midnight in Manila and ends at the start of the next day', () => {
    expect(dayRangeToUtc({ fromDay: '2026-09-24', toDay: '2026-09-24' })).toEqual({
      from: '2026-09-23T16:00:00.000Z',
      to: '2026-09-24T16:00:00.000Z',
    });
  });

  it('includes the whole last day of a longer range', () => {
    expect(dayRangeToUtc({ fromDay: '2026-09-01', toDay: '2026-09-30' })).toEqual({
      from: '2026-08-31T16:00:00.000Z',
      to: '2026-09-30T16:00:00.000Z',
    });
  });

  it('rolls over month and year ends', () => {
    expect(dayRangeToUtc({ fromDay: '2026-12-31', toDay: '2026-12-31' }).to).toBe('2026-12-31T16:00:00.000Z');
  });
});

describe('businessToday', () => {
  it('uses Philippine time, so late evening UTC is already tomorrow in Manila', () => {
    expect(businessToday(new Date('2026-09-24T17:30:00Z'))).toBe('2026-09-25');
    expect(businessToday(new Date('2026-09-24T15:59:00Z'))).toBe('2026-09-24');
  });
});

describe('presetRange', () => {
  const now = new Date('2026-09-24T05:00:00Z');

  it('builds inclusive windows ending today', () => {
    expect(presetRange('today', now)).toEqual({ fromDay: '2026-09-24', toDay: '2026-09-24' });
    expect(presetRange('7d', now)).toEqual({ fromDay: '2026-09-18', toDay: '2026-09-24' });
    expect(presetRange('30d', now)).toEqual({ fromDay: '2026-08-26', toDay: '2026-09-24' });
  });
});

describe('rangeProblem', () => {
  it('accepts a good range and a single day', () => {
    expect(rangeProblem({ fromDay: '2026-09-01', toDay: '2026-09-30' })).toBeNull();
    expect(rangeProblem({ fromDay: '2026-09-01', toDay: '2026-09-01' })).toBeNull();
  });

  it('rejects blank, invalid and reversed ranges', () => {
    expect(rangeProblem({ fromDay: '', toDay: '2026-09-01' })).toBe('Choose both dates');
    expect(rangeProblem({ fromDay: '2026-13-40', toDay: '2026-09-01' })).toBe('Choose both dates');
    expect(rangeProblem({ fromDay: '2026-09-10', toDay: '2026-09-01' })).toBe('The end date is before the start date');
  });
});
