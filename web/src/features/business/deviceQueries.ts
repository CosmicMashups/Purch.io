import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { deviceApi, type CreatePairingBody } from './deviceApi';

export const deviceKeys = { all: ['devices'] as const };

export const useDevices = (enabled = true) => useQuery({ queryKey: deviceKeys.all, queryFn: () => deviceApi.list(), enabled });

export function useCreatePairing() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreatePairingBody) => deviceApi.createPairing(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: deviceKeys.all }),
  });
}

export function useNewPairingCode() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deviceApi.newPairingCode(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: deviceKeys.all }),
  });
}

export function useRevokeDevice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deviceApi.revoke(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: deviceKeys.all }),
  });
}
