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

export interface PlaceKioskOrderRequest {
  orderId: string;
  lines: AddLineRequest[];
  orderType: string;
}

/** The kiosk's cart lives on the device (see localCart.ts); placeOrder is the one call that builds
 * and submits it, carrying its own idempotency key so a retry after a lost response never double-sends. */
export const kioskApi = {
  branding: () => apiClient.get<{ kioskPosterImageUrl: string | null }>('/kiosk/branding').then((r) => r.data),
  placeOrder: (body: PlaceKioskOrderRequest) => apiClient.post<Transaction>('/kiosk/cart/place-order', body).then((r) => r.data),
};

export const displayApi = {
  pending: (role: keyof typeof PENDING_PATH, branchId: string) =>
    apiClient.get<Transaction[]>(PENDING_PATH[role], { params: { branchId } }).then((r) => r.data),
  setKitchenStatus: (transactionId: string, kitchenStatus: KitchenStatus) =>
    apiClient.put<Transaction>(`/kitchen-display/orders/${transactionId}/status`, { kitchenStatus }).then((r) => r.data),
};
