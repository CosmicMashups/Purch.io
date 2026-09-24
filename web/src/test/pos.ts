import type { Item } from '../features/catalog/types';
import type { Transaction, TransactionLine } from '../features/pos/types';

export function makeItem(over: Partial<Item> & { id: string; name: string }): Item {
  return {
    sku: null,
    barcode: null,
    categoryId: null,
    basePrice: 100,
    imageUrl: null,
    pricingType: 0,
    stockOnHand: 10,
    isActive: true,
    departmentId: null,
    tingiMode: 0,
    packagedSize: null,
    tingiIncrementStep: null,
    tingiAllowedSizes: [],
    serviceDurationMinutes: null,
    lowStockThreshold: null,
    isOutOfStock: false,
    ...over,
  } as Item;
}

export function makeLine(over: Partial<TransactionLine> & { id: string; itemName: string }): TransactionLine {
  return {
    itemId: 'i',
    itemVariantId: null,
    itemVariantAttributes: {},
    quantity: 1,
    unitPrice: 100,
    lineTotal: 100,
    promoDiscountAmount: 0,
    appliedPromoLabel: null,
    comboSelections: [],
    modifierSelections: [],
    ...over,
  };
}

/** A server cart as the API would return it. The figures are whatever the test says the server computed. */
export function makeCart(over: Partial<Transaction> = {}): Transaction {
  return {
    id: 'cart1',
    branchId: 'kat',
    deviceId: 'dev1',
    status: 0,
    lines: [],
    subtotal: 0,
    discountAmount: 0,
    seniorPwdDiscountApplied: false,
    promoCode: null,
    promoDiscountAmount: 0,
    itemPromoDiscountAmount: 0,
    totalAmount: 0,
    receiptNumber: null,
    orderType: null,
    originatedFromKiosk: false,
    kioskPrepNumber: null,
    kitchenStatus: 0,
    payments: [],
    ...over,
  };
}
