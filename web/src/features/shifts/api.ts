import { apiClient } from '../../lib/apiClient';
import type { CloseShiftRequest, Shift } from './types';

export const shiftsApi = {
  /** The API answers the JSON literal `null` when the device has no open shift. */
  current: () => apiClient.get<Shift | null>('/shifts/current').then((r) => r.data ?? null),
  open: (openingCashAmount: number) => apiClient.post<Shift>('/shifts/open', { openingCashAmount }).then((r) => r.data),
  close: (body: CloseShiftRequest) => apiClient.post<Shift>('/shifts/close', body).then((r) => r.data),
};
