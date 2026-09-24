import { apiClient } from '../../lib/apiClient';
import type { AddLineRequest, KitchenStatus, Transaction } from '../pos/types';
import type { DeviceRole } from './deviceRoles';

interface SessionResponse {
  accessToken: string;
  refreshToken: string;
}

const SESSION_PATH: Record<DeviceRole, string> = {
  Kiosk: '/kiosk/session',
  KitchenDisplay: '/kitchen-display/session',
  OrderBoard: '/order-board/session',
};

const PENDING_PATH = { KitchenDisplay: '/kitchen-display/pending', OrderBoard: '/order-board/pending' } as const;

/** Pairing is anonymous: the device code and its PIN are the credentials. */
export const deviceApi = {
  pair: (role: DeviceRole, devicePairingCode: string, pairingPin: string) =>
    apiClient.post<SessionResponse>(SESSION_PATH[role], { devicePairingCode, pairingPin }).then((r) => r.data),
};

/** A kiosk's own cart. The server prices it and returns the whole cart on every call. */
export const kioskApi = {
  getCart: () => apiClient.get<Transaction>('/kiosk/cart').then((r) => r.data),
  addLine: (body: AddLineRequest) => apiClient.post<Transaction>('/kiosk/cart/lines', body).then((r) => r.data),
  updateLine: (lineId: string, quantity: number) => apiClient.put<Transaction>(`/kiosk/cart/lines/${lineId}`, { quantity }).then((r) => r.data),
  removeLine: (lineId: string) => apiClient.delete<Transaction>(`/kiosk/cart/lines/${lineId}`).then((r) => r.data),
  setOrderType: (orderType: string) => apiClient.put<Transaction>('/kiosk/cart/order-type', { orderType }).then((r) => r.data),
  submit: () => apiClient.post<Transaction>('/kiosk/cart/submit').then((r) => r.data),
  branding: () => apiClient.get<{ kioskPosterImageUrl: string | null }>('/kiosk/branding').then((r) => r.data),
};

export const displayApi = {
  pending: (role: keyof typeof PENDING_PATH, branchId: string) =>
    apiClient.get<Transaction[]>(PENDING_PATH[role], { params: { branchId } }).then((r) => r.data),
  setKitchenStatus: (transactionId: string, kitchenStatus: KitchenStatus) =>
    apiClient.put<Transaction>(`/kitchen-display/orders/${transactionId}/status`, { kitchenStatus }).then((r) => r.data),
};
