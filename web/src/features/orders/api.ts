import { apiClient } from '../../lib/apiClient';
import type { Transaction } from '../pos/types';

export type OrderStatusName = 'Completed' | 'Voided' | 'Refunded' | 'Exchanged';
export type OrderMethodName = 'Cash' | 'QrPh' | 'BankTransfer' | 'ManualGcashQr' | 'BillPaymentELoad' | 'UtangCredit' | 'Split';

/** What the Orders list is narrowed to. Dates are UTC instants; the page turns the viewer's own days into these. */
export interface OrdersFilter {
  from?: string;
  to?: string;
  status?: OrderStatusName;
  branchId?: string;
  deviceId?: string;
  staffUserId?: string;
  method?: OrderMethodName;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** Mirrors Purch.Application.Orders.OrderListItemDto. */
export interface OrderRow {
  id: string;
  receiptNumber: number | null;
  at: string;
  status: string;
  hasExchange: boolean;
  branchId: string;
  branchName: string;
  deviceId: string;
  deviceName: string;
  staffName: string | null;
  customerName: string | null;
  paymentMethods: string[];
  totalAmount: number;
}

export interface OrdersPage {
  items: OrderRow[];
  total: number;
  page: number;
  pageSize: number;
}

function params(filter: OrdersFilter) {
  return Object.fromEntries(Object.entries(filter).filter(([, value]) => value !== undefined && value !== ''));
}

export const ordersApi = {
  list: (filter: OrdersFilter) => apiClient.get<OrdersPage>('/orders', { params: params(filter) }).then((r) => r.data),
  detail: (id: string) => apiClient.get<Transaction>(`/orders/${id}`).then((r) => r.data),
  exportCsv: (filter: OrdersFilter) => apiClient.get<string>('/orders/export.csv', { params: params({ ...filter, page: undefined, pageSize: undefined }), responseType: 'text' }).then((r) => r.data),
};
