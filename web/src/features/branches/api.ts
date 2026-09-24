import { apiClient } from '../../lib/apiClient';
import type { Branch } from './types';

export const branchesApi = {
  list: () => apiClient.get<Branch[]>('/branches').then((r) => r.data),
};
