import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query';
import { reportsApi } from './api';
import type { CogsGroupBy, RangeParams } from './types';

export const reportKeys = {
  staff: (p: RangeParams) => ['reports', 'staff', p] as const,
  departments: (p: RangeParams) => ['reports', 'departments', p] as const,
  movements: (p: RangeParams) => ['reports', 'movements', p] as const,
  approvalsReview: (date: string | undefined) => ['reports', 'approvalsReview', date ?? 'today'] as const,
};

export const useStaffPerformance = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: reportKeys.staff(params), queryFn: () => reportsApi.staffPerformance(params), enabled });

export const useDepartmentSales = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: reportKeys.departments(params), queryFn: () => reportsApi.departmentSales(params), enabled });

export const useMovementSummary = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: reportKeys.movements(params), queryFn: () => reportsApi.movementSummary(params), enabled });

/** One request per tab: figures stay fresh for a minute, switching sub-views or revisiting a tab costs nothing, and a
 * range change keeps the previous numbers on screen until the new ones land. */
const AGGREGATE = { staleTime: 60_000, refetchOnWindowFocus: false, placeholderData: keepPreviousData } as const;

export const useSalesMix = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: ['reports', 'salesMix', params], queryFn: () => reportsApi.salesMix(params), enabled, ...AGGREGATE });

export const useTaxReport = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: ['reports', 'tax', params], queryFn: () => reportsApi.tax(params), enabled, ...AGGREGATE });

export const useProfitReport = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: ['reports', 'profit', params], queryFn: () => reportsApi.grossProfit(params), enabled, ...AGGREGATE });

export const useDiscountReport = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: ['reports', 'discounts', params], queryFn: () => reportsApi.discounts(params), enabled, ...AGGREGATE });

export const useManualDiscounts = (params: RangeParams, limit: number, enabled = true) =>
  useQuery({
    queryKey: ['reports', 'manualDiscounts', params, limit],
    queryFn: () => reportsApi.manualDiscounts({ ...params, limit }),
    enabled,
    ...AGGREGATE,
  });

export const useVoidReport = (params: RangeParams, limit: number, enabled = true) =>
  useQuery({ queryKey: ['reports', 'voids', params, limit], queryFn: () => reportsApi.voids({ ...params, limit }), enabled, ...AGGREGATE });

export const useRefundReport = (params: RangeParams, limit: number, enabled = true) =>
  useQuery({ queryKey: ['reports', 'refunds', params, limit], queryFn: () => reportsApi.refunds({ ...params, limit }), enabled, ...AGGREGATE });

/** Fetched only once the executive cards scroll into view, so the dashboard's first paint never waits on it. */
export const useDashboardInsights = (params: RangeParams | null, enabled: boolean) =>
  useQuery({
    queryKey: ['reports', 'insights', params],
    queryFn: () => reportsApi.insights(params as RangeParams),
    enabled: enabled && params !== null,
    ...AGGREGATE,
  });

/** Readings are generated on demand, not cached: a Z-reading advances the day's counters on the server. */
export const useXReading = () => useMutation({ mutationFn: () => reportsApi.xReading() });
export const useZReading = () => useMutation({ mutationFn: () => reportsApi.zReading() });

/** date is an ISO yyyy-MM-dd string, or undefined for today. */
export const useApprovalsReview = (date: string | undefined, enabled = true) =>
  useQuery({ queryKey: reportKeys.approvalsReview(date), queryFn: () => reportsApi.approvalsReview(date), enabled });

export const useInventoryValuation = (type: string) =>
  useQuery({ queryKey: ['reports', 'valuation', type], queryFn: () => reportsApi.inventoryValuation({ type }), ...AGGREGATE });

export const useInventoryCogs = (params: RangeParams, groupBy: CogsGroupBy) =>
  useQuery({ queryKey: ['reports', 'cogs', params, groupBy], queryFn: () => reportsApi.inventoryCogs({ ...params, groupBy }), ...AGGREGATE });
