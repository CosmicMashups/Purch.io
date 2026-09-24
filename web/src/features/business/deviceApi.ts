import { apiClient } from '../../lib/apiClient';

/** Mirrors Purch.Application.Onboarding.DeviceDto. */
export interface Device {
  id: string;
  branchId: string;
  pairingCode: string;
  deviceIdentifier: string | null;
  deviceType: number;
  lastSeenAt: string | null;
}

export interface CreateDeviceBody {
  branchId: string;
  deviceIdentifier: string | null;
  deviceType: number;
  pairingPin: string | null;
}

export const deviceApi = {
  list: () => apiClient.get<Device[]>('/devices').then((r) => r.data),
  create: (body: CreateDeviceBody) => apiClient.post<Device>('/devices', body).then((r) => r.data),
  resetPairingCode: (id: string) => apiClient.post<Device>(`/devices/${id}/reset-pairing-code`).then((r) => r.data),
  resetPairingPin: (id: string, newPin: string) => apiClient.post<Device>(`/devices/${id}/reset-pairing-pin`, { newPin }).then((r) => r.data),
};
