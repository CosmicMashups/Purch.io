import { describe, expect, it } from 'vitest';
import { choiceToParams, defaultChoice } from './params';

describe('choiceToParams', () => {
  it('turns a preset into UTC instants with no branch', () => {
    const params = choiceToParams(defaultChoice('today'));
    expect(params).not.toBeNull();
    expect(params).not.toHaveProperty('branchId');
    expect(new Date(params!.to).getTime() - new Date(params!.from).getTime()).toBe(24 * 3_600_000);
  });

  it('includes the chosen branch', () => {
    expect(choiceToParams({ ...defaultChoice('7d'), branchId: 'kat' })?.branchId).toBe('kat');
  });

  it('uses the custom days when custom is chosen', () => {
    const params = choiceToParams({ preset: 'custom', custom: { fromDay: '2026-09-01', toDay: '2026-09-02' }, branchId: '' });
    expect(params).toEqual({ from: '2026-08-31T16:00:00.000Z', to: '2026-09-02T16:00:00.000Z' });
  });

  it('is null for an incomplete or reversed custom range, so nothing is fetched', () => {
    expect(choiceToParams({ preset: 'custom', custom: { fromDay: '', toDay: '2026-09-02' }, branchId: '' })).toBeNull();
    expect(choiceToParams({ preset: 'custom', custom: { fromDay: '2026-09-05', toDay: '2026-09-02' }, branchId: '' })).toBeNull();
  });
});
