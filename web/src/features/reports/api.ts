import { apiClient } from '../../lib/apiClient';
import type { ApprovalsReview, BirReading, DepartmentSales, MovementSummary, RangeParams, StaffPerformanceReport } from './types';

export const reportsApi = {
  staffPerformance: (params: RangeParams) => apiClient.get<StaffPerformanceReport>('/reports/staff-performance', { params }).then((r) => r.data),
  departmentSales: (params: RangeParams) => apiClient.get<DepartmentSales[]>('/reports/department-sales', { params }).then((r) => r.data),
  movementSummary: (params: RangeParams) => apiClient.get<MovementSummary>('/reports/inventory/movement-summary', { params }).then((r) => r.data),
  xReading: () => apiClient.post<BirReading>('/reports/x-reading').then((r) => r.data),
  zReading: () => apiClient.post<BirReading>('/reports/z-reading').then((r) => r.data),
  lowStockCsv: () => apiClient.get<string>('/reports/inventory/low-stock-export.csv', { responseType: 'text' }).then((r) => r.data),
  transactionsCsv: (params: RangeParams) =>
    apiClient.get<string>('/reports/sales/transactions-export.csv', { params, responseType: 'text' }).then((r) => r.data),
  approvalsReview: (date?: string) => apiClient.get<ApprovalsReview>('/reports/approvals-review', { params: date ? { date } : undefined }).then((r) => r.data),
};
