import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { equipmentApi } from './api';
import type { Equipment, EquipmentStatus } from './types';

export const equipmentKeys = {
  all: ['equipment'] as const,
  forItem: (itemId: string) => ['items', itemId, 'equipment'] as const,
};

export const useEquipment = () => useQuery({ queryKey: equipmentKeys.all, queryFn: equipmentApi.list });

/** Equipment status decides whether items can be sold, so a change refreshes the catalog the till reads as well. */
function useEquipmentMutation<TVars>(fn: (vars: TVars) => Promise<unknown>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => fn(vars),
    onSuccess: () => Promise.all([qc.invalidateQueries({ queryKey: equipmentKeys.all }), qc.invalidateQueries({ queryKey: ['items'] })]),
  });
}

type Api = typeof equipmentApi;

export const useCreateEquipment = () => useEquipmentMutation(equipmentApi.create);
export const useUpdateEquipment = () => useEquipmentMutation(({ id, body }: { id: string; body: Parameters<Api['update']>[1] }) => equipmentApi.update(id, body));
export const useSetEquipmentStatus = () => useEquipmentMutation(({ id, status }: { id: string; status: EquipmentStatus }) => equipmentApi.setStatus(id, status));

/** Moves the given equipment to the positions they are listed in, showing the new order straight away and undoing it if the save fails. */
export function useReorderEquipment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) => equipmentApi.reorder(ids),
    onMutate: async (ids) => {
      await qc.cancelQueries({ queryKey: equipmentKeys.all });
      const previous = qc.getQueryData<Equipment[]>(equipmentKeys.all);
      qc.setQueryData<Equipment[]>(equipmentKeys.all, (rows) =>
        rows?.map((row) => {
          const position = ids.indexOf(row.id);
          return position < 0 ? row : { ...row, sortOrder: position };
        }),
      );
      return { previous };
    },
    onError: (_error, _ids, context) => qc.setQueryData(equipmentKeys.all, context?.previous),
    onSettled: () => qc.invalidateQueries({ queryKey: equipmentKeys.all }),
  });
}

export const useItemEquipment = (itemId: string | undefined) =>
  useQuery({ queryKey: equipmentKeys.forItem(itemId ?? ''), queryFn: () => equipmentApi.getItemEquipment(itemId!), enabled: !!itemId });

export const useReplaceItemEquipment = () =>
  useEquipmentMutation(({ itemId, equipmentIds }: { itemId: string; equipmentIds: string[] }) => equipmentApi.replaceItemEquipment(itemId, equipmentIds));
