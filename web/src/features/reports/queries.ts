import { useMutation, useQuery } from '@tanstack/react-query';
import { reportsApi } from './api';
import type { RangeParams } from './types';

export const reportKeys = {
  staff: (p: RangeParams) => ['reports', 'staff', p] as const,
  departments: (p: RangeParams) => ['reports', 'departments', p] as const,
  movements: (p: RangeParams) => ['reports', 'movements', p] as const,
};

export const useStaffPerformance = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: reportKeys.staff(params), queryFn: () => reportsApi.staffPerformance(params), enabled });

export const useDepartmentSales = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: reportKeys.departments(params), queryFn: () => reportsApi.departmentSales(params), enabled });

export const useMovementSummary = (params: RangeParams, enabled = true) =>
  useQuery({ queryKey: reportKeys.movements(params), queryFn: () => reportsApi.movementSummary(params), enabled });

/** Readings are generated on demand, not cached: a Z-reading advances the day's counters on the server. */
export const useXReading = () => useMutation({ mutationFn: () => reportsApi.xReading() });
export const useZReading = () => useMutation({ mutationFn: () => reportsApi.zReading() });
