import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { memberApi, type CreateInviteBody, type UpdateMemberBody } from './memberApi';

export const memberKeys = { members: ['staff', 'members'] as const, invites: ['staff', 'invites'] as const };

export const useMembers = () => useQuery({ queryKey: memberKeys.members, queryFn: () => memberApi.list() });
export const useInvites = () => useQuery({ queryKey: memberKeys.invites, queryFn: () => memberApi.invites() });

export function useInvite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateInviteBody) => memberApi.invite(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: memberKeys.invites }),
  });
}

export function useCancelInvite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => memberApi.cancelInvite(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: memberKeys.invites }),
  });
}

export function useUpdateMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateMemberBody }) => memberApi.update(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: memberKeys.members }),
  });
}

export function useResetLink() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => memberApi.resetLink(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: memberKeys.invites }),
  });
}
