import { apiClient } from '../../lib/apiClient';
import type { AddLineRequest, KitchenStatus, Transaction } from '../pos/types';
import type { KioskPromoRulesResponse } from './promoRules';

const PENDING_PATH = { KitchenDisplay: '/kitchen-display/pending', OrderBoard: '/order-board/pending' } as const;

/** What a device keeps after pairing. The credential is shown once and identifies the device from then on. */
export interface PairedDevice {
  deviceCredential: string;
  deviceId: string;
  tenantId: string;
  branchId: string;
  deviceType: number;
  name: string | null;
}

export interface DeviceSession {
  accessToken: string | null;
  refreshToken: string | null;
  requiresStaff: boolean;
  deviceId: string;
  deviceType: number;
  name: string | null;
}

/** Both calls are anonymous: the one-time code pairs the device, and its own credential starts every session after that. */
export const deviceApi = {
  pair: (pairingCode: string) => apiClient.post<PairedDevice>('/devices/pair', { pairingCode }).then((r) => r.data),
  startSession: (deviceCredential: string) => apiClient.post<DeviceSession>('/devices/session', { deviceCredential }).then((r) => r.data),
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
  /** The active automatic promotions (no promo codes) the cart totals itself with. */
  promoRules: () => apiClient.get<KioskPromoRulesResponse>('/kiosk/promo-rules').then((r) => r.data),
  placeOrder: (body: PlaceKioskOrderRequest) => apiClient.post<Transaction>('/kiosk/cart/place-order', body).then((r) => r.data),
};

export const displayApi = {
  pending: (role: keyof typeof PENDING_PATH, branchId: string) =>
    apiClient.get<Transaction[]>(PENDING_PATH[role], { params: { branchId } }).then((r) => r.data),
  setKitchenStatus: (transactionId: string, kitchenStatus: KitchenStatus) =>
    apiClient.put<Transaction>(`/kitchen-display/orders/${transactionId}/status`, { kitchenStatus }).then((r) => r.data),
};
