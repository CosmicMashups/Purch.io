import { apiClient } from '../../lib/apiClient';
import { useAuthStore } from '../../lib/authStore';

export async function signOut(): Promise<void> {
  const { refreshToken, clearTokens } = useAuthStore.getState();
  try {
    // Revoke server-side so the refresh token can't be replayed; a failure must not trap the user signed in.
    if (refreshToken) await apiClient.post('/auth/logout', { refreshToken });
  } catch {
    // Ignored: the local session is cleared regardless.
  }
  clearTokens();
}
