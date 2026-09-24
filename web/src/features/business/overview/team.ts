import { assignColors } from '../../../components/charts/colors';
import type { WaffleItem } from '../../../components/charts/Proportions';
import type { StaffMember } from '../staffApi';
import { Role, roleLabels } from '../types';

/** Every kind of person that can be on staff, so each role keeps the same colour whoever is switched off. */
const STAFF_ROLES = [Role.Admin, Role.Manager, Role.Cashier, Role.Warehouse] as const;

export function teamItems(staff: StaffMember[]): WaffleItem[] {
  const colors = assignColors(STAFF_ROLES.map(String));
  return STAFF_ROLES.flatMap((role) => {
    const color = colors.get(String(role)) as string;
    const name = roleLabels[role];
    const active = staff.filter((s) => s.role === role && s.isActive).length;
    const off = staff.filter((s) => s.role === role && !s.isActive).length;
    return [
      ...(active > 0 ? [{ key: `${role}-on`, label: name, count: active, color }] : []),
      ...(off > 0 ? [{ key: `${role}-off`, label: `${name}, switched off`, count: off, color, hollow: true }] : []),
    ];
  });
}
