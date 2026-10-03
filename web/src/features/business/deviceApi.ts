import { apiClient } from '../../lib/apiClient';

/** Mirrors Purch.Application.Onboarding.DeviceDto. The fields after lastSeenAt are new, so the older ones stay as they were. */
export interface Device {
  id: string;
  branchId: string;
  deviceIdentifier: string | null;
  deviceType: number;
  lastSeenAt: string | null;
  name?: string | null;
  /** 0 Active, 1 Waiting for its code, 2 Revoked (see DeviceStatus). */
  status?: number;
  pairedAt?: string | null;
  pairingCodeExpiresAt?: string | null;
  linkedRegisterDeviceId?: string | null;
}

export interface CreatePairingBody {
  name: string;
  deviceType: number;
  branchId: string;
  linkedRegisterDeviceId: string | null;
}

/** The one-time code is shown to the Admin once; the server keeps only a hash of it. */
export interface PairingCode {
  device: Device;
  pairingCode: string;
  expiresAt: string;
}

export const deviceApi = {
  list: () => apiClient.get<Device[]>('/devices').then((r) => r.data),
  createPairing: (body: CreatePairingBody) => apiClient.post<PairingCode>('/devices/pairing-requests', body).then((r) => r.data),
  newPairingCode: (id: string) => apiClient.post<PairingCode>(`/devices/${id}/pairing-code`).then((r) => r.data),
  revoke: (id: string) => apiClient.post<Device>(`/devices/${id}/revoke`).then((r) => r.data),
};
