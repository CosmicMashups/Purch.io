import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from './api';

export const dashboardKeys = {
  sales: ['dashboard', 'sales'] as const,
  inventory: ['dashboard', 'inventory'] as const,
  flaggedSync: ['dashboard', 'flaggedSync'] as const,
};

// `enabled` mirrors the API's role rules so a role never fires a call it is certain to be refused.
export const useSalesDashboard = (enabled: boolean) =>
  useQuery({ queryKey: dashboardKeys.sales, queryFn: dashboardApi.sales, enabled });

export const useInventoryDashboard = (enabled: boolean) =>
  useQuery({ queryKey: dashboardKeys.inventory, queryFn: dashboardApi.inventory, enabled });

export const useFlaggedSync = (enabled: boolean) =>
  useQuery({ queryKey: dashboardKeys.flaggedSync, queryFn: dashboardApi.flaggedSync, enabled });
