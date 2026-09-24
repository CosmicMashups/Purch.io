/** Mirrors Purch.Application.CreditLedger.CustomerCreditLedgerDto. */
export interface CreditLedger {
  id: string;
  customerFullName: string;
  customerPhoneNumber: string;
  customerAddress: string | null;
  balance: number;
  creditLimit: number;
  dueDate: string | null;
  isActive: boolean;
}

/** Mirrors CreditReminderDto. */
export interface CreditReminder {
  id: string;
  customerFullName: string;
  customerPhoneNumber: string;
  balance: number;
  dueDate: string;
  isOverdue: boolean;
}
