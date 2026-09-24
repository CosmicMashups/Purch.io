import { apiClient } from '../../lib/apiClient';
import type { AddLineRequest, RecordPaymentRequest, Transaction } from './types';

/** The server owns the cart and prices it. Every call returns the whole priced cart to render as-is. */
export const posApi = {
  getCart: () => apiClient.get<Transaction>('/transactions/cart').then((r) => r.data),
  addLine: (body: AddLineRequest) => apiClient.post<Transaction>('/transactions/cart/lines', body).then((r) => r.data),
  updateLine: (lineId: string, quantity: number) =>
    apiClient.put<Transaction>(`/transactions/cart/lines/${lineId}`, { quantity }).then((r) => r.data),
  removeLine: (lineId: string) => apiClient.delete<Transaction>(`/transactions/cart/lines/${lineId}`).then((r) => r.data),
  voidCart: () => apiClient.post<Transaction>('/transactions/cart/void').then((r) => r.data),
  applyPromoCode: (code: string | null) => apiClient.put<Transaction>('/transactions/cart/promo-code', { code }).then((r) => r.data),
  applySeniorPwd: (apply: boolean) => apiClient.put<Transaction>('/transactions/cart/senior-pwd-discount', { apply }).then((r) => r.data),
  setOrderType: (orderType: string) => apiClient.put<Transaction>('/transactions/cart/order-type', { orderType }).then((r) => r.data),
  listKioskPending: (branchId: string) => apiClient.get<Transaction[]>('/transactions/kiosk-pending', { params: { branchId } }).then((r) => r.data),
  claimKioskOrder: (transactionId: string) => apiClient.post<Transaction>(`/transactions/kiosk-pending/${transactionId}/claim`).then((r) => r.data),
  pay: (body: RecordPaymentRequest) => apiClient.post<Transaction>('/transactions/cart/payments', body).then((r) => r.data),
};
