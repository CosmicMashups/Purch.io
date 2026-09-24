/** Mirrors Purch.Domain.Enums.ShiftStatus. */
export const ShiftStatus = { Open: 0, Closed: 1 } as const;
export type ShiftStatus = (typeof ShiftStatus)[keyof typeof ShiftStatus];

/** Mirrors Purch.Application.Shifts.ShiftDto. Expected cash and variance are computed by the server. */
export interface Shift {
  id: string;
  branchId: string;
  deviceId: string;
  status: ShiftStatus;
  openedByUserId: string;
  openedByUserName: string;
  openingCashAmount: number;
  openedAt: string;
  closedByUserId: string | null;
  closedByUserName: string | null;
  closingCashAmount: number | null;
  expectedCashAmount: number | null;
  varianceAmount: number | null;
  handoverNotes: string | null;
  approvedByUserId: string | null;
  approvedByUserName: string | null;
  closedAt: string | null;
}

export interface CloseShiftRequest {
  closingCashAmount: number;
  handoverNotes: string | null;
  approverPin: string | null;
}
