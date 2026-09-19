import axios, { isAxiosError, type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from './authStore';
import { ApiError, type ApiErrorKind } from './apiError';

const baseURL = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:5001';

export const apiClient = axios.create({
  baseURL,
  timeout: 15000,
});

// Separate instance for refresh calls so the 401 interceptor below never recurses on itself.
const refreshClient = axios.create({ baseURL, timeout: 15000 });

const NO_AUTH_REFRESH_PATHS = ['/auth/login', '/auth/admin-login', '/auth/refresh', '/kiosk/session'];

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

async function refreshAccessToken(): Promise<string | null> {
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) return null;
  if (!refreshPromise) {
    const spentToken = refreshToken;
    refreshPromise = refreshClient
      .post<{ accessToken: string; refreshToken: string }>('/auth/refresh', { refreshToken: spentToken })
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
  const message = body.detail ?? body.title ?? error.message;

  const kindByStatus: Record<number, ApiErrorKind> = {
    400: 'validation',
    401: 'unauthorized',
    403: 'forbidden',
    404: 'notFound',
    409: 'conflict',
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
