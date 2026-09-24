import { apiClient } from '../../lib/apiClient';
import type { BootstrapBody } from './rules';

/** Mirrors Purch.Application.Onboarding.BootstrapTenantResult. */
export interface BootstrapResult {
  tenantId: string;
  branchId: string;
  deviceId: string;
  devicePairingCode: string;
  adminUserId: string;
}

export const onboardingApi = {
  /** Anonymous: the business does not exist yet, so there is nobody to sign in as. */
  bootstrap: (body: BootstrapBody) => apiClient.post<BootstrapResult>('/onboarding/bootstrap', body).then((r) => r.data),
};
