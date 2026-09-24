import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { staffApi, type CreateStaffBody, type UpdateStaffBody } from './staffApi';

export const staffKeys = { all: ['staff'] as const };

export const useStaff = () => useQuery({ queryKey: staffKeys.all, queryFn: () => staffApi.list() });

export function useCreateStaff() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateStaffBody) => staffApi.create(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.all }),
  });
}

export function useUpdateStaff() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateStaffBody }) => staffApi.update(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.all }),
  });
}
