import { apiClient } from '../../lib/apiClient';
import { useAuthStore } from '../../lib/authStore';

// Set by an explicit "Sign out" so the route guard sends the person to the email login rather than back to a
// paired device's PIN screen (which would look like they are still inside the tenant). Cleared on the next sign-in.
let leftTenant = false;
useAuthStore.subscribe((state) => {
  if (state.accessToken) leftTenant = false;
});

/** True after an explicit sign-out until someone signs in again; RequireAuth uses it to pick /login over /unlock. */
export const hasLeftTenant = (): boolean => leftTenant;

export async function signOut(options: { leaveTenant?: boolean } = {}): Promise<void> {
  const { refreshToken, clearTokens } = useAuthStore.getState();
  try {
    // Revoke server-side so the refresh token can't be replayed; a failure must not trap the user signed in.
    if (refreshToken) await apiClient.post('/auth/logout', { refreshToken });
  } catch {
    // Ignored: the local session is cleared regardless.
  }
  leftTenant = options.leaveTenant === true;
  clearTokens();
}
