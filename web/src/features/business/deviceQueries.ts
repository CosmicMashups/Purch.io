import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { deviceApi, type CreateDeviceBody } from './deviceApi';

export const deviceKeys = { all: ['devices'] as const };

export const useDevices = (enabled = true) => useQuery({ queryKey: deviceKeys.all, queryFn: () => deviceApi.list(), enabled });

export function useCreateDevice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateDeviceBody) => deviceApi.create(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: deviceKeys.all }),
  });
}

export function useResetPairingCode() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deviceApi.resetPairingCode(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: deviceKeys.all }),
  });
}

export function useResetPairingPin() {
  return useMutation({ mutationFn: ({ id, newPin }: { id: string; newPin: string }) => deviceApi.resetPairingPin(id, newPin) });
}
