/** Mirrors Purch.Application.Reporting.StaffPerformanceReportDto. */
export interface StaffSalesSummary {
  staffUserId: string;
  staffName: string;
  transactionCount: number;
  totalSales: number;
}

export interface StaffShiftAttendance {
  staffUserId: string;
  staffName: string;
  shiftsOpened: number;
  shiftsWithVariance: number;
}

export interface StaffPerformanceReport {
  sales: StaffSalesSummary[];
  shiftAttendance: StaffShiftAttendance[];
}

/** Mirrors DepartmentSalesSummaryDto. A null departmentId is the "General" row for items with no department. */
export interface DepartmentSales {
  departmentId: string | null;
  departmentName: string;
  revenue: number;
}

/** Mirrors MovementSummaryDto. `type` is the MovementType integer. */
export interface MovementTypeSummary {
  type: number;
  totalQuantity: number;
  movementCount: number;
}

export interface MovementSummary {
  from: string;
  to: string;
  byType: MovementTypeSummary[];
}

/** Mirrors Purch.Domain.Enums.BirReadingType. */
export const BirReadingType = { X: 0, Z: 1 } as const;
export type BirReadingType = (typeof BirReadingType)[keyof typeof BirReadingType];

/** Mirrors BirReadingDto. Every figure is computed by the server. */
export interface BirReading {
  type: BirReadingType;
  deviceId: string;
  machineIdentificationNumber: string;
  generatedAt: string;
  beginningReceiptNumber: number | null;
  endingReceiptNumber: number | null;
  transactionCount: number;
  grossSales: number;
  vatableSales: number;
  vatAmount: number;
  /** Senior/PWD sales at their VAT-exclusive price (before the 20% discount); they carry no VAT. */
  vatExemptSales: number;
  /** The VAT taken off those sales, already inside grossSales. */
  vatExemptionTotal: number;
  seniorPwdDiscountTotal: number;
  promoDiscountTotal: number;
  totalDiscounts: number;
  /** Exchanges processed on this device since the last reading (replacement minus returned), already
   * folded into grossSales/netSales below — broken out so the reading stays auditable. */
  exchangeAdjustmentsTotal: number;
  /** Sales originally rung up on this device that were refunded since the last reading, whether or not
   * the sale itself was reported on an earlier one. Already subtracted from grossSales/netSales below. */
  refundsTotal: number;
  netSales: number;
  voidedCount: number;
  voidedAmount: number;
  oldGrandAccumulatedSales: number;
  newGrandAccumulatedSales: number;
  resetCounter: number;
  lateReceiptNumbers: number[];
  missingReceiptNumbers: number[];
}

export interface RangeParams {
  from: string;
  to: string;
  branchId?: string;
}

/** Mirrors Purch.Domain.Enums.AuditActionType — only the values this report ever shows. */
export const AuditActionType = { Void: 0, Refund: 1 } as const;
export type AuditActionType = (typeof AuditActionType)[keyof typeof AuditActionType];

/** Mirrors Purch.Application.Approvals.ApprovalEntryDto. */
export interface ApprovalEntry {
  requesterId: string;
  requesterName: string;
  actionType: AuditActionType;
  targetEntityId: string;
  createdAt: string;
  afterHours: boolean;
}

/** Mirrors Purch.Application.Approvals.ApprovalFlagDto. */
export interface ApprovalFlag {
  message: string;
}

/** Mirrors Purch.Application.Approvals.ApproverSummaryDto. */
export interface ApproverSummary {
  approverId: string;
  approverName: string;
  approverRole: number;
  totalApprovals: number;
  afterHoursApprovals: number;
  flags: ApprovalFlag[];
  entries: ApprovalEntry[];
}

/** Mirrors Purch.Application.Approvals.ApprovalsReviewDto. */
export interface ApprovalsReview {
  date: string;
  totalApprovals: number;
  approvers: ApproverSummary[];
}
