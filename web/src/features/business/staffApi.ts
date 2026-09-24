import { apiClient } from '../../lib/apiClient';

/** Mirrors Purch.Application.Onboarding.StaffDto. */
export interface StaffMember {
  id: string;
  name: string;
  role: number;
  scopeType: number;
  scopeId: string | null;
  branchId: string | null;
  isActive: boolean;
}

export interface CreateStaffBody {
  name: string;
  role: number;
  scopeType: number;
  scopeId: string | null;
  branchId: string | null;
  pin: string;
}

export interface UpdateStaffBody {
  role: number;
  scopeType: number;
  scopeId: string | null;
  branchId: string | null;
  isActive: boolean;
}

export const staffApi = {
  list: () => apiClient.get<StaffMember[]>('/staff').then((r) => r.data),
  create: (body: CreateStaffBody) => apiClient.post<StaffMember>('/staff', body).then((r) => r.data),
  update: (id: string, body: UpdateStaffBody) => apiClient.put<StaffMember>(`/staff/${id}`, body).then((r) => r.data),
};
