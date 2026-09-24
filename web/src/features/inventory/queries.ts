import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { dashboardKeys } from '../dashboard/queries';
import { MOVEMENT_PAGE_SIZE, inventoryApi } from './api';
import type { MovementCursor, MovementFilter } from './types';

export const inventoryKeys = {
  items: ['inventory-items'] as const,
  movements: ['inventory', 'movements'] as const,
  suppliers: ['inventory', 'suppliers'] as const,
  purchaseOrders: ['inventory', 'purchase-orders'] as const,
  transfers: ['inventory', 'transfers'] as const,
};

export const useInventoryItems = () => useQuery({ queryKey: inventoryKeys.items, queryFn: inventoryApi.listInventoryItems });
export const useSuppliers = () => useQuery({ queryKey: inventoryKeys.suppliers, queryFn: inventoryApi.listSuppliers });
export const usePurchaseOrders = () => useQuery({ queryKey: inventoryKeys.purchaseOrders, queryFn: inventoryApi.listPurchaseOrders });
export const useTransfers = () => useQuery({ queryKey: inventoryKeys.transfers, queryFn: inventoryApi.listTransfers });

/** The movement log, newest first, loaded a page at a time by the API's (createdAt, id) cursor. */
export function useMovementLog(filter: MovementFilter) {
  return useInfiniteQuery({
    queryKey: [...inventoryKeys.movements, filter],
    initialPageParam: null as MovementCursor | null,
    queryFn: ({ pageParam }) => inventoryApi.listMovements(filter, pageParam),
    getNextPageParam: (lastPage): MovementCursor | undefined => {
      const last = lastPage[lastPage.length - 1];
      return lastPage.length === MOVEMENT_PAGE_SIZE && last ? { before: last.createdAt, beforeId: last.id } : undefined;
    },
  });
}

/** Stock changes ripple into the dashboard, ingredient list, item stock and the movement log. */
function useStockInvalidation() {
  const qc = useQueryClient();
  return async () => {
    await Promise.all([
      qc.invalidateQueries({ queryKey: inventoryKeys.movements }),
      qc.invalidateQueries({ queryKey: inventoryKeys.items }),
      qc.invalidateQueries({ queryKey: inventoryKeys.purchaseOrders }),
      qc.invalidateQueries({ queryKey: inventoryKeys.transfers }),
      qc.invalidateQueries({ queryKey: dashboardKeys.inventory }),
      qc.invalidateQueries({ queryKey: ['items'] }),
    ]);
  };
}

function useStockMutation<TVars>(fn: (vars: TVars) => Promise<unknown>) {
  const invalidate = useStockInvalidation();
  // Wrapped so TanStack's extra mutation-context argument never reaches an API function.
  return useMutation({ mutationFn: (vars: TVars) => fn(vars), onSuccess: invalidate });
}

type Api = typeof inventoryApi;

export const useRecordMovement = () => useStockMutation(inventoryApi.recordMovement);
export const useCreateInventoryItem = () => useStockMutation(inventoryApi.createInventoryItem);
export const useUpdateInventoryItem = () =>
  useStockMutation(({ id, body }: { id: string; body: Parameters<Api['updateInventoryItem']>[1] }) => inventoryApi.updateInventoryItem(id, body));
export const usePhysicalCount = () =>
  useStockMutation(({ id, body }: { id: string; body: Parameters<Api['physicalCount']>[1] }) => inventoryApi.physicalCount(id, body));
export const useReceiveStock = () =>
  useStockMutation(({ id, body }: { id: string; body: Parameters<Api['receiveStock']>[1] }) => inventoryApi.receiveStock(id, body));

export function useCreateSupplier() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Parameters<Api['createSupplier']>[0]) => inventoryApi.createSupplier(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: inventoryKeys.suppliers }),
  });
}

export const useCreatePurchaseOrder = () => useStockMutation(inventoryApi.createPurchaseOrder);
export const useMarkPurchaseOrderSent = () => useStockMutation(inventoryApi.markPurchaseOrderSent);
export const useCancelPurchaseOrder = () => useStockMutation(inventoryApi.cancelPurchaseOrder);
export const useReceivePurchaseOrder = () =>
  useStockMutation(({ id, body }: { id: string; body: Parameters<Api['receivePurchaseOrder']>[1] }) => inventoryApi.receivePurchaseOrder(id, body));

export const useCreateTransfer = () => useStockMutation(inventoryApi.createTransfer);
export const useMarkTransferInTransit = () => useStockMutation(inventoryApi.markTransferInTransit);
export const useMarkTransferReceived = () => useStockMutation(inventoryApi.markTransferReceived);
export const useCancelTransfer = () => useStockMutation(inventoryApi.cancelTransfer);
