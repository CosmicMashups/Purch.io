import { describe, expect, it } from 'vitest';
import { FILTERABLE_TYPES, RECORDABLE_TYPES, movementLabel, movementSchema } from './movement';
import { MovementType } from './types';

const base = { itemId: 'i', branchId: 'b', type: MovementType.StockIn as number, quantity: 5, reasonCategory: '', supplierReference: '', note: '' };
const message = (v: object) => {
  const r = movementSchema.safeParse(v);
  return r.success ? null : r.error.issues[0].message;
};

describe('movementSchema', () => {
  it('accepts a plain stock-in', () => {
    expect(movementSchema.safeParse(base).success).toBe(true);
  });

  it('rejects zero and NaN quantities', () => {
    expect(message({ ...base, quantity: 0 })).toBe('Quantity cannot be zero');
    expect(message({ ...base, quantity: NaN })).toBe('Enter a quantity');
  });

  it('allows a negative quantity only for an adjustment', () => {
    expect(message({ ...base, quantity: -2 })).toBe('Enter a quantity above zero');
    expect(movementSchema.safeParse({ ...base, type: MovementType.Adjustment, quantity: -2 }).success).toBe(true);
  });

  it('requires a reason for spoiled and a supplier reference for returns', () => {
    expect(message({ ...base, type: MovementType.Spoiled })).toBe('Say why it spoiled');
    expect(movementSchema.safeParse({ ...base, type: MovementType.Spoiled, reasonCategory: 'Expired' }).success).toBe(true);
    expect(message({ ...base, type: MovementType.ForReturn, supplierReference: '  ' })).toBe('Enter the supplier reference');
  });

  it('never lets a hand-entered movement be a Sale', () => {
    expect(message({ ...base, type: MovementType.Sale })).not.toBeNull();
    expect(RECORDABLE_TYPES).not.toContain(MovementType.Sale);
    expect(FILTERABLE_TYPES).toContain(MovementType.Sale);
  });
});

describe('movementLabel', () => {
  it('labels known types and falls back for unknown ones', () => {
    expect(movementLabel(MovementType.ForReturn)).toBe('For Return');
    expect(movementLabel(99)).toBe('Other');
  });
});
