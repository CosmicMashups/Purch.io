import { z } from 'zod';
import { PricingType, TingiMode } from './types';

const optionalTrimmed = z
  .string()
  .trim()
  .transform((v) => (v === '' ? null : v))
  .nullable()
  .optional();

// Matches add_item_screen.dart / edit_item_screen.dart: name required non-empty after trim,
// price parses to a number >= 0 (zero allowed).
export const itemBaseSchema = z.object({
  name: z.string().trim().min(1, 'Required'),
  sku: optionalTrimmed,
  barcode: optionalTrimmed,
  categoryId: optionalTrimmed,
  basePrice: z.coerce.number({ invalid_type_error: 'Enter a valid amount' }).min(0, 'Price cannot be negative'),
  imageUrl: optionalTrimmed,
});

export const createItemSchema = itemBaseSchema.extend({
  pricingType: z.nativeEnum(PricingType),
});

export const updateItemSchema = itemBaseSchema.extend({
  isActive: z.boolean(),
  departmentId: optionalTrimmed,
});

export const tingiConfigSchema = z.object({
  tingiMode: z.nativeEnum(TingiMode),
  packagedSize: z.coerce.number().positive('Enter pack size > 0').nullable().optional(),
  tingiIncrementStep: z.coerce.number().positive('Enter step > 0').nullable().optional(),
  allowedSizesCsv: z.string().optional(),
});

export const serviceDurationSchema = z.object({
  durationMinutes: z.coerce.number().int().positive('Enter duration greater than 0'),
});

export const bundleRuleSchema = z.object({
  description: z.string().trim().min(1, 'Required'),
  triggerQuantity: z.coerce.number().int().positive('Enter a quantity greater than 0'),
  bundlePrice: z.coerce.number().min(0, 'Price cannot be negative'),
});

export const variantSchema = z.object({
  attributesJson: z.string().min(1, 'Add at least one attribute'),
  sku: optionalTrimmed,
  priceOverride: z.coerce.number().nullable().optional(),
  imageUrl: optionalTrimmed,
});

export const comboComponentSchema = z.object({
  componentCategoryId: z.string().min(1, 'Select a category'),
  slotLabel: z.string().trim().min(1, 'Required'),
  quantity: z.coerce.number().int().positive('Enter a quantity greater than 0'),
  substitutionUpchargeAmount: z.coerce.number().min(0, 'Amount cannot be negative'),
});

export const categorySchema = z.object({
  name: z.string().trim().min(1, 'Required'),
  sortOrder: z.coerce.number().int(),
  imageUrl: optionalTrimmed,
});

export const modifierGroupSchema = z.object({
  name: z.string().trim().min(1, 'Required'),
  allowMultipleSelection: z.boolean(),
  isRequired: z.boolean(),
});

export const modifierSchema = z.object({
  name: z.string().trim().min(1, 'Required'),
  priceDelta: z.coerce.number(),
});

export const batchSchema = z.object({
  lotNumber: optionalTrimmed,
  expiryDate: optionalTrimmed,
  quantityReceived: z.coerce.number().int().positive('Enter a quantity greater than 0'),
});

export const lowStockThresholdSchema = z.object({
  threshold: z.coerce.number().int().nonnegative('Threshold cannot be negative').nullable().optional(),
});

export const departmentAssignmentSchema = z.object({
  departmentId: optionalTrimmed,
});
