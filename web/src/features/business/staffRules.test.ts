import { describe, expect, it } from 'vitest';
import { pinProblem, scopeChoicesFor, scopeFields, staffSchema } from './staffRules';
import { Role, ScopeType } from './types';

describe('pinProblem', () => {
  it('accepts 4 to 8 digits', () => {
    expect(pinProblem('1234')).toBeNull();
    expect(pinProblem('12345678')).toBeNull();
    expect(pinProblem(' 4321 ')).toBeNull();
  });

  it('rejects blank, short, long and non-digit PINs', () => {
    expect(pinProblem('')).toBe('Enter a PIN');
    expect(pinProblem('123')).toBe('Use 4 to 8 digits');
    expect(pinProblem('123456789')).toBe('Use 4 to 8 digits');
    expect(pinProblem('12a4')).toBe('Use 4 to 8 digits');
  });
});

describe('scopeFields', () => {
  const departments = [
    { id: 'bakery', branchId: 'kat' },
    { id: 'grill', branchId: 'kam' },
  ];

  it('gives a whole-business account no scope id and no branch', () => {
    expect(scopeFields(ScopeType.Tenant, 'kat', 'bakery', departments)).toEqual({ ok: true, scopeId: null, branchId: null });
  });

  it('gives a branch account its branch as both scope id and branch', () => {
    expect(scopeFields(ScopeType.Branch, 'kam', '', departments)).toEqual({ ok: true, scopeId: 'kam', branchId: 'kam' });
    expect(scopeFields(ScopeType.Branch, '', '', departments)).toEqual({ ok: false, message: 'Choose a branch' });
  });

  it("gives a department account the department and that department's own branch", () => {
    expect(scopeFields(ScopeType.Department, 'kat', 'grill', departments)).toEqual({ ok: true, scopeId: 'grill', branchId: 'kam' });
    expect(scopeFields(ScopeType.Department, 'kat', '', departments)).toEqual({ ok: false, message: 'Choose a department' });
    expect(scopeFields(ScopeType.Department, 'kat', 'gone', departments)).toEqual({ ok: false, message: 'Choose a department' });
  });

  it('ignores a leftover branch choice when the scope is the whole business', () => {
    expect(scopeFields(ScopeType.Tenant, 'kat', '', departments).ok).toBe(true);
  });
});

describe('scopeChoicesFor', () => {
  it('keeps admins whole-business and lets other roles be narrowed', () => {
    expect(scopeChoicesFor(Role.Admin)).toEqual([ScopeType.Tenant]);
    expect(scopeChoicesFor(Role.Cashier)).toEqual([ScopeType.Tenant, ScopeType.Branch, ScopeType.Department]);
  });
});

describe('staffSchema', () => {
  const base = { name: 'Ana Reyes', role: Role.Cashier as number, scopeType: 0, branchId: '', departmentId: '', pin: '', isActive: true };

  it('accepts a staff role and rejects device roles', () => {
    expect(staffSchema.safeParse(base).success).toBe(true);
    expect(staffSchema.safeParse({ ...base, role: Role.Kiosk }).success).toBe(false);
  });

  it('needs a name', () => {
    expect(staffSchema.safeParse({ ...base, name: '  ' }).success).toBe(false);
  });
});
