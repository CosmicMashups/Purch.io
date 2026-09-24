import { apiClient } from '../../lib/apiClient';
import type {
  BranchTransfer,
  CreateBranchTransferRequest,
  CreateInventoryItemRequest,
  CreatePurchaseOrderRequest,
  CreateSupplierRequest,
  InventoryItem,
  InventoryMovement,
  MovementCursor,
  MovementFilter,
  PurchaseOrder,
  ReceivePurchaseOrderRequest,
  RecordMovementRequest,
  Supplier,
  UpdateInventoryItemRequest,
} from './types';

export const MOVEMENT_PAGE_SIZE = 30;

export const inventoryApi = {
  listInventoryItems: () => apiClient.get<InventoryItem[]>('/inventory-items').then((r) => r.data),
  createInventoryItem: (body: CreateInventoryItemRequest) => apiClient.post<InventoryItem>('/inventory-items', body).then((r) => r.data),
  updateInventoryItem: (id: string, body: UpdateInventoryItemRequest) =>
    apiClient.put<InventoryItem>(`/inventory-items/${id}`, body).then((r) => r.data),
  physicalCount: (id: string, body: { quantityOnHand: number; branchId: string }) =>
    apiClient.post<InventoryItem>(`/inventory-items/${id}/physical-count`, body).then((r) => r.data),
  receiveStock: (id: string, body: { packagesReceived: number; branchId: string; supplierReference: string | null }) =>
    apiClient.post<InventoryItem>(`/inventory-items/${id}/receive`, body).then((r) => r.data),

  listMovements: (filter: MovementFilter, cursor: MovementCursor | null) =>
    apiClient
      .get<InventoryMovement[]>('/inventory/movements', {
        params: { ...filter, limit: MOVEMENT_PAGE_SIZE, before: cursor?.before, beforeId: cursor?.beforeId },
      })
      .then((r) => r.data),
  recordMovement: (body: RecordMovementRequest) => apiClient.post<InventoryMovement>('/inventory/movements', body).then((r) => r.data),

  listSuppliers: () => apiClient.get<Supplier[]>('/suppliers').then((r) => r.data),
  createSupplier: (body: CreateSupplierRequest) => apiClient.post<Supplier>('/suppliers', body).then((r) => r.data),

  listPurchaseOrders: () => apiClient.get<PurchaseOrder[]>('/purchase-orders').then((r) => r.data),
  createPurchaseOrder: (body: CreatePurchaseOrderRequest) => apiClient.post<PurchaseOrder>('/purchase-orders', body).then((r) => r.data),
  markPurchaseOrderSent: (id: string) => apiClient.post<PurchaseOrder>(`/purchase-orders/${id}/mark-sent`).then((r) => r.data),
  cancelPurchaseOrder: (id: string) => apiClient.post<PurchaseOrder>(`/purchase-orders/${id}/cancel`).then((r) => r.data),
  receivePurchaseOrder: (id: string, body: ReceivePurchaseOrderRequest) =>
    apiClient.post<PurchaseOrder>(`/purchase-orders/${id}/receive`, body).then((r) => r.data),

  listTransfers: () => apiClient.get<BranchTransfer[]>('/branch-transfers').then((r) => r.data),
  createTransfer: (body: CreateBranchTransferRequest) => apiClient.post<BranchTransfer>('/branch-transfers', body).then((r) => r.data),
  markTransferInTransit: (id: string) => apiClient.post<BranchTransfer>(`/branch-transfers/${id}/mark-in-transit`).then((r) => r.data),
  markTransferReceived: (id: string) => apiClient.post<BranchTransfer>(`/branch-transfers/${id}/mark-received`).then((r) => r.data),
  cancelTransfer: (id: string) => apiClient.post<BranchTransfer>(`/branch-transfers/${id}/cancel`).then((r) => r.data),
};
