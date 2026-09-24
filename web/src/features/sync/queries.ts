import { useMutation, useQueryClient } from '@tanstack/react-query';
import { dashboardKeys } from '../dashboard/queries';
import { syncApi } from './api';

/** The flagged list is shared with the Home notice, so acknowledging refreshes both. */
export function useAcknowledgeFlagged() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => syncApi.acknowledge(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: dashboardKeys.flaggedSync }),
  });
}
