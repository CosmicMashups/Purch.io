import { z } from 'zod';

const num = (message: string) => z.number({ invalid_type_error: message, required_error: message });

export const supplierSchema = z.object({
  name: z.string().trim().min(1, 'Enter the supplier name'),
  contactInfo: z.string(),
});

export type SupplierForm = z.infer<typeof supplierSchema>;

export const purchaseOrderSchema = z.object({
  supplierId: z.string().min(1, 'Choose a supplier'),
  branchId: z.string().min(1, 'Choose a branch'),
  lines: z
    .array(
      z.object({
        itemId: z.string().min(1, 'Choose an item'),
        quantityOrdered: num('Enter a quantity').gt(0, 'Must be more than 0'),
        expectedUnitCost: num('Enter a cost').min(0, 'Cost cannot be negative'),
      }),
    )
    .min(1, 'Add at least one item'),
});

export type PurchaseOrderForm = z.infer<typeof purchaseOrderSchema>;

export const transferSchema = z
  .object({
    sourceBranchId: z.string().min(1, 'Choose the sending branch'),
    destinationBranchId: z.string().min(1, 'Choose the receiving branch'),
    lines: z
      .array(
        z.object({
          itemId: z.string().min(1, 'Choose an item'),
          quantity: num('Enter a quantity').gt(0, 'Must be more than 0'),
        }),
      )
      .min(1, 'Add at least one item'),
  })
  .superRefine((v, ctx) => {
    if (v.sourceBranchId && v.sourceBranchId === v.destinationBranchId) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['destinationBranchId'], message: 'Choose a different branch to send to' });
    }
  });

export type TransferForm = z.infer<typeof transferSchema>;
