/** Mirrors Purch.Application.Onboarding.BranchDto. Fields beyond the first three are absent in older responses. */
export interface Branch {
  id: string;
  name: string;
  address: string | null;
  receiptPrinterProfile?: number;
  cashDrawerEnabled?: boolean;
  cashDrawerPolicy?: number;
  manualGcashQrImageUrl?: string | null;
  manualGcashAccountName?: string | null;
  manualGcashAccountNumber?: string | null;
}

/** Mirrors DepartmentDto. */
export interface BranchDepartment {
  id: string;
  branchId: string;
  name: string;
  concessionaireContactInfo: string | null;
}
