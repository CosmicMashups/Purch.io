import { z } from 'zod';

const num = (message: string) => z.number({ invalid_type_error: message, required_error: message });

export const supplierSchema = z.object({
  name: z.string().trim().min(1, 'Enter the supplier name'),
  specialization: z.string(),
  address: z.string(),
  tin: z.string(),
  remarks: z.string(),
  contacts: z.array(
    z.object({
      contactPerson: z.string(),
      modes: z.array(z.string()),
      numbers: z.array(z.object({ value: z.string() })),
      emails: z.array(z.object({ value: z.string().refine((v) => v.trim() === '' || /^\S+@\S+\.\S+$/.test(v.trim()), 'Enter a valid email') })),
    }),
  ),
});

export type SupplierForm = z.infer<typeof supplierSchema>;

export const emptyContact = (): SupplierForm['contacts'][number] => ({
  contactPerson: '',
  modes: [],
  numbers: [{ value: '' }],
  emails: [{ value: '' }],
});

export const emptySupplierForm = (): SupplierForm => ({ name: '', specialization: '', address: '', tin: '', remarks: '', contacts: [emptyContact()] });

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
          stockRef: z.string().min(1, 'Choose an item or ingredient'),
          quantity: num('Enter a quantity').gt(0, 'Must be more than 0'),
        }),
      )
      .min(1, 'Add at least one line'),
  })
  .superRefine((v, ctx) => {
    if (v.sourceBranchId && v.sourceBranchId === v.destinationBranchId) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['destinationBranchId'], message: 'Choose a different branch to send to' });
    }
  });

export type TransferForm = z.infer<typeof transferSchema>;

export const incomingReceivingSchema = z.object({
  purchaseOrderId: z.string(),
  supplierId: z.string().min(1, 'Choose a supplier'),
  branchId: z.string().min(1, 'Choose a branch'),
  deliveryDate: z.string().min(1, 'Enter the delivery date'),
  remarks: z.string(),
  lines: z
    .array(
      z.object({
        itemId: z.string().min(1, 'Choose an item'),
        quantityReceived: num('Enter a quantity').gt(0, 'Must be more than 0'),
        uom: z.string().trim().min(1, 'Enter the unit'),
        unitPrice: num('Enter a price').min(0, 'Price cannot be negative'),
        condition: z.enum(['0', '1']),
        remark: z.enum(['0', '1']),
      }),
    )
    .min(1, 'Add at least one item'),
});

export type IncomingReceivingForm = z.infer<typeof incomingReceivingSchema>;
