import { create } from 'zustand';
import { queryClient } from './queryClient';

const ACCESS_TOKEN_KEY = 'purch.accessToken';
const REFRESH_TOKEN_KEY = 'purch.refreshToken';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  setTokens: (accessToken: string, refreshToken: string) => void;
  clearTokens: () => void;
  /** Adopts whatever another tab last wrote to localStorage. */
  syncFromStorage: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: localStorage.getItem(ACCESS_TOKEN_KEY),
  refreshToken: localStorage.getItem(REFRESH_TOKEN_KEY),
  setTokens: (accessToken, refreshToken) => {
    localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    set({ accessToken, refreshToken });
  },
  clearTokens: () => {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    set({ accessToken: null, refreshToken: null });
    // The cache is not keyed by tenant, so whoever signs in next must never see this session's data.
    queryClient.clear();
  },
  syncFromStorage: () => {
    const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);
    if (!accessToken) queryClient.clear();
    set({ accessToken, refreshToken: localStorage.getItem(REFRESH_TOKEN_KEY) });
  },
}));

// Refresh tokens are single-use, so two tabs must never each hold their own copy: the moment one
// tab rotates the pair (or signs out), every other tab adopts it, instead of redeeming a token that
// is already spent and getting logged out.
window.addEventListener('storage', (event) => {
  if (event.key === null || event.key === ACCESS_TOKEN_KEY || event.key === REFRESH_TOKEN_KEY) {
    useAuthStore.getState().syncFromStorage();
  }
});
