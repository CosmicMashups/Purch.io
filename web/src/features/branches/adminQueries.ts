import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { catalogKeys } from '../catalog/queries';
import { branchAdminApi, type GcashBody, type HardwareBody } from './adminApi';
import { branchKeys } from './queries';

export const useBranchDepartments = (branchId: string | null) =>
  useQuery({
    queryKey: ['branches', branchId, 'departments'] as const,
    queryFn: () => branchAdminApi.listDepartments(branchId as string),
    enabled: branchId !== null,
  });

export function useCreateBranch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; address: string | null }) => branchAdminApi.create(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: branchKeys.all }),
  });
}

export function useUpdateHardware() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: HardwareBody }) => branchAdminApi.updateHardware(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: branchKeys.all }),
  });
}

export function useUpdateGcash() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: GcashBody }) => branchAdminApi.updateGcash(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: branchKeys.all }),
  });
}

export function useCreateDepartment(branchId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; concessionaireContactInfo: string | null }) => branchAdminApi.createDepartment(branchId, body),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['branches', branchId, 'departments'] });
      await qc.invalidateQueries({ queryKey: catalogKeys.departments });
    },
  });
}
