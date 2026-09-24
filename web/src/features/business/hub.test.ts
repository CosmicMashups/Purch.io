import { describe, expect, it } from 'vitest';
import { TILE_GROUPS, hubStats } from './hub';
import type { StaffMember } from './staffApi';

const member = (over: Partial<StaffMember>): StaffMember => ({ id: 'x', name: 'A', role: 2, scopeType: 0, scopeId: null, branchId: null, isActive: true, ...over });

describe('TILE_GROUPS', () => {
  it('lists every tile once, so no link is lost or doubled', () => {
    const ids = TILE_GROUPS.flatMap((g) => g.tiles.map((t) => t.id));
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids).toHaveLength(12);
  });
});

describe('hubStats', () => {
  it('says nothing for a figure that has not loaded', () => {
    expect(hubStats({})).toEqual({});
  });

  it('counts with the right words', () => {
    const s = hubStats({ itemCount: 1, categoryCount: 6, modifierGroupCount: 0, branchCount: 1, deviceCount: 5 });
    expect(s['catalog-items']?.text).toBe('1 item');
    expect(s['catalog-categories']?.text).toBe('6 categories');
    expect(s['catalog-modifiers']?.text).toBe('0 groups');
    expect(s.branches?.text).toBe('1 branch');
    expect(s.devices?.text).toBe('5 paired');
  });

  it('separates staff who are switched off', () => {
    expect(hubStats({ staff: [member({}), member({ isActive: false })] }).staff?.text).toBe('1 active, 1 off');
    expect(hubStats({ staff: [member({})] }).staff?.text).toBe('1 active');
  });

  it('totals what customers owe, ignoring closed accounts', () => {
    const credit = [
      { id: '1', customerFullName: 'A', customerPhoneNumber: '', customerAddress: null, balance: 300, creditLimit: 1000, dueDate: null, isActive: true },
      { id: '2', customerFullName: 'B', customerPhoneNumber: '', customerAddress: null, balance: 200, creditLimit: 1000, dueDate: null, isActive: true },
      { id: '3', customerFullName: 'C', customerPhoneNumber: '', customerAddress: null, balance: 999, creditLimit: 1000, dueDate: null, isActive: false },
    ];
    expect(hubStats({ credit }).customers?.text).toBe('₱500.00 owed');
    expect(hubStats({ credit: [] }).customers?.text).toBe('Nothing owed');
  });

  it('flags only unreviewed sync records', () => {
    const rec = (reviewedAt: string | null) => ({ id: 'r', deviceId: 'd', entityType: 'Item', entityId: 'e', clientTimestamp: '', reviewedAt });
    expect(hubStats({ flagged: [rec(null), rec('2026-09-01'), rec(null)] })['sync-conflicts']).toEqual({ text: '2 to review', warn: true });
    expect(hubStats({ flagged: [rec('2026-09-01')] })['sync-conflicts']).toEqual({ text: 'All clear' });
  });
});
