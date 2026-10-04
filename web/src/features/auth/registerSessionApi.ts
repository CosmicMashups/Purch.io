import { apiClient } from '../../lib/apiClient';
import { useAuthStore } from '../../lib/authStore';

export interface RegisterChoice {
  deviceId: string;
  name: string;
}

export type RegisterSessionResult = { done: true } | { done: false; registers: RegisterChoice[] };

/**
 * Ties an Admin's or Manager's email session to a Register of the business, so they can sell without signing out.
 * With several Registers and none chosen, the API answers with the list to pick from.
 */
export async function startRegisterSession(deviceId?: string): Promise<RegisterSessionResult> {
  const { data } = await apiClient.post<{ accessToken?: string; refreshToken?: string; chooseRegister?: boolean; registers?: RegisterChoice[] }>(
    '/auth/register-session',
    deviceId ? { deviceId } : {},
  );
  if (data.chooseRegister) return { done: false, registers: data.registers ?? [] };

  const store = useAuthStore.getState();
  const previousRefresh = store.refreshToken;
  store.setTokens(data.accessToken!, data.refreshToken!);
  // The old email-only session is replaced; revoke it so it cannot keep renewing. Best effort.
  if (previousRefresh) void apiClient.post('/auth/logout', { refreshToken: previousRefresh }).catch(() => undefined);
  return { done: true };
}
