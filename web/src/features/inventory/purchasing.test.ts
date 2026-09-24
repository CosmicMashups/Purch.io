import { describe, expect, it } from 'vitest';
import { purchaseOrderSchema, supplierSchema, transferSchema } from './purchasing';

const first = (r: { success: boolean; error?: { issues: { message: string }[] } }) => (r.success ? null : r.error?.issues[0].message);

describe('supplierSchema', () => {
  it('needs a name but not contact info', () => {
    expect(supplierSchema.safeParse({ name: 'Metro Foods', contactInfo: '' }).success).toBe(true);
    expect(first(supplierSchema.safeParse({ name: ' ', contactInfo: '' }))).toBe('Enter the supplier name');
  });
});

describe('purchaseOrderSchema', () => {
  const line = { itemId: 'i', quantityOrdered: 10, expectedUnitCost: 25.5 };
  const base = { supplierId: 's', branchId: 'b', lines: [line] };

  it('accepts a valid order', () => {
    expect(purchaseOrderSchema.safeParse(base).success).toBe(true);
  });

  it('needs at least one line', () => {
    expect(first(purchaseOrderSchema.safeParse({ ...base, lines: [] }))).toBe('Add at least one item');
  });

  it('checks each line', () => {
    expect(first(purchaseOrderSchema.safeParse({ ...base, lines: [{ ...line, quantityOrdered: 0 }] }))).toBe('Must be more than 0');
    expect(first(purchaseOrderSchema.safeParse({ ...base, lines: [{ ...line, expectedUnitCost: -1 }] }))).toBe('Cost cannot be negative');
    expect(first(purchaseOrderSchema.safeParse({ ...base, lines: [{ ...line, itemId: '' }] }))).toBe('Choose an item');
  });

  it('allows a free item (zero cost)', () => {
    expect(purchaseOrderSchema.safeParse({ ...base, lines: [{ ...line, expectedUnitCost: 0 }] }).success).toBe(true);
  });
});

describe('transferSchema', () => {
  const base = { sourceBranchId: 'a', destinationBranchId: 'b', lines: [{ itemId: 'i', quantity: 3 }] };

  it('accepts a valid transfer', () => {
    expect(transferSchema.safeParse(base).success).toBe(true);
  });

  it('refuses sending a branch its own stock', () => {
    expect(first(transferSchema.safeParse({ ...base, destinationBranchId: 'a' }))).toBe('Choose a different branch to send to');
  });

  it('requires a positive quantity and at least one line', () => {
    expect(first(transferSchema.safeParse({ ...base, lines: [{ itemId: 'i', quantity: 0 }] }))).toBe('Must be more than 0');
    expect(first(transferSchema.safeParse({ ...base, lines: [] }))).toBe('Add at least one item');
  });
});
