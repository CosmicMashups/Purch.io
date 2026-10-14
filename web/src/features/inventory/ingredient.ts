import { z } from 'zod';

const num = (message: string) => z.number({ invalid_type_error: message, required_error: message });

export const ingredientSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name'),
  sku: z.string(),
  baseUnit: z.string().trim().min(1, 'Enter the unit you count in, like g or pc'),
  packagingUnit: z.string().trim().min(1, 'Enter how it is bought, like case or sack'),
  packagingSize: num('Enter a number').gt(0, 'Must be more than 0'),
  lowStockThreshold: z.string(),
  /** Empty string means uncategorised. */
  categoryId: z.string(),
  isActive: z.boolean(),
  /** Peso text per base unit; blank means unknown. */
  unitCost: z.string().optional(),
});

export type IngredientForm = z.infer<typeof ingredientSchema>;

/** A blank threshold means "no alert level" (null). Anything typed must be a number that is not negative. */
export function parseThreshold(text: string): { ok: true; value: number | null } | { ok: false; message: string } {
  const trimmed = text.trim();
  if (trimmed === '') return { ok: true, value: null };
  const value = Number(trimmed);
  if (!Number.isFinite(value)) return { ok: false, message: 'Enter a number' };
  if (value < 0) return { ok: false, message: 'Cannot be negative' };
  return { ok: true, value };
}

/** Blank means unknown (null). A typed unit cost is a peso amount from 0 up to 999999.99, at most 4 decimals (per-gram costs are small). */
export function parseUnitCost(text: string): { ok: true; value: number | null } | { ok: false; message: string } {
  const trimmed = text.trim();
  if (trimmed === '') return { ok: true, value: null };
  if (!/^\d+(\.\d{1,4})?$/.test(trimmed)) return { ok: false, message: /^-/.test(trimmed) ? 'Cost cannot be negative' : 'Enter an amount with up to 4 decimals' };
  const value = Number(trimmed);
  if (value > 999999.99) return { ok: false, message: 'Cost cannot be more than 999,999.99' };
  return { ok: true, value };
}

export const countSchema = z.object({
  branchId: z.string().min(1, 'Choose a branch'),
  quantityOnHand: num('Enter the counted quantity').min(0, 'Cannot be negative'),
});

export const receiveSchema = z.object({
  branchId: z.string().min(1, 'Choose a branch'),
  packagesReceived: num('Enter how many arrived').gt(0, 'Must be more than 0'),
  supplierReference: z.string(),
});

export type CountForm = z.infer<typeof countSchema>;
export type ReceiveForm = z.infer<typeof receiveSchema>;
