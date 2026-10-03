import { apiClient } from '../../lib/apiClient';
import type { Member } from './memberApi';
import { MembershipRole, StaffDuty } from './types';

/**
 * A person as the audit log, the business hub and the overview read them: a name, a role and whether they are active. These
 * are the people of the business (memberships), shaped like the older staff rows those screens were written against. The
 * role is the older numeric one (0 Admin, 1 Manager, 2 Cashier, 3 Warehouse).
 */
export interface StaffMember {
  id: string;
  name: string;
  role: number;
  scopeType: number;
  scopeId: string | null;
  branchId: string | null;
  isActive: boolean;
}

function toStaffMember(member: Member): StaffMember {
  const role =
    member.role === MembershipRole.Admin ? 0 : member.role === MembershipRole.Manager ? 1 : (member.duties & StaffDuty.Warehouse) !== 0 && (member.duties & StaffDuty.Cashier) === 0 ? 3 : 2;
  return { id: member.id, name: member.name, role, scopeType: 0, scopeId: null, branchId: null, isActive: member.isActive };
}

export const staffApi = {
  list: () => apiClient.get<Member[]>('/staff/members').then((r) => r.data.map(toStaffMember)),
};
