import { create } from 'zustand';
import { clearOfflineCache } from '../offline/db/persister';
import { queryClient } from './queryClient';

const ACCESS_TOKEN_KEY = 'purch.accessToken';
const REFRESH_TOKEN_KEY = 'purch.refreshToken';

/**
 * Safe localStorage accessors that gracefully handle restricted iframe sandboxes,
 * private browsing modes where storage access throws a SecurityError, or SSR.
 */
function safeGetStorage(key: string): string | null {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return null;
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function safeSetStorage(key: string, value: string): void {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.setItem(key, value);
    }
  } catch {
    // QuotaExceededError or security restrictions — fails gracefully in memory
  }
}

function safeRemoveStorage(key: string): void {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.removeItem(key);
    }
  } catch {}
}

/**
 * SECURITY ARCHITECTURE NOTE:
 * - Storage Mechanism: In this client-side SPA (hosted statically on S3/Firebase/Vercel
 *   without a stateful Backend-For-Frontend proxy), auth tokens reside in localStorage to
 *   facilitate multi-tab synchronization and single-use refresh token rotation.
 * - XSS Risk & Defense-in-Depth: Because localStorage is readable by any JS in the origin,
 *   application security relies on:
 *   1. Strict Content-Security-Policy (CSP) headers (blocking unsafe-inline and untrusted scripts).
 *   2. Zero use of raw `dangerouslySetInnerHTML` on untrusted backend/user input.
 *   3. Modern framework sanitization (React JSX auto-escaping).
 * - Multi-Tenant Isolation: When tokens are cleared or replaced, `queryClient.clear()` is
 *   synchronously invoked to prevent tenant query data from lingering in browser memory.
 */
interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  setTokens: (accessToken: string, refreshToken: string) => void;
  clearTokens: () => void;
  /** Adopts whatever another tab last wrote to localStorage. */
  syncFromStorage: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: safeGetStorage(ACCESS_TOKEN_KEY),
  refreshToken: safeGetStorage(REFRESH_TOKEN_KEY),
  setTokens: (accessToken, refreshToken) => {
    safeSetStorage(ACCESS_TOKEN_KEY, accessToken);
    safeSetStorage(REFRESH_TOKEN_KEY, refreshToken);
    set({ accessToken, refreshToken });
  },
  clearTokens: () => {
    safeRemoveStorage(ACCESS_TOKEN_KEY);
    safeRemoveStorage(REFRESH_TOKEN_KEY);
    set({ accessToken: null, refreshToken: null });
    // The cache is not keyed by tenant, so whoever signs in next must never see this session's data.
    queryClient.clear();
    // Saved offline data belongs to the business that just signed out; it must not outlive the session.
    void clearOfflineCache();
  },
  syncFromStorage: () => {
    const accessToken = safeGetStorage(ACCESS_TOKEN_KEY);
    if (!accessToken) queryClient.clear();
    set({ accessToken, refreshToken: safeGetStorage(REFRESH_TOKEN_KEY) });
  },
}));

// Refresh tokens are single-use, so two tabs must never each hold their own copy: the moment one
// tab rotates the pair (or signs out), every other tab adopts it, instead of redeeming a token that
// is already spent and getting logged out.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === null || event.key === ACCESS_TOKEN_KEY || event.key === REFRESH_TOKEN_KEY) {
      useAuthStore.getState().syncFromStorage();
    }
  });
}
