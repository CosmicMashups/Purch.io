import { z } from 'zod';
import { Role, ScopeType, STAFF_ROLES } from './types';

export const PIN_MIN = 4;
export const PIN_MAX = 8;

/** Same rule as the API and the Flutter client: 4 to 8 digits. */
export function pinProblem(pin: string): string | null {
  const trimmed = pin.trim();
  if (trimmed === '') return 'Enter a PIN';
  if (trimmed.length < PIN_MIN || trimmed.length > PIN_MAX || !/^\d+$/.test(trimmed)) return `Use ${PIN_MIN} to ${PIN_MAX} digits`;
  return null;
}

export interface DepartmentRef {
  id: string;
  branchId: string;
}

/**
 * The API stores scope, scope id and branch as given and never checks they agree, and reports and stock
 * limits read them later. So they are derived together here, from the one thing the person chose.
 * A whole-business account carries neither; a branch account carries its branch as both; a department
 * account carries the department, and that department's branch.
 */
export function scopeFields(
  scopeType: number,
  branchId: string,
  departmentId: string,
  departments: DepartmentRef[],
): { ok: true; scopeId: string | null; branchId: string | null } | { ok: false; message: string } {
  if (scopeType === ScopeType.Tenant) return { ok: true, scopeId: null, branchId: null };
  if (scopeType === ScopeType.Branch) {
    return branchId ? { ok: true, scopeId: branchId, branchId } : { ok: false, message: 'Choose a branch' };
  }
  const department = departments.find((d) => d.id === departmentId);
  return department ? { ok: true, scopeId: department.id, branchId: department.branchId } : { ok: false, message: 'Choose a department' };
}

const roleValue = z.number().refine((v) => (STAFF_ROLES as readonly number[]).includes(v), 'Choose a role');

export const staffSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name'),
  role: roleValue,
  scopeType: z.number().int().min(0).max(2),
  branchId: z.string(),
  departmentId: z.string(),
  pin: z.string(),
  isActive: z.boolean(),
});

export type StaffForm = z.infer<typeof staffSchema>;

/** A whole-business admin is the owner's account; narrowing it would lock the owner out of tenant-wide pages. */
export function scopeChoicesFor(role: number): readonly number[] {
  return role === Role.Admin ? [ScopeType.Tenant] : [ScopeType.Tenant, ScopeType.Branch, ScopeType.Department];
}
