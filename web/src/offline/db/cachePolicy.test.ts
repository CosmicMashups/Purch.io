import { describe, expect, it } from 'vitest';
import { CACHE_MAX_AGE_MS, isCacheableKey, shouldPersistQuery } from './cachePolicy';

describe('isCacheableKey', () => {
  it('allows the catalog and branch structure, including per-item sub-queries', () => {
    for (const key of [['items'], ['items', 'i1', 'variants'], ['categories'], ['modifierGroups'], ['branches'], ['branches', 'b1', 'departments'], ['departments']]) {
      expect(isCacheableKey(key)).toBe(true);
    }
  });

  it('keeps everything personal or sensitive out', () => {
    for (const key of [
      ['staff'],
      ['credit-ledger'],
      ['credit-ledger', 'reminders', 7],
      ['audit-logs', {}],
      ['tenant', 'settings'],
      ['pos', 'cart'],
      ['pos', 'kiosk-pending', 'b1'],
      ['reports', 'staff', {}],
      ['devices'],
      ['shifts', 'current'],
      ['dashboard', 'sales'],
      ['inventory-items'],
      ['inventory', 'movements'],
      ['promos', 'codes'],
      ['sync'],
    ]) {
      expect(isCacheableKey(key)).toBe(false);
    }
  });

  it('rejects a key that is empty or does not start with a string', () => {
    expect(isCacheableKey([])).toBe(false);
    expect(isCacheableKey([{ items: true }])).toBe(false);
  });
});

describe('shouldPersistQuery', () => {
  it('keeps successful catalog queries only', () => {
    expect(shouldPersistQuery({ queryKey: ['items'], state: { status: 'success' } })).toBe(true);
    expect(shouldPersistQuery({ queryKey: ['items'], state: { status: 'error' } })).toBe(false);
    expect(shouldPersistQuery({ queryKey: ['items'], state: { status: 'pending' } })).toBe(false);
    expect(shouldPersistQuery({ queryKey: ['staff'], state: { status: 'success' } })).toBe(false);
  });
});

describe('CACHE_MAX_AGE_MS', () => {
  it('is one day', () => {
    expect(CACHE_MAX_AGE_MS).toBe(86_400_000);
  });
});
