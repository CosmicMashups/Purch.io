import { useQuery, useQueryClient } from '@tanstack/react-query';
import { dashboardApi } from './api';

export const dashboardKeys = {
  sales: ['dashboard', 'sales'] as const,
  inventory: ['dashboard', 'inventory'] as const,
  flaggedSync: ['dashboard', 'flaggedSync'] as const,
};

/**
 * How long a cached dashboard figure may sit before a revisit refetches it in the background. Kept well
 * under a minute so it still feels live, but long enough that quickly switching tabs back and forth costs
 * the backend nothing — a fresh figure a sale or stock change actually changed arrives right away anyway,
 * because saving one invalidates these keys directly instead of waiting out this window.
 */
const DASHBOARD_STALE_MS = 45_000;

// `enabled` mirrors the API's role rules so a role never fires a call it is certain to be refused. The cached
// figure (from this session or, after a reload, from the saved copy — see offline/db/cachePolicy.ts) is shown
// at once; a revisit only refetches once it is older than DASHBOARD_STALE_MS.
export const useSalesDashboard = (enabled: boolean) =>
  useQuery({ queryKey: dashboardKeys.sales, queryFn: dashboardApi.sales, enabled, staleTime: DASHBOARD_STALE_MS });

export const useInventoryDashboard = (enabled: boolean) =>
  useQuery({ queryKey: dashboardKeys.inventory, queryFn: dashboardApi.inventory, enabled, staleTime: DASHBOARD_STALE_MS });

export const useFlaggedSync = (enabled: boolean) =>
  useQuery({ queryKey: dashboardKeys.flaggedSync, queryFn: dashboardApi.flaggedSync, enabled, staleTime: DASHBOARD_STALE_MS });

/** Every dashboard figure, refetched right away — a sale completing is the other event (besides a stock
 * change, already wired in features/inventory/queries.ts) that makes today's numbers stale immediately. */
export function useRefreshDashboards() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: ['dashboard'] });
}
