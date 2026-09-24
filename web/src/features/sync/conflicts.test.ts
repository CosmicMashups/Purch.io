import { describe, expect, it } from 'vitest';
import type { FlaggedSyncRecord } from '../dashboard/types';
import { openCount, shortRecordId, sortForReview } from './conflicts';

const rec = (id: string, ts: string, reviewedAt: string | null): FlaggedSyncRecord => ({
  id,
  deviceId: 'd',
  entityType: 'Sale',
  entityId: `3fa85f64-5717-4562-b3fc-2c963f66af${id}`,
  clientTimestamp: ts,
  reviewedAt,
});

describe('sortForReview', () => {
  it('puts unreviewed records first, newest first within each group', () => {
    const sorted = sortForReview([
      rec('1', '2026-09-20T00:00:00Z', '2026-09-21T00:00:00Z'),
      rec('2', '2026-09-22T00:00:00Z', null),
      rec('3', '2026-09-23T00:00:00Z', '2026-09-24T00:00:00Z'),
      rec('4', '2026-09-21T00:00:00Z', null),
    ]);
    expect(sorted.map((r) => r.id)).toEqual(['2', '4', '3', '1']);
  });

  it('does not change the list it is given', () => {
    const list = [rec('1', '2026-09-20T00:00:00Z', null), rec('2', '2026-09-21T00:00:00Z', null)];
    sortForReview(list);
    expect(list.map((r) => r.id)).toEqual(['1', '2']);
  });
});

describe('openCount', () => {
  it('counts only unreviewed records', () => {
    expect(openCount([rec('1', '2026-09-20T00:00:00Z', null), rec('2', '2026-09-20T00:00:00Z', 'x')])).toBe(1);
    expect(openCount([])).toBe(0);
  });
});

describe('shortRecordId', () => {
  it('shows the first eight characters', () => {
    expect(shortRecordId('3fa85f64-5717-4562-b3fc-2c963f66afa6')).toBe('#3fa85f64');
  });
});
