import { describe, expect, it } from 'vitest';
import { AuditActionType, DeviceType, Role, STAFF_ROLES, auditActionLabels, deviceTypeLabels, labelOf, roleLabels } from './types';

describe('enum labels', () => {
  it('labels every audit action, device type and role the API can send', () => {
    for (const value of Object.values(AuditActionType)) expect(auditActionLabels[value]).toBeTruthy();
    for (const value of Object.values(DeviceType)) expect(deviceTypeLabels[value]).toBeTruthy();
    for (const value of Object.values(Role)) expect(roleLabels[value]).toBeTruthy();
  });

  it('falls back for a value a newer server might add', () => {
    expect(labelOf(roleLabels, 99)).toBe('Unknown');
  });

  it('never offers device roles as staff roles', () => {
    expect(STAFF_ROLES).toEqual([Role.Admin, Role.Manager, Role.Cashier, Role.Warehouse]);
  });
});
