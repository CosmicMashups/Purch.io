import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
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
    refreshPromise = refreshClient
      .post<{ accessToken: string; refreshToken: string }>('/auth/refresh', { refreshToken })
      .then((res) => {
        useAuthStore.getState().setTokens(res.data.accessToken, res.data.refreshToken);
        return res.data.accessToken;
      })
      .catch(() => {
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
