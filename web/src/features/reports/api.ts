import { apiClient } from '../../lib/apiClient';
import type {
  DashboardInsights,
  DiscountReport,
  ManualDiscountRow,
  PageOf,
  ProfitReport,
  RefundReport,
  SalesMix,
  TaxReport,
  VoidReport,
} from './revenueTypes';
import type { ApprovalsReview, BirReading, DepartmentSales, MovementSummary, RangeParams, StaffPerformanceReport } from './types';

type PagedParams = RangeParams & { limit?: number };

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
  salesMix: (params: RangeParams) => apiClient.get<SalesMix>('/reports/sales-mix', { params }).then((r) => r.data),
  tax: (params: RangeParams) => apiClient.get<TaxReport>('/reports/tax', { params }).then((r) => r.data),
  grossProfit: (params: RangeParams) => apiClient.get<ProfitReport>('/reports/gross-profit', { params }).then((r) => r.data),
  discounts: (params: RangeParams) => apiClient.get<DiscountReport>('/reports/discounts', { params }).then((r) => r.data),
  manualDiscounts: (params: PagedParams) =>
    apiClient.get<PageOf<ManualDiscountRow>>('/reports/discounts/manual', { params }).then((r) => r.data),
  voids: (params: PagedParams) => apiClient.get<VoidReport>('/reports/voids', { params }).then((r) => r.data),
  refunds: (params: PagedParams) => apiClient.get<RefundReport>('/reports/refunds', { params }).then((r) => r.data),
  insights: (params: RangeParams) => apiClient.get<DashboardInsights>('/reports/dashboard-insights', { params }).then((r) => r.data),
  voidsCsv: (params: RangeParams) => apiClient.get<string>('/reports/voids.csv', { params, responseType: 'text' }).then((r) => r.data),
  refundsCsv: (params: RangeParams) => apiClient.get<string>('/reports/refunds.csv', { params, responseType: 'text' }).then((r) => r.data),
  manualDiscountsCsv: (params: RangeParams) =>
    apiClient.get<string>('/reports/discounts/manual.csv', { params, responseType: 'text' }).then((r) => r.data),
};
