import { apiClient } from '../../lib/apiClient';

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
}

export const authApi = {
  pinLogin: (devicePairingCode: string, pin: string) =>
    apiClient.post<LoginResponse>('/auth/login', { devicePairingCode, pin }).then((r) => r.data),

  adminLogin: (email: string, password: string) =>
    apiClient.post<LoginResponse>('/auth/admin-login', { email, password }).then((r) => r.data),
};
