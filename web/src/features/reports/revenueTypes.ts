/** Mirrors Purch.Domain.Enums.PaymentMethod. */
export const PAYMENT_METHOD_LABELS: Record<number, string> = {
  0: 'Cash',
  1: 'QR Ph',
  2: 'Bank transfer',
  3: 'GCash QR',
  4: 'Bill payment / e-load',
  5: 'Utang (credit)',
  6: 'Split',
};

/** Mirrors Purch.Application.Reporting.SalesMixDto. Gross: later refunds do not reduce it. */
export interface SalesMix {
  grossSales: number;
  transactionCount: number;
  operatingDays: number;
  payments: { method: number; amount: number; paymentCount: number; isCredit: boolean }[];
  exchangeSettlements: number;
  categories: { categoryId: string | null; name: string; isOpenItem: boolean; revenue: number; quantity: number }[];
  /** Hour 0-23 in business time (UTC+8). */
  hours: { hour: number; sales: number; transactionCount: number; averageBasket: number; averagePerOperatingDay: number }[];
  /** dayOfWeek 0 = Sunday. */
  heatmap: { dayOfWeek: number; hour: number; sales: number; transactionCount: number }[];
}

/** Mirrors TaxDayDto. Stamped per sale, so a range total can differ from the X/Z readings by centavos. */
export interface TaxDay {
  date: string;
  saleCount: number;
  grossSales: number;
  vatableSales: number;
  vatAmount: number;
  vatExemptSales: number;
  zeroRatedSales: number;
}

export interface TaxReport {
  totals: TaxDay;
  days: TaxDay[];
  vatDerivedSaleCount: number;
  seniorPwdSaleCount: number;
  seniorPwdExemptAmount: number;
}

export interface ProfitDay {
  date: string;
  knownCostRevenue: number;
  cost: number;
  grossProfit: number;
  marginPercent: number | null;
  coveragePercent: number | null;
}

export interface ProfitGroup {
  id: string | null;
  name: string;
  quantity: number;
  knownCostRevenue: number;
  cost: number;
  grossProfit: number;
  marginPercent: number | null;
  coveragePercent: number | null;
}

/** Mirrors ProfitReportDto. Lines without a known sale-time cost are excluded, never counted as zero cost. */
export interface ProfitReport {
  revenueExVat: number;
  knownCostRevenue: number;
  unknownCostRevenue: number;
  cost: number;
  grossProfit: number;
  marginPercent: number | null;
  coveragePercent: number | null;
  days: ProfitDay[];
  categories: ProfitGroup[];
  topItems: ProfitGroup[];
  bottomItems: ProfitGroup[];
}

export interface PageOf<T> {
  rows: T[];
  total: number;
  offset: number;
  limit: number;
}

export interface DiscountReport {
  grossSales: number;
  promoCodeDiscount: number;
  itemPromoDiscount: number;
  manualDiscount: number;
  seniorPwdDiscount: number;
  totalDiscounts: number;
  discountRatePercent: number;
  byCashier: { staffUserId: string | null; name: string; saleCount: number; sales: number; discounts: number; ratePercent: number }[];
  approvers: { userId: string; name: string; discountApprovals: number; priceOverrideApprovals: number }[];
}

export interface ManualDiscountRow {
  transactionId: string;
  receiptNumber: number | null;
  completedAt: string;
  branchName: string;
  staffName: string;
  total: number;
  cartDiscount: number;
  lineDiscounts: number;
  lineDiscountCount: number;
  priceOverrideCount: number;
  reason: string | null;
  approverName: string | null;
}

export interface VoidRow {
  transactionId: string;
  voidedAt: string;
  branchName: string;
  staffName: string;
  total: number;
  lineCount: number;
  parkedLabel: string | null;
  reason: string | null;
  approverName: string | null;
}

/** Zero-value discards and expired parked carts are counted apart, so this can differ from the X/Z voided count. */
export interface VoidReport {
  count: number;
  amount: number;
  emptyDiscardedCount: number;
  expiredParkedCount: number;
  voids: PageOf<VoidRow>;
}

export interface RefundRow {
  transactionId: string;
  receiptNumber: number | null;
  completedAt: string;
  refundedAt: string;
  branchName: string;
  staffName: string;
  total: number;
  reason: string | null;
  approverName: string | null;
}

export interface ExchangeRow {
  adjustmentId: string;
  originalReceiptNumber: number | null;
  createdAt: string;
  branchName: string;
  returnedTotal: number;
  replacementTotal: number;
  priceDifference: number;
  reason: string;
  approverName: string;
}

export interface RefundReport {
  refundCount: number;
  refundAmount: number;
  exchangePriceDifference: number;
  refunds: PageOf<RefundRow>;
  exchanges: PageOf<ExchangeRow>;
}

export interface DashboardInsights {
  marginTrend: ProfitDay[];
  marginPercent: number | null;
  coveragePercent: number | null;
  leakage: { date: string; grossSales: number; discounts: number; voids: number; refunds: number; leakagePercent: number }[];
  leakagePercent: number;
  categories: { name: string; isOpenItem: boolean; revenue: number; sharePercent: number; marginPercent: number | null }[];
}
