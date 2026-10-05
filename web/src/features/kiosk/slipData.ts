import type { Transaction } from '../pos/types';
import { lineDetails, paymentSummary } from './tickets';

/** Everything printed on a kiosk order slip. It is a hand-off ticket, not an official receipt: the cashier issues that. */
export interface SlipData {
  businessName: string;
  orderNumber: string;
  orderType: string | null;
  /** "Cash", "With Discounts: Senior Citizen"... empty when the customer chose nothing. */
  payment: string;
  discounted: boolean;
  lines: { quantity: number; name: string; details: string[] }[];
  total: number;
  printedAt: Date;
  /** A sample printed to prove the printer works. */
  test?: boolean;
}

export function slipFromOrder(order: Transaction, businessName: string, fallback: { orderType: string | null; payment: string | null; discountHint: string | null }): SlipData {
  const payment = order.kioskPaymentPreference ?? fallback.payment;
  return {
    businessName,
    orderNumber: order.kioskPrepNumber === null ? '-' : String(order.kioskPrepNumber),
    orderType: order.orderType ?? fallback.orderType,
    payment: paymentSummary(payment, order.kioskDiscountHint ?? fallback.discountHint),
    discounted: payment === 'discount',
    lines: order.lines.map((line) => ({ quantity: line.quantity, name: line.itemName, details: lineDetails(line) })),
    total: order.totalAmount,
    printedAt: new Date(),
  };
}

export function testSlip(businessName: string): SlipData {
  return {
    businessName: businessName || 'Your business',
    orderNumber: '000',
    orderType: 'Dine In',
    payment: 'Cash',
    discounted: false,
    lines: [
      { quantity: 2, name: 'Sample item', details: ['Sample option'] },
      { quantity: 1, name: 'Another sample item', details: [] },
    ],
    total: 0,
    printedAt: new Date(),
    test: true,
  };
}
