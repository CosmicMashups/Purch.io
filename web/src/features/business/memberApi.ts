import { apiClient } from '../../lib/apiClient';

/** Mirrors Purch.Application.Onboarding.MemberDto: a person's place in this business. */
export interface Member {
  id: string;
  name: string;
  email: string;
  role: number;
  duties: number;
  branchIds: string[];
  isActive: boolean;
  hasPin: boolean;
}

export interface Invite {
  id: string;
  /** 0 enrolment, 1 password reset. */
  purpose: number;
  name: string;
  email: string;
  role: number;
  duties: number;
  branchIds: string[];
  expiresAt: string;
}

/** The token is shown once as a link or QR code; the server keeps only its hash. */
export interface InviteLink {
  invite: Invite;
  token: string;
}

export interface CreateInviteBody {
  name: string;
  email: string;
  role: number;
  duties: number;
  branchIds: string[];
  /** Re-enrols someone from the old sign-in, so their earlier sales and shifts stay theirs. */
  legacyUserId?: string | null;
}

/** Someone from the old sign-in who has not been invited to the new one yet. The role and duty are suggestions. */
export interface LegacyStaff {
  id: string;
  name: string;
  suggestedRole: number;
  suggestedDuties: number;
  branchId: string | null;
}

export interface UpdateMemberBody {
  role: number;
  duties: number;
  branchIds: string[];
  isActive: boolean;
}

export const memberApi = {
  list: () => apiClient.get<Member[]>('/staff/members').then((r) => r.data),
  update: (id: string, body: UpdateMemberBody) => apiClient.put<Member>(`/staff/members/${id}`, body).then((r) => r.data),
  resetLink: (id: string) => apiClient.post<InviteLink>(`/staff/members/${id}/reset-link`).then((r) => r.data),
  invites: () => apiClient.get<Invite[]>('/staff/invites').then((r) => r.data),
  legacy: () => apiClient.get<LegacyStaff[]>('/staff/legacy').then((r) => r.data),
  invite: (body: CreateInviteBody) => apiClient.post<InviteLink>('/staff/invites', body).then((r) => r.data),
  cancelInvite: (id: string) => apiClient.delete(`/staff/invites/${id}`).then(() => undefined),
};
