import { z } from 'zod';
import { formatPeso } from '../dashboard/format';
import { PaymentMethod, type ManualDiscountKind, type PaymentPart, type Transaction, type TransactionLine } from './types';

/** Client copies of the server's rules for manual discounts, price overrides, open items and notes. The server still decides. */

const MAX_PRICE = 999_999.99;
const decimals = (n: number, places: number) => Math.abs(Math.round(n * 10 ** places) - n * 10 ** places) < 1e-6;
const money = (field: string) =>
  z
    .number({ invalid_type_error: `Enter ${field}` })
    .finite(`Enter ${field}`)
    .gt(0, `${field[0].toUpperCase()}${field.slice(1)} must be more than 0`)
    .refine((n) => decimals(n, 2), 'Use at most 2 decimal places');

export const reasonSchema = z.string().trim().min(1, 'Give a reason').max(200, 'Keep the reason to 200 characters');

/** `total` is what the discount applies to: the line total for a line, the cart subtotal for the cart. */
export const discountSchema = (total: number) =>
  z
    .object({ kind: z.enum(['Percent', 'Fixed']), value: money('a value'), reason: reasonSchema })
    .superRefine((v, ctx) => {
      if (v.kind === 'Percent' && v.value > 100) ctx.addIssue({ code: 'custom', path: ['value'], message: 'A percent discount can be at most 100%' });
      if (v.kind === 'Fixed' && v.value > total + 1e-9) ctx.addIssue({ code: 'custom', path: ['value'], message: `Can't be more than ${formatPeso(total)}` });
    });

export const priceOverrideSchema = z.object({
  newUnitPrice: money('a price').refine((n) => n <= MAX_PRICE, `At most ${formatPeso(MAX_PRICE)}`),
  reason: reasonSchema,
});

export const lineNoteSchema = z.string().max(200, 'Keep the note to 200 characters');

export const openItemSchema = z.object({
  name: z.string().trim().min(1, 'Name the item').max(80, 'Keep the name to 80 characters'),
  unitPrice: money('a price').refine((n) => n <= MAX_PRICE, `At most ${formatPeso(MAX_PRICE)}`),
  quantity: z
    .number({ invalid_type_error: 'Enter a quantity' })
    .finite('Enter a quantity')
    .gt(0, 'Quantity must be more than 0')
    .max(99_999, 'At most 99,999')
    .refine((n) => decimals(n, 3), 'Use at most 3 decimal places'),
  note: lineNoteSchema.optional(),
});

/** The first error message for each field, for showing under the inputs. */
export function fieldErrors(error: z.ZodError | undefined): Record<string, string> {
  const out: Record<string, string> = {};
  for (const issue of error?.issues ?? []) {
    const key = String(issue.path[0] ?? '');
    if (!(key in out)) out[key] = issue.message;
  }
  return out;
}

/** What the discount would take off, before the server confirms it. */
export function discountPreview(kind: ManualDiscountKind, value: number, total: number): number {
  if (!Number.isFinite(value) || value <= 0) return 0;
  // In cents, rounded half away from zero as the server does; the epsilon absorbs float noise (2.01 * 50 = 100.4999...).
  const cents = kind === 'Percent' ? total * Math.min(value, 100) : Math.min(value, total) * 100;
  return (Math.sign(cents) * Math.round(Math.abs(cents) + 1e-6)) / 100;
}

/** "-10% (-₱20.00)" or "-₱15.00"; null when the line has no manual discount. */
export function discountMarker(line: Pick<TransactionLine, 'manualDiscountAmount' | 'manualDiscountKind' | 'manualDiscountValue'>): string | null {
  const amount = line.manualDiscountAmount ?? 0;
  if (amount <= 0) return null;
  if (line.manualDiscountKind === 'Percent' && line.manualDiscountValue) return `-${line.manualDiscountValue}% (-${formatPeso(amount)})`;
  return `-${formatPeso(amount)}`;
}

/**
 * What a manual discount applies to, matching the server: a line's lineTotal (gross, before its own manual
 * discount), or for the cart the subtotal after line manual discounts.
 */
export function discountBase(cart: Transaction, line: TransactionLine | null): number {
  if (line) return line.lineTotal;
  const lineDiscounts = cart.lines.reduce((sum, l) => sum + (l.manualDiscountAmount ?? 0), 0);
  return Math.max(0, Math.round((cart.subtotal - lineDiscounts) * 100) / 100);
}

export function hasManualDiscount(cart: Transaction): boolean {
  return (cart.manualDiscountAmount ?? 0) > 0 || cart.lines.some((l) => (l.manualDiscountAmount ?? 0) > 0);
}

/** Text typed into a number field: "" and "12." read as NaN / 12 the way a till expects. */
export const parseAmount = (text: string): number => (text.trim() === '' ? NaN : Number(text));

export const REASON_CHIPS = ['Loyal customer', 'Damaged item', 'Price match', 'Staff meal'] as const;

export const SENIOR_EXCLUSIVE = 'Senior/PWD discount is applied - remove it to give a manual discount';

/* Split tender: client copies of SplitTenderService's rules. The server still decides. */

export const MAX_SPLIT_PARTS = 10;
export const MAX_REFERENCE_LENGTH = 100;
export const SPLIT_METHODS: readonly PaymentMethod[] = [PaymentMethod.Cash, PaymentMethod.BankTransfer, PaymentMethod.ManualGcashQr, PaymentMethod.UtangCredit];

const cents = (n: number) => Math.round(n * 100);
const isCentAmount = (n: number) => Number.isFinite(n) && decimals(n, 2);

/** What is still to collect, never negative: the total less every part's amount, worked in whole centavos. */
export function remaining(total: number, parts: Pick<PaymentPart, 'amount'>[]): number {
  const left = cents(total) - parts.reduce((sum, p) => sum + cents(p.amount), 0);
  return Math.max(0, left) / 100;
}

/** Change handed back across the cash parts. */
export function splitChange(parts: PaymentPart[]): number {
  return parts.reduce((sum, p) => (p.method === PaymentMethod.Cash && p.amountTendered != null ? sum + cents(p.amountTendered) - cents(p.amount) : sum), 0) / 100;
}

/** The first rule the parts break, or null when the server should accept them. Order follows the server's. */
export function validateParts(total: number, parts: PaymentPart[]): string | null {
  if (parts.length === 0) return 'Add at least one payment.';
  if (parts.length > MAX_SPLIT_PARTS) return `A sale can be split into at most ${MAX_SPLIT_PARTS} payments.`;
  for (const p of parts) {
    if (!SPLIT_METHODS.includes(p.method)) return "That method isn't available for checkout yet.";
    if (!(p.amount > 0) || !isCentAmount(p.amount)) return 'Each payment amount must be more than zero, in pesos and centavos.';
    if (p.method === PaymentMethod.Cash) {
      const tendered = p.amountTendered ?? p.amount;
      if (!isCentAmount(tendered) || cents(tendered) < cents(p.amount)) return 'Cash received must cover its part of the payment.';
    } else if (p.amountTendered != null && cents(p.amountTendered) !== cents(p.amount)) {
      return 'Only cash can be more than its amount; enter the exact amount for the other payments.';
    }
    if (p.method === PaymentMethod.UtangCredit && !p.customerCreditLedgerId) return 'Choose a customer account for the Utang payment.';
    if ((p.reference?.trim().length ?? 0) > MAX_REFERENCE_LENGTH) return `A payment reference can be at most ${MAX_REFERENCE_LENGTH} characters.`;
  }
  if (parts.filter((p) => p.method === PaymentMethod.UtangCredit).length > 1) return 'Only one Utang payment is allowed per sale.';
  if (parts.reduce((s, p) => s + cents(p.amount), 0) !== cents(total)) return `The payments must add up to ${formatPeso(total)}.`;
  return null;
}
