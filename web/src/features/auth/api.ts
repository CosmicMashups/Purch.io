import { apiClient } from '../../lib/apiClient';

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
}

export const authApi = {
  adminLogin: (email: string, password: string) =>
    apiClient.post<LoginResponse>('/auth/admin-login', { email, password }).then((r) => r.data),
};
