import { apiClient } from '../../lib/apiClient';

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
}

export interface BusinessChoice {
  tenantId: string;
  name: string;
}

export type SignInResponse = ({ chooseBusiness?: false } & LoginResponse) | { chooseBusiness: true; businesses: BusinessChoice[] };

export const authApi = {
  /** Email and password. A person who belongs to several businesses gets the list back and signs in again with the one they pick. */
  signIn: (email: string, password: string, tenantId?: string) =>
    apiClient.post<SignInResponse>('/auth/sign-in', { email, password, tenantId: tenantId ?? null }).then((r) => r.data),

  pinLogin: (devicePairingCode: string, pin: string) =>
    apiClient.post<LoginResponse>('/auth/login', { devicePairingCode, pin }).then((r) => r.data),

  adminLogin: (email: string, password: string) =>
    apiClient.post<LoginResponse>('/auth/admin-login', { email, password }).then((r) => r.data),
};
