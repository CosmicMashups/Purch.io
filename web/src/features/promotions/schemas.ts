import { z } from 'zod';
import { PromoDiscountType } from './types';

// These only catch what staff can fix before sending. The API applies the real promo rules.
const requiredNumber = (message: string) => z.number({ invalid_type_error: message, required_error: message });
const name = z.string().trim().min(1, 'Enter a name');
const itemId = z.string().min(1, 'Choose an item');
const positiveInt = requiredNumber('Enter a whole number').int('Enter a whole number').min(1, 'Must be at least 1');
const discountType = z.union([
  z.literal(PromoDiscountType.Percentage),
  z.literal(PromoDiscountType.FixedAmount),
  z.literal(PromoDiscountType.FixedPrice),
]);
const optionalDateTime = z.string();

type Windowed = { startsAt: string; endsAt: string };

function endsAfterStart(value: Windowed, ctx: z.RefinementCtx): void {
  if (value.startsAt && value.endsAt && new Date(value.endsAt) <= new Date(value.startsAt)) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['endsAt'], message: 'End must be after the start' });
  }
}

function percentageWithinRange(value: { discountType: number; discountValue: number }, ctx: z.RefinementCtx): void {
  if (value.discountType === PromoDiscountType.Percentage && value.discountValue > 100) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['discountValue'], message: 'A percentage cannot be more than 100' });
  }
}

const discountValue = requiredNumber('Enter an amount').gt(0, 'Must be more than 0');

export const bogoSchema = z
  .object({
    name,
    triggerItemId: itemId,
    triggerQuantity: positiveInt,
    freeItemId: itemId,
    freeQuantity: positiveInt,
    startsAt: optionalDateTime,
    endsAt: optionalDateTime,
    isActive: z.boolean(),
  })
  .superRefine(endsAfterStart);

export const comboSchema = z
  .object({
    name,
    itemAId: itemId,
    itemBId: itemId,
    comboPrice: requiredNumber('Enter a price').min(0, 'Price cannot be negative'),
    startsAt: optionalDateTime,
    endsAt: optionalDateTime,
    isActive: z.boolean(),
  })
  .superRefine((value, ctx) => {
    endsAfterStart(value, ctx);
    if (value.itemAId && value.itemAId === value.itemBId) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['itemBId'], message: 'Choose two different items' });
    }
  });

export const itemDiscountSchema = z
  .object({
    name,
    itemId,
    discountType,
    discountValue,
    startsAt: optionalDateTime,
    endsAt: optionalDateTime,
    isActive: z.boolean(),
  })
  .superRefine((value, ctx) => {
    endsAfterStart(value, ctx);
    percentageWithinRange(value, ctx);
  });

export const promoCodeSchema = z
  .object({
    code: z.string().trim().min(1, 'Enter a code'),
    discountType: z.union([z.literal(PromoDiscountType.Percentage), z.literal(PromoDiscountType.FixedAmount)]),
    discountValue,
    expiresAt: optionalDateTime,
  })
  .superRefine(percentageWithinRange);

export type BogoForm = z.infer<typeof bogoSchema>;
export type ComboForm = z.infer<typeof comboSchema>;
export type ItemDiscountForm = z.infer<typeof itemDiscountSchema>;
export type PromoCodeForm = z.infer<typeof promoCodeSchema>;
