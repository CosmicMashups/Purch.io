import { apiClient } from '../../lib/apiClient';
import type { FlaggedSyncRecord, InventoryDashboard, SalesDashboard } from './types';

export const dashboardApi = {
  sales: (range?: { from: string; to: string } | null) =>
    apiClient.get<SalesDashboard>('/reports/sales-dashboard', { params: range ?? undefined }).then((r) => r.data),
  inventory: () => apiClient.get<InventoryDashboard>('/inventory/dashboard').then((r) => r.data),
  flaggedSync: () => apiClient.get<FlaggedSyncRecord[]>('/sync/flagged').then((r) => r.data),
};
