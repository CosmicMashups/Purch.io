import { apiClient } from '../../lib/apiClient';
import type {
  BranchTransfer,
  CreateBranchTransferRequest,
  CreateInventoryItemRequest,
  CreatePurchaseOrderRequest,
  CreateIncomingReceivingRequest,
  IncomingReceiving,
  SupplierRequest,
  InventoryCategory,
  InventoryCategoryBody,
  InventoryItem,
  InventoryMovement,
  MovementCursor,
  MovementFilter,
  PurchaseOrder,
  ConvertPurchaseRequestRequest,
  CreatePurchaseRequestRequest,
  PurchaseRequest,
  RestockSuggestion,
  SupplierLink,
  RecordMovementRequest,
  Supplier,
  UpdateInventoryItemRequest,
} from './types';

export const MOVEMENT_PAGE_SIZE = 30;

export const inventoryApi = {
  listInventoryItems: () => apiClient.get<InventoryItem[]>('/inventory-items').then((r) => r.data),
  createInventoryItem: (body: CreateInventoryItemRequest) => apiClient.post<InventoryItem>('/inventory-items', body).then((r) => r.data),
  reorderInventoryItems: (inventoryItemIds: string[]) => apiClient.put('/inventory-items/order', { inventoryItemIds }).then(() => undefined),
  updateInventoryItem: (id: string, body: UpdateInventoryItemRequest) =>
    apiClient.put<InventoryItem>(`/inventory-items/${id}`, body).then((r) => r.data),
  physicalCount: (id: string, body: { quantityOnHand: number; branchId: string }) =>
    apiClient.post<InventoryItem>(`/inventory-items/${id}/physical-count`, body).then((r) => r.data),
  receiveStock: (id: string, body: { packagesReceived: number; branchId: string; supplierReference: string | null }) =>
    apiClient.post<InventoryItem>(`/inventory-items/${id}/receive`, body).then((r) => r.data),

  listInventoryCategories: () => apiClient.get<InventoryCategory[]>('/inventory-categories').then((r) => r.data),
  createInventoryCategory: (body: InventoryCategoryBody) => apiClient.post<InventoryCategory>('/inventory-categories', body).then((r) => r.data),
  updateInventoryCategory: (id: string, body: InventoryCategoryBody) =>
    apiClient.put<InventoryCategory>(`/inventory-categories/${id}`, body).then((r) => r.data),
  deleteInventoryCategory: (id: string) => apiClient.delete(`/inventory-categories/${id}`).then(() => undefined),

  listMovements: (filter: MovementFilter, cursor: MovementCursor | null) =>
    apiClient
      .get<InventoryMovement[]>('/inventory/movements', {
        params: { ...filter, limit: MOVEMENT_PAGE_SIZE, before: cursor?.before, beforeId: cursor?.beforeId },
      })
      .then((r) => r.data),
  recordMovement: (body: RecordMovementRequest) => apiClient.post<InventoryMovement>('/inventory/movements', body).then((r) => r.data),

  listSuppliers: () => apiClient.get<Supplier[]>('/suppliers').then((r) => r.data),
  createSupplier: (body: SupplierRequest) => apiClient.post<Supplier>('/suppliers', body).then((r) => r.data),

  updateSupplier: (id: string, body: SupplierRequest & { isActive: boolean }) => apiClient.put<Supplier>(`/suppliers/${id}`, body).then((r) => r.data),

  listPurchaseOrders: () => apiClient.get<PurchaseOrder[]>('/purchase-orders').then((r) => r.data),
  createPurchaseOrder: (body: CreatePurchaseOrderRequest) => apiClient.post<PurchaseOrder>('/purchase-orders', body).then((r) => r.data),
  markPurchaseOrderSent: (id: string) => apiClient.post<PurchaseOrder>(`/purchase-orders/${id}/mark-sent`).then((r) => r.data),
  cancelPurchaseOrder: (id: string) => apiClient.post<PurchaseOrder>(`/purchase-orders/${id}/cancel`).then((r) => r.data),

  listRestockSuggestions: () => apiClient.get<RestockSuggestion[]>('/inventory/restock-suggestions').then((r) => r.data),
  listPurchaseRequests: () => apiClient.get<PurchaseRequest[]>('/purchase-requests').then((r) => r.data),
  createPurchaseRequest: (body: CreatePurchaseRequestRequest) => apiClient.post<PurchaseRequest>('/purchase-requests', body).then((r) => r.data),
  approvePurchaseRequest: (id: string) => apiClient.post<PurchaseRequest>(`/purchase-requests/${id}/approve`).then((r) => r.data),
  cancelPurchaseRequest: (id: string) => apiClient.post<PurchaseRequest>(`/purchase-requests/${id}/cancel`).then((r) => r.data),
  convertPurchaseRequest: (id: string, body: ConvertPurchaseRequestRequest) =>
    apiClient.post<PurchaseOrder[]>(`/purchase-requests/${id}/convert`, body).then((r) => r.data),
  setSuppliers: (owner: { itemId: string } | { inventoryItemId: string }, suppliers: { supplierId: string; isPreferred: boolean; unitCost: number | null }[]) => {
    const path = 'itemId' in owner ? `/items/${owner.itemId}/suppliers` : `/inventory-items/${owner.inventoryItemId}/suppliers`;
    return apiClient.put<SupplierLink[]>(path, { suppliers }).then((r) => r.data);
  },

  listIncomingReceiving: () => apiClient.get<IncomingReceiving[]>('/incoming-receiving').then((r) => r.data),
  createIncomingReceiving: (body: CreateIncomingReceivingRequest) =>
    apiClient.post<IncomingReceiving>('/incoming-receiving', body).then((r) => r.data),
  linkIncomingReceiving: (id: string, purchaseOrderId: string) =>
    apiClient.post<IncomingReceiving>(`/incoming-receiving/${id}/link-purchase-order`, { purchaseOrderId }).then((r) => r.data),

  listTransfers: () => apiClient.get<BranchTransfer[]>('/branch-transfers').then((r) => r.data),
  createTransfer: (body: CreateBranchTransferRequest) => apiClient.post<BranchTransfer>('/branch-transfers', body).then((r) => r.data),
  markTransferInTransit: (id: string) => apiClient.post<BranchTransfer>(`/branch-transfers/${id}/mark-in-transit`).then((r) => r.data),
  markTransferReceived: (id: string) => apiClient.post<BranchTransfer>(`/branch-transfers/${id}/mark-received`).then((r) => r.data),
  cancelTransfer: (id: string) => apiClient.post<BranchTransfer>(`/branch-transfers/${id}/cancel`).then((r) => r.data),
};
