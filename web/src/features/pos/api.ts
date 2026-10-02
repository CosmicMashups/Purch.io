import { apiClient } from '../../lib/apiClient';
import type { AddLineRequest, Adjustment, CreateExchangeRequest, RecordPaymentRequest, RefundTransactionRequest, Transaction } from './types';

/** The server owns the cart and prices it. Every call returns the whole priced cart to render as-is. */
export const posApi = {
  getCart: () => apiClient.get<Transaction>('/transactions/cart').then((r) => r.data),
  addLine: (body: AddLineRequest) => apiClient.post<Transaction>('/transactions/cart/lines', body).then((r) => r.data),
  /** Several adds in one call, all or nothing. The batch id makes a retry safe: the server applies each id once. It carries no prices. */
  addLines: (batchId: string, lines: AddLineRequest[]) =>
    apiClient.post<Transaction>('/transactions/cart/lines/batch', { batchId, lines }).then((r) => r.data),
  /** approverPin is only ever required on a claimed kiosk order the kitchen hasn't started yet — see ApproverPinDialog. */
  updateLine: (lineId: string, quantity: number, approverPin?: string) =>
    apiClient.put<Transaction>(`/transactions/cart/lines/${lineId}`, { quantity, approverPin }).then((r) => r.data),
  removeLine: (lineId: string, approverPin?: string) =>
    apiClient.delete<Transaction>(`/transactions/cart/lines/${lineId}`, { params: approverPin ? { approverPin } : undefined }).then((r) => r.data),
  /** approverPin is required whenever the cart holds anything — a different Admin/Manager, unless the tenant has only one. */
  voidCart: (approverPin?: string) => apiClient.post<Transaction>('/transactions/cart/void', { approverPin }).then((r) => r.data),
  applyPromoCode: (code: string | null) => apiClient.put<Transaction>('/transactions/cart/promo-code', { code }).then((r) => r.data),
  applySeniorPwd: (apply: boolean) => apiClient.put<Transaction>('/transactions/cart/senior-pwd-discount', { apply }).then((r) => r.data),
  setOrderType: (orderType: string) => apiClient.put<Transaction>('/transactions/cart/order-type', { orderType }).then((r) => r.data),
  listKioskPending: (branchId: string) => apiClient.get<Transaction[]>('/transactions/kiosk-pending', { params: { branchId } }).then((r) => r.data),
  claimKioskOrder: (transactionId: string) => apiClient.post<Transaction>(`/transactions/kiosk-pending/${transactionId}/claim`).then((r) => r.data),
  pay: (body: RecordPaymentRequest) => apiClient.post<Transaction>('/transactions/cart/payments', body).then((r) => r.data),
  refund: (transactionId: string, body: RefundTransactionRequest) => apiClient.post<Transaction>(`/transactions/${transactionId}/refund`, body).then((r) => r.data),
  exchange: (transactionId: string, body: CreateExchangeRequest) =>
    apiClient.post<Adjustment>(`/transactions/${transactionId}/exchange`, body).then((r) => r.data),
  /** How much of each line can still be returned, after earlier exchanges. */
  returnableLines: (transactionId: string) =>
    apiClient.get<{ lineId: string; remainingQuantity: number }[]>(`/transactions/${transactionId}/returnable-lines`).then((r) => r.data),
  /** Finds a completed sale by its receipt number, to refund or exchange one the cashier doesn't already
   * have open. Receipt numbers are only unique per device, so more than one sale can come back. */
  findByReceiptNumber: (receiptNumber: number) => apiClient.get<Transaction[]>(`/transactions/by-receipt/${receiptNumber}`).then((r) => r.data),
};
