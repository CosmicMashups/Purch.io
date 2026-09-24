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
  seniorPwdDiscountTotal: number;
  promoDiscountTotal: number;
  totalDiscounts: number;
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
