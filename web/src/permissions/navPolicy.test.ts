import { describe, expect, it } from 'vitest';
import { isBusinessTileVisible, staffRoleFromClaim, tabsForRole } from './navPolicy';

describe('tabsForRole', () => {
  it('gives Admin and Manager all four tabs', () => {
    const all = ['home', 'sell', 'inventory', 'business'];
    expect(tabsForRole('Admin')).toEqual(all);
    expect(tabsForRole('Manager')).toEqual(all);
  });

  it('confines Cashier to Home and Sell', () => {
    expect(tabsForRole('Cashier')).toEqual(['home', 'sell']);
  });

  it('confines Warehouse to Home and Inventory', () => {
    expect(tabsForRole('Warehouse')).toEqual(['home', 'inventory']);
  });

  it('falls back to the restrictive set for an unknown role', () => {
    expect(tabsForRole(null)).toEqual(['home', 'sell']);
  });
});

describe('isBusinessTileVisible', () => {
  it('shows everything to Admin', () => {
    expect(isBusinessTileVisible('devices', 'Admin')).toBe(true);
  });

  it('hides the admin-only tiles from Manager', () => {
    expect(isBusinessTileVisible('devices', 'Manager')).toBe(false);
    expect(isBusinessTileVisible('business-settings', 'Manager')).toBe(false);
    expect(isBusinessTileVisible('staff', 'Manager')).toBe(true);
  });

  it('shows nothing to other roles', () => {
    expect(isBusinessTileVisible('staff', 'Cashier')).toBe(false);
    expect(isBusinessTileVisible('staff', null)).toBe(false);
  });
});

describe('staffRoleFromClaim', () => {
  it('matches case-insensitively', () => {
    expect(staffRoleFromClaim('cashier')).toBe('Cashier');
  });

  it('rejects device roles and garbage', () => {
    expect(staffRoleFromClaim('Kiosk')).toBeNull();
    expect(staffRoleFromClaim(null)).toBeNull();
  });
});
