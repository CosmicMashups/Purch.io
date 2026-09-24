import { z } from 'zod';
import { MovementType } from './types';

export const movementLabels: Record<MovementType, string> = {
  [MovementType.StockIn]: 'Stock-In',
  [MovementType.StockOut]: 'Stock-Out',
  [MovementType.Consumption]: 'Consumption',
  [MovementType.Spoiled]: 'Spoiled',
  [MovementType.Damaged]: 'Damaged',
  [MovementType.ForReturn]: 'For Return',
  [MovementType.Transfer]: 'Transfer',
  [MovementType.Adjustment]: 'Adjustment',
  [MovementType.Sale]: 'Sale',
};

/** Sale rows come from completed sales only, so they are never offered for hand entry. */
export const RECORDABLE_TYPES: readonly MovementType[] = [
  MovementType.StockIn,
  MovementType.StockOut,
  MovementType.Consumption,
  MovementType.Spoiled,
  MovementType.Damaged,
  MovementType.ForReturn,
  MovementType.Transfer,
  MovementType.Adjustment,
];

/** Log filters can also pick Sale, so staff can see automated movements next to manual ones. */
export const FILTERABLE_TYPES: readonly MovementType[] = [...RECORDABLE_TYPES, MovementType.Sale];

export function movementLabel(type: number): string {
  return movementLabels[type as MovementType] ?? 'Other';
}

export const movementSchema = z
  .object({
    itemId: z.string().min(1, 'Choose an item'),
    branchId: z.string().min(1, 'Choose a branch'),
    type: z.number().int().min(0).max(7),
    quantity: z.number({ invalid_type_error: 'Enter a quantity', required_error: 'Enter a quantity' }),
    reasonCategory: z.string(),
    supplierReference: z.string(),
    note: z.string(),
  })
  .superRefine((v, ctx) => {
    // These mirror InventoryMovementService.RecordAsync so mistakes surface before sending.
    if (v.quantity === 0) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['quantity'], message: 'Quantity cannot be zero' });
    } else if (v.type !== MovementType.Adjustment && v.quantity < 0) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['quantity'], message: 'Enter a quantity above zero' });
    }
    if (v.type === MovementType.Spoiled && v.reasonCategory.trim() === '') {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['reasonCategory'], message: 'Say why it spoiled' });
    }
    if (v.type === MovementType.ForReturn && v.supplierReference.trim() === '') {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['supplierReference'], message: 'Enter the supplier reference' });
    }
  });

export type MovementForm = z.infer<typeof movementSchema>;

export function quantityHint(type: MovementType): string {
  return type === MovementType.Adjustment
    ? 'Use a negative number to correct stock down, positive to correct it up'
    : 'How many units moved';
}
