import { apiClient } from '../../lib/apiClient';

/** Mirrors Purch.Application.Devices.RosterEntryDto. role: 0 Admin, 1 Manager, 2 Staff. */
export interface RosterPerson {
  membershipId: string;
  name: string;
  role: number;
  hasPin: boolean;
}

export interface DeviceRoster {
  deviceName: string | null;
  deviceType: number;
  people: RosterPerson[];
}

export interface Unlocked {
  accessToken: string;
  refreshToken: string;
  person: RosterPerson;
}

/** Both calls are anonymous: the device proves itself with its own credential, never in a URL. */
export const unlockApi = {
  roster: (deviceCredential: string) => apiClient.post<DeviceRoster>('/devices/roster', { deviceCredential }).then((r) => r.data),
  unlock: (deviceCredential: string, membershipId: string, pin: string) =>
    apiClient.post<Unlocked>('/devices/unlock', { deviceCredential, membershipId, pin }).then((r) => r.data),
};
