import axios, { isAxiosError, type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { API_BASE_URL as baseURL } from './apiBase';
import { useAuthStore } from './authStore';
import { ApiError, type ApiErrorKind } from './apiError';

export const apiClient = axios.create({
  baseURL,
  // A cold-starting serverless backend can take longer than a warm one to answer its first request.
  timeout: 30000,
});

// Separate instance for refresh calls so the 401 interceptor below never recurses on itself.
const refreshClient = axios.create({ baseURL, timeout: 15000 });

const NO_AUTH_REFRESH_PATHS = ['/auth/login', '/auth/admin-login', '/auth/refresh', '/kiosk/session', '/kitchen-display/session', '/order-board/session'];

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

interface RetriableConfig extends InternalAxiosRequestConfig {
  _retried?: boolean;
}

let refreshPromise: Promise<string | null> | null = null;

const REFRESH_ATTEMPTS = 3;
const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

/** A refresh attempt that fails for a reason that says nothing about the session (no connection, a timeout, a
 * 5xx from a cold-starting or briefly unreachable backend) is tried again a couple of times, so one bad
 * moment does not fail every request that happened to be waiting on the new token. */
async function postRefresh(spentToken: string) {
  for (let attempt = 1; ; attempt++) {
    try {
      return await refreshClient.post<{ accessToken: string; refreshToken: string }>('/auth/refresh', { refreshToken: spentToken });
    } catch (error) {
      const status = isAxiosError(error) ? error.response?.status : undefined;
      const transient = isAxiosError(error) && (status === undefined || status >= 500);
      if (!transient || attempt >= REFRESH_ATTEMPTS) throw error;
      await wait(500 * 2 ** (attempt - 1));
    }
  }
}

/** Milliseconds until the access token expires, or null if it can't be read. */
function msUntilExpiry(token: string | null): number | null {
  if (!token) return null;
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as { exp?: number };
    return typeof payload.exp === 'number' ? payload.exp * 1000 - Date.now() : null;
  } catch {
    return null;
  }
}

const REFRESH_AHEAD_MS = 2 * 60 * 1000;

/** Renews the access token shortly before it expires, so a screen full of queries never all hit a 401 at once. */
export function refreshIfExpiringSoon(): void {
  const { accessToken, refreshToken } = useAuthStore.getState();
  if (!refreshToken) return;
  const left = msUntilExpiry(accessToken);
  if (left !== null && left < REFRESH_AHEAD_MS) void refreshAccessToken();
}

if (typeof window !== 'undefined') {
  setInterval(refreshIfExpiringSoon, 30_000);
  window.addEventListener('online', refreshIfExpiringSoon);
  window.addEventListener('focus', refreshIfExpiringSoon);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') refreshIfExpiringSoon();
  });
}

async function refreshAccessToken(): Promise<string | null> {
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) return null;
  if (!refreshPromise) {
    const spentToken = refreshToken;
    refreshPromise = postRefresh(spentToken)
      .then((res) => {
        useAuthStore.getState().setTokens(res.data.accessToken, res.data.refreshToken);
        return res.data.accessToken;
      })
      .catch((error: unknown) => {
        // Only a definitive 401 means the refresh token is dead. A dropped connection, a timeout
        // or a 5xx (a cold-starting backend, a database hiccup) says nothing about the session, so
        // the tokens stay put and the next request simply tries again — wiping them here is what
        // used to sign people out over a few seconds of bad signal.
        if (!isAxiosError(error) || error.response?.status !== 401) {
          return null;
        }

        // Another tab may have rotated the pair first, making our copy the stale one: adopt
        // theirs rather than logging out.
        useAuthStore.getState().syncFromStorage();
        const latest = useAuthStore.getState();
        if (latest.refreshToken && latest.refreshToken !== spentToken) {
          return latest.accessToken;
        }

        useAuthStore.getState().clearTokens();
        return null;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }
  return refreshPromise;
}

function mapError(error: AxiosError): ApiError {
  if (!error.response) {
    return new ApiError('network', error.message || 'Network error');
  }

  const { status, data } = error.response;
  const body = (data ?? {}) as { title?: string; detail?: string; errors?: Record<string, string[]> };
  // A broken rule comes back as { title: 'Validation failed.', errors: {...} } with no detail, so the generic
  // title would hide the one sentence that says what to fix. Prefer the first specific message.
  const firstFieldMessage = Object.values(body.errors ?? {}).flat()[0];
  const message =
    status === 429
      ? 'Too many attempts. Please wait a few minutes and try again.'
      : (body.detail ?? firstFieldMessage ?? body.title ?? error.message);

  const kindByStatus: Record<number, ApiErrorKind> = {
    400: 'validation',
    401: 'unauthorized',
    403: 'forbidden',
    404: 'notFound',
    409: 'conflict',
    429: 'rateLimited',
    503: 'serviceUnavailable',
  };

  const kind = kindByStatus[status] ?? 'unknown';
  return new ApiError(kind, message, body.errors);
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetriableConfig | undefined;
    const status = error.response?.status;
    const path = config?.url ?? '';
    const isAuthPath = NO_AUTH_REFRESH_PATHS.some((p) => path.includes(p));

    if (status === 401 && config && !config._retried && !isAuthPath) {
      config._retried = true;
      const newToken = await refreshAccessToken();
      if (newToken) {
        config.headers.Authorization = `Bearer ${newToken}`;
        return apiClient(config);
      }
    }

    return Promise.reject(mapError(error));
  },
);
