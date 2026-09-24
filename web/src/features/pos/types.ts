/** Mirrors Purch.Domain.Enums.TransactionStatus. */
export const TransactionStatus = { Open: 0, AwaitingPayment: 1, Completed: 2, Voided: 3, Refunded: 4 } as const;
export type TransactionStatus = (typeof TransactionStatus)[keyof typeof TransactionStatus];

/** Mirrors Purch.Domain.Enums.PaymentMethod. Only Cash, BankTransfer, ManualGcashQr and UtangCredit are accepted by the API today. */
export const PaymentMethod = {
  Cash: 0,
  QrPh: 1,
  BankTransfer: 2,
  ManualGcashQr: 3,
  BillPaymentELoad: 4,
  UtangCredit: 5,
  Split: 6,
} as const;
export type PaymentMethod = (typeof PaymentMethod)[keyof typeof PaymentMethod];

/** Mirrors Purch.Domain.Enums.PaymentStatus. */
export const PaymentStatus = { Pending: 0, Confirmed: 1, Failed: 2, Cancelled: 3 } as const;
export type PaymentStatus = (typeof PaymentStatus)[keyof typeof PaymentStatus];

/** Mirrors Purch.Domain.Enums.KitchenStatus. */
export const KitchenStatus = { Queued: 0, Preparing: 1, Ready: 2, PickedUp: 3 } as const;
export type KitchenStatus = (typeof KitchenStatus)[keyof typeof KitchenStatus];

/** Mirrors Purch.Application.Pos.TransactionDto. Every money figure is computed by the server. */
export interface ComboSelection {
  slotId: string;
  slotLabel: string;
  selectedItemId: string;
  selectedItemName: string;
}

export interface ModifierSelection {
  itemModifierId: string;
  modifierName: string;
  modifierGroupName: string;
  priceDelta: number;
}

export interface TransactionLine {
  id: string;
  itemId: string;
  itemName: string;
  itemVariantId: string | null;
  itemVariantAttributes: Record<string, string>;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  promoDiscountAmount: number;
  appliedPromoLabel: string | null;
  comboSelections: ComboSelection[];
  modifierSelections: ModifierSelection[];
}

export interface Payment {
  id: string;
  method: PaymentMethod;
  status: PaymentStatus;
  amount: number;
  amountTendered: number | null;
  changeGiven: number | null;
}

export interface Transaction {
  id: string;
  branchId: string;
  deviceId: string;
  status: TransactionStatus;
  lines: TransactionLine[];
  subtotal: number;
  discountAmount: number;
  seniorPwdDiscountApplied: boolean;
  promoCode: string | null;
  promoDiscountAmount: number;
  itemPromoDiscountAmount: number;
  totalAmount: number;
  receiptNumber: number | null;
  orderType: string | null;
  originatedFromKiosk: boolean;
  kioskPrepNumber: number | null;
  kitchenStatus: KitchenStatus;
  payments: Payment[];
}

export interface AddLineRequest {
  itemId: string;
  itemVariantId: string | null;
  quantity: number;
  comboSelections?: { slotId: string; selectedItemId: string }[];
  selectedModifierIds?: string[];
}

export interface RecordPaymentRequest {
  method: PaymentMethod;
  amountTendered: number | null;
  customerCreditLedgerId?: string | null;
  allowCreditLimitOverride?: boolean;
  creditLimitOverrideReason?: string | null;
}
