import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { shiftsApi } from './api';
import type { CloseShiftRequest } from './types';

export const shiftKeys = { current: ['shifts', 'current'] as const };

export const useCurrentShift = (enabled = true) => useQuery({ queryKey: shiftKeys.current, queryFn: () => shiftsApi.current(), enabled });

export function useOpenShift() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (openingCashAmount: number) => shiftsApi.open(openingCashAmount),
    onSuccess: (shift) => qc.setQueryData(shiftKeys.current, shift),
  });
}

/** Closing returns the closed shift with its reconciliation. The device then has no open shift. */
export function useCloseShift() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CloseShiftRequest) => shiftsApi.close(body),
    onSuccess: () => qc.setQueryData(shiftKeys.current, null),
  });
}
