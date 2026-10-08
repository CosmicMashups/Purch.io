/**
 * TypeScript port of the backend's cart pricing (`ItemPromoPricingCalculator` plus
 * `TransactionService.RecalculateTotalAsync`), the same rules the Flutter `PricingEngine` follows, so the
 * Cashier can total a cart on the device instantly instead of waiting for a server round trip per tap.
 *
 * Discounts do NOT stack (RA 9994): the statutory Senior Citizen/PWD 20% cannot be combined with a promo
 * code or any promotional discount, and only one promotion applies at a time.
 *  - Senior/PWD on: the sale is VAT-exempt (RA 9994, RA 10754), so the 12% VAT comes off the regular (pre-promo)
 *    subtotal first, then 20% of that VAT-exclusive price; every promotion is suppressed.
 *  - Otherwise: ONE promotion, the automatic item promos (BOGO/combo/item discount) or the promo code,
 *    whichever is larger (a tie goes to the item promos).
 *
 * The result is an on-screen estimate: the server re-prices the whole cart with its own copy of these rules
 * at checkout and its numbers are the ones recorded. Keep all copies in step through the shared cases in
 * `shared/pricing-scenarios.json`, which every engine's tests run.
 */

export const SENIOR_PWD_DISCOUNT_RATE = 0.2;

/** Prices include 12% VAT; a Senior/PWD sale is exempt from it. */
export const VAT_RATE = 0.12;

export type PromoDiscountType = 'percentage' | 'fixedAmount' | 'fixedPrice';
export type PromoSide = 'none' | 'itemPromos' | 'promoCode';
export type PromoCodeNotApplied = 'none' | 'suppressedBySeniorPwd' | 'supersededByItemPromos';

/** One cart line as the engine sees it. `lineId` only has to be unique within the cart and stable in add order. */
export interface PricingLineInput {
  lineId: string;
  itemId: string;
  quantity: number;
  unitPrice: number;
}

interface RuleWindow {
  isActive?: boolean;
  startsAt?: string | null;
  endsAt?: string | null;
}

export interface BogoRule extends RuleWindow {
  name: string;
  triggerItemId: string;
  triggerQuantity: number;
  freeItemId: string;
  freeQuantity: number;
}

export interface ComboRule extends RuleWindow {
  name?: string;
  itemAId: string;
  itemBId: string;
  comboPrice: number;
}

export interface ItemDiscountRule extends RuleWindow {
  itemId: string;
  discountType: PromoDiscountType;
  discountValue: number;
}

export interface PromoCodeRule {
  code: string;
  discountType: PromoDiscountType;
  discountValue: number;
  isActive?: boolean;
  expiresAt?: string | null;
}

export interface PricingRules {
  bogo?: BogoRule[];
  combo?: ComboRule[];
  itemDiscount?: ItemDiscountRule[];
  promoCodes?: PromoCodeRule[];
}

export interface LineDiscount {
  discount: number;
  label: string | null;
}

export interface PricingResult {
  /** Per-line item-promo discount, all zero unless the item promos are the promotion that applies. */
  lineDiscounts: Record<string, LineDiscount>;
  /** Sum of line totals before any discount (the receipt's "subtotal"). */
  grossSubtotal: number;
  itemPromoDiscountAmount: number;
  /** The VAT taken off a Senior/PWD sale (0 otherwise). */
  vatExemptAmount: number;
  seniorPwdDiscountAmount: number;
  promoDiscountAmount: number;
  /** Senior/PWD + promo-code discount (item promos are separate, as on the server). */
  discountAmount: number;
  totalAmount: number;
  /** The promo code that is actually discounting, null when suppressed, superseded, unknown or expired. */
  appliedPromoCode: string | null;
  /** The valid promo code still on the cart, whether or not it is discounting. */
  retainedPromoCode: string | null;
  promoCodeNotApplied: PromoCodeNotApplied;
  appliedPromoSide: PromoSide;
  /** What Senior/PWD (VAT exemption plus the 20%) would take off, so the customer can pick the better deal. */
  seniorPwdSavings: number;
  /** What the best promotion would take off, whether or not it is the one applied. */
  promoSavings: number;
}

const peso = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const formatCurrency = (amount: number) => `₱${peso.format(amount)}`;

/** Rounds to 2 decimals, halves to even: C#'s `Math.Round(decimal, 2)` default, which the server uses. */
function round2(value: number): number {
  const scaled = value * 100;
  const floor = Math.floor(scaled);
  const diff = scaled - floor;
  const rounded = Math.abs(diff - 0.5) < 1e-9 ? (floor % 2 === 0 ? floor : floor + 1) : Math.round(scaled);
  return rounded / 100;
}

function trimNumber(value: number): string {
  return Number.isInteger(value) ? value.toFixed(0) : String(value);
}

function isActive(rule: RuleWindow, now: Date): boolean {
  if (rule.isActive === false) return false;
  if (rule.startsAt && new Date(rule.startsAt) > now) return false;
  if (rule.endsAt && new Date(rule.endsAt) < now) return false;
  return true;
}

function calculateItemPromos(
  lines: PricingLineInput[],
  bogoRules: BogoRule[],
  comboRules: ComboRule[],
  itemDiscountRules: ItemDiscountRule[],
): Record<string, LineDiscount> {
  const claimed: Record<string, number> = {};
  const discount: Record<string, number> = {};
  const label: Record<string, string | null> = {};
  for (const line of lines) {
    claimed[line.lineId] = 0;
    discount[line.lineId] = 0;
    label[line.lineId] = null;
  }

  const unclaimed = (line: PricingLineInput) => line.quantity - claimed[line.lineId];

  const claimAcrossLines = (
    ordered: PricingLineInput[],
    quantityToClaim: number,
    extra?: { totalDiscount: number; labelText: string },
  ) => {
    let remainingQty = quantityToClaim;
    for (let i = 0; i < ordered.length && remainingQty > 0; i++) {
      const line = ordered[i];
      const take = Math.min(remainingQty, unclaimed(line));
      if (take <= 0) continue;
      claimed[line.lineId] += take;
      if (extra) {
        const share = quantityToClaim > 0 ? extra.totalDiscount * (take / quantityToClaim) : 0;
        discount[line.lineId] += share;
        label[line.lineId] = extra.labelText;
      }
      remainingQty -= take;
    }
  };

  // --- BOGO ---
  for (const rule of bogoRules) {
    const triggerQty = lines.filter((l) => l.itemId === rule.triggerItemId).reduce((sum, l) => sum + l.quantity, 0);

    let freeUnits: number;
    if (rule.triggerItemId === rule.freeItemId) {
      // The same units can't be both trigger and free.
      const groupSize = rule.triggerQuantity + rule.freeQuantity;
      const groups = groupSize > 0 ? Math.floor(triggerQty / groupSize) : 0;
      freeUnits = groups * rule.freeQuantity;
    } else {
      const groups = rule.triggerQuantity > 0 ? Math.floor(triggerQty / rule.triggerQuantity) : 0;
      const freeAvailable = lines.filter((l) => l.itemId === rule.freeItemId).reduce((sum, l) => sum + unclaimed(l), 0);
      freeUnits = Math.min(groups * rule.freeQuantity, freeAvailable);
    }
    if (freeUnits <= 0) continue;

    let remaining = freeUnits;
    for (const freeLine of lines.filter((l) => l.itemId === rule.freeItemId)) {
      if (remaining <= 0) break;
      const take = Math.min(remaining, unclaimed(freeLine));
      if (take <= 0) continue;
      claimed[freeLine.lineId] += take;
      discount[freeLine.lineId] += take * freeLine.unitPrice;
      label[freeLine.lineId] = rule.name.trim() === '' ? 'BUY 1 TAKE 1' : rule.name;
      remaining -= take;
    }
  }

  // --- Combo ---
  for (const rule of comboRules) {
    const aLines = lines.filter((l) => l.itemId === rule.itemAId);
    const bLines = lines.filter((l) => l.itemId === rule.itemBId);
    const aAvailable = aLines.reduce((s, l) => s + unclaimed(l), 0);
    const bAvailable = bLines.reduce((s, l) => s + unclaimed(l), 0);
    const pairs = Math.min(aAvailable, bAvailable);
    if (pairs <= 0) continue;

    const unitPriceA = aLines.length > 0 ? aLines[0].unitPrice : 0;
    const unitPriceB = bLines.length > 0 ? bLines[0].unitPrice : 0;
    const normalTotal = pairs * (unitPriceA + unitPriceB);
    const comboTotal = pairs * rule.comboPrice;
    const totalDiscount = Math.max(normalTotal - comboTotal, 0);

    if (totalDiscount <= 0) {
      // Matched but doesn't reduce the price: still claim the units.
      claimAcrossLines(aLines, pairs);
      claimAcrossLines(bLines, pairs);
      continue;
    }

    const discountA = normalTotal > 0 ? (totalDiscount * (pairs * unitPriceA)) / normalTotal : totalDiscount / 2;
    const discountB = totalDiscount - discountA;
    const labelText = `COMBO ${formatCurrency(rule.comboPrice)}`;
    claimAcrossLines(aLines, pairs, { totalDiscount: discountA, labelText });
    claimAcrossLines(bLines, pairs, { totalDiscount: discountB, labelText });
  }

  // --- Item discount ---
  for (const rule of itemDiscountRules) {
    for (const line of lines.filter((l) => l.itemId === rule.itemId)) {
      const unclaimedQty = unclaimed(line);
      if (unclaimedQty <= 0) continue;
      let reduction: number;
      switch (rule.discountType) {
        case 'percentage':
          reduction = (line.unitPrice * rule.discountValue) / 100;
          break;
        case 'fixedAmount':
          reduction = Math.min(rule.discountValue, line.unitPrice);
          break;
        case 'fixedPrice':
          reduction = Math.max(line.unitPrice - rule.discountValue, 0);
          break;
      }
      if (reduction <= 0) continue;
      claimed[line.lineId] += unclaimedQty;
      discount[line.lineId] += reduction * unclaimedQty;
      label[line.lineId] =
        rule.discountType === 'percentage'
          ? `${trimNumber(rule.discountValue)}% OFF`
          : rule.discountType === 'fixedAmount'
            ? `${formatCurrency(rule.discountValue)} OFF`
            : 'SALE PRICE';
    }
  }

  const result: Record<string, LineDiscount> = {};
  for (const line of lines) result[line.lineId] = { discount: round2(discount[line.lineId]), label: label[line.lineId] };
  return result;
}

export function priceCart(input: {
  lines: PricingLineInput[];
  rules?: PricingRules;
  seniorPwdApplied?: boolean;
  promoCode?: string | null;
  now?: Date;
}): PricingResult {
  const { lines, seniorPwdApplied = false } = input;
  const rules = input.rules ?? {};
  const now = input.now ?? new Date();

  const itemResults = calculateItemPromos(
    lines,
    (rules.bogo ?? []).filter((r) => isActive(r, now)),
    (rules.combo ?? []).filter((r) => isActive(r, now)),
    (rules.itemDiscount ?? []).filter((r) => isActive(r, now)),
  );
  const itemPromoAmount = Object.values(itemResults).reduce((sum, r) => sum + r.discount, 0);

  // Regular prices, before any discount.
  const gross = lines.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0);

  // What the promo code WOULD give on the regular subtotal. An unknown or expired code is dropped, exactly as
  // the server does mid-cart, instead of erroring on every line edit.
  let retainedCode: string | null = null;
  let promoCodeAmount = 0;
  const trimmed = input.promoCode?.trim();
  if (trimmed) {
    const promo = (rules.promoCodes ?? []).find((p) => p.code.toLowerCase() === trimmed.toLowerCase());
    const expired = promo?.expiresAt ? new Date(promo.expiresAt) <= now : false;
    if (promo && promo.isActive !== false && !expired) {
      retainedCode = promo.code;
      const amount = promo.discountType === 'percentage' ? round2((gross * promo.discountValue) / 100) : promo.discountValue;
      promoCodeAmount = Math.min(amount, gross);
    }
  }

  // VAT comes off first; the 20% is worked on the VAT-exclusive price.
  const vatExclusive = round2(gross / (1 + VAT_RATE));
  const vatExemptIfSenior = round2(gross - vatExclusive);
  const seniorPwdDiscountIfSenior = round2(vatExclusive * SENIOR_PWD_DISCOUNT_RATE);
  const seniorPwdSavings = round2(vatExemptIfSenior + seniorPwdDiscountIfSenior);

  let seniorPwdAmount = 0;
  let vatExemptAmount = 0;
  let appliedItemPromoAmount = 0;
  let appliedCodeAmount = 0;
  let side: PromoSide = 'none';
  let codeReason: PromoCodeNotApplied = 'none';
  let lineDiscounts: Record<string, LineDiscount> = {};
  for (const line of lines) lineDiscounts[line.lineId] = { discount: 0, label: null };

  if (seniorPwdApplied) {
    // On the regular price, not on a price already reduced by a promotion.
    vatExemptAmount = vatExemptIfSenior;
    seniorPwdAmount = seniorPwdDiscountIfSenior;
    if (retainedCode !== null) codeReason = 'suppressedBySeniorPwd';
  } else if (itemPromoAmount >= promoCodeAmount) {
    appliedItemPromoAmount = itemPromoAmount;
    lineDiscounts = itemResults;
    if (itemPromoAmount > 0) side = 'itemPromos';
    if (retainedCode !== null && promoCodeAmount > 0) codeReason = 'supersededByItemPromos';
  } else {
    appliedCodeAmount = promoCodeAmount;
    side = 'promoCode';
  }

  const discountAmount = seniorPwdAmount + appliedCodeAmount;
  return {
    lineDiscounts,
    grossSubtotal: gross,
    itemPromoDiscountAmount: appliedItemPromoAmount,
    vatExemptAmount,
    seniorPwdDiscountAmount: seniorPwdAmount,
    promoDiscountAmount: appliedCodeAmount,
    discountAmount,
    totalAmount: round2(gross - appliedItemPromoAmount - vatExemptAmount - discountAmount),
    appliedPromoCode: side === 'promoCode' ? retainedCode : null,
    retainedPromoCode: retainedCode,
    promoCodeNotApplied: codeReason,
    appliedPromoSide: side,
    seniorPwdSavings,
    promoSavings: Math.max(itemPromoAmount, promoCodeAmount),
  };
}
