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
  /** Null for an item chosen through a category-linked group; that one carries `itemId` instead. */
  itemModifierId: string | null;
  itemId?: string | null;
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
  /** Manager/Admin manual discount on this line (0 when none). */
  manualDiscountAmount?: number;
  manualDiscountKind?: ManualDiscountKind | null;
  manualDiscountValue?: number | null;
  /** The catalog price this line was first overridden from; null when never overridden. */
  priceOverrideFrom?: number | null;
  isOpenItem?: boolean;
  openItemName?: string | null;
  note?: string | null;
}

export interface Payment {
  id: string;
  method: PaymentMethod;
  status: PaymentStatus;
  amount: number;
  amountTendered: number | null;
  changeGiven: number | null;
  /** Order of this tender within a split payment. */
  sequence?: number;
  reference?: string | null;
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
  /** The 12% VAT taken off a Senior/PWD sale (RA 9994, RA 10754); 0 for any other sale. */
  vatExemptAmount: number;
  promoCode: string | null;
  promoDiscountAmount: number;
  itemPromoDiscountAmount: number;
  totalAmount: number;
  receiptNumber: number | null;
  orderType: string | null;
  originatedFromKiosk: boolean;
  kioskPrepNumber: number | null;
  /** Kiosk orders only: how the customer said they will pay ("cash", "card", "ewallet", "discount"). */
  kioskPaymentPreference?: string | null;
  /** With the "discount" choice: which one they will ask for ("senior", "pwd", "other"). */
  kioskDiscountHint?: string | null;
  kitchenStatus: KitchenStatus;
  payments: Payment[];
  /** When the cart was started. */
  createdAt: string;
  /** When it was paid, which is the moment the receipt shows. Null while the sale is still open. */
  completedAt: string | null;
  /** Lines that changed or were dropped on resume of a parked cart; absent otherwise. */
  changedLines?: ChangedLine[] | null;
  manualDiscountAmount?: number;
  manualDiscountReason?: string | null;
  manualDiscountKind?: ManualDiscountKind | null;
  manualDiscountValue?: number | null;
}

/** Mirrors Purch.Application.Pos.ChangedLineDto. */
export interface ChangedLine {
  lineId: string;
  reason: 'PriceChanged' | 'Unavailable' | 'NotRepriced';
}

export type ManualDiscountKind = 'Percent' | 'Fixed';

/** Mirrors Purch.Application.Pos.ParkedCartSummaryDto. */
export interface ParkedCartSummary {
  id: string;
  label: string | null;
  parkedAt: string;
  parkedByName: string | null;
  lineCount: number;
  totalAmount: number;
}

/** Mirrors SetManualDiscountRequest. A value of 0 clears the discount. */
export interface ManualDiscountRequest {
  kind: ManualDiscountKind;
  value: number;
  reason: string;
  /** A Manager/Admin PIN. Required on every set, remove and override, whoever is signed in. */
  approverPin?: string;
}

/** Mirrors OverrideLinePriceRequest. */
export interface OverridePriceRequest {
  newUnitPrice: number;
  reason: string;
  /** A Manager/Admin PIN, always required. */
  approverPin?: string;
}

/** Mirrors AddOpenItemRequest. approverPin only on a claimed kiosk order. */
export interface OpenItemRequest {
  name: string;
  unitPrice: number;
  quantity: number;
  note?: string | null;
  approverPin?: string;
}

/** Mirrors PaymentPartRequest. Only a Cash part may be tendered above its amount. */
export interface PaymentPart {
  method: PaymentMethod;
  amount: number;
  amountTendered?: number | null;
  customerCreditLedgerId?: string | null;
  reference?: string | null;
}

export interface AddLineRequest {
  itemId: string;
  itemVariantId: string | null;
  quantity: number;
  comboSelections?: { slotId: string; selectedItemId: string }[];
  selectedModifierIds?: string[];
  /** Items chosen through a category-linked modifier group. */
  selectedCategoryItemIds?: string[];
}

export interface RecordPaymentRequest {
  method: PaymentMethod;
  amountTendered: number | null;
  customerCreditLedgerId?: string | null;
  allowCreditLimitOverride?: boolean;
  creditLimitOverrideReason?: string | null;
  /** The total the customer was shown. The server refuses the sale unless this equals its own total. */
  expectedTotal?: number;
  /** Split tender: one entry per tender, summing exactly to the total. When present the request-level
   * method/amountTendered/customerCreditLedgerId are ignored; omit for a single full payment. */
  parts?: PaymentPart[];
}

/** Refunding a completed sale always needs an Admin/Manager's approval — see ApproverPinDialog. */
export interface RefundTransactionRequest {
  reason: string;
  approverPin?: string;
}

/** Mirrors Purch.Application.Pos.CreateExchangeRequest. Always needs a manager/admin PIN. */
export interface CreateExchangeRequest {
  returnLines: { originalLineId: string; quantity: number }[];
  replacementLines: { itemId: string; itemVariantId: string | null; quantity: number }[];
  reason: string;
  approverPin: string;
  /** Required only when the two totals differ. Cash, BankTransfer or ManualGcashQr. */
  settlementMethod?: PaymentMethod;
  settlementAmountTendered?: number;
}

export interface AdjustmentLine {
  itemId: string;
  itemName: string;
  itemVariantId: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

/** Mirrors Purch.Application.Pos.AdjustmentDto. */
export interface Adjustment {
  id: string;
  originalTransactionId: string;
  originalReceiptNumber: number | null;
  createdAt: string;
  reason: string;
  approvedByUserId: string;
  approvedByName: string;
  returnLines: AdjustmentLine[];
  replacementLines: AdjustmentLine[];
  returnedTotal: number;
  replacementTotal: number;
  /** Replacement minus returned: positive means the customer owed more, negative means they were refunded. */
  priceDifference: number;
  settlementMethod: PaymentMethod | null;
  changeGiven: number | null;
}
