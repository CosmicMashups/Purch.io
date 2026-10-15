import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog, FormDialogLoader } from '../../../components/forms/FormDialog';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { ListCard, Pill, QueryList } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import { useSelectableBranches } from '../../branches/queries';
import type { Branch } from '../../branches/types';
import { useItems } from '../../catalog/queries';
import type { Item } from '../../catalog/types';
import { formatPeso } from '../../dashboard/format';
import { printPurchaseOrder } from '../printPurchaseOrder';
import { purchaseOrderSchema, type PurchaseOrderForm } from '../purchasing';
import {
  useCancelPurchaseOrder,
  useCreatePurchaseOrder,
  useInventoryItems,
  useMarkPurchaseOrderSent,
  usePurchaseOrders,
  useSuppliers,
} from '../queries';
import type { InventoryItem, PurchaseOrder } from '../types';
import { purchaseOrderActions, purchaseOrderStatusLabel } from '../workflow';

const actionButton = 'h-12 rounded-control bg-brand px-5 text-base font-semibold text-on-brand hover:bg-brand-strong disabled:opacity-60';
const dangerButton = 'h-12 rounded-control bg-danger px-5 text-base font-semibold text-white hover:opacity-90 disabled:opacity-60';

export function PurchaseOrdersPage() {
  const orders = usePurchaseOrders();
  const items = useItems();
  const ingredients = useInventoryItems();
  const suppliers = useSuppliers();
  const { branches, isError: branchesFailed, error: branchesError, refetch: refetchBranches } = useSelectableBranches();
  const send = useMarkPurchaseOrderSent();
  const cancel = useCancelPurchaseOrder();
  const [cancelling, setCancelling] = useState<PurchaseOrder | null>(null);
  const [creating, setCreating] = useState(false);

  async function onSend(order: PurchaseOrder) {
    await send.mutateAsync(order.id);
    toast.success(`Order to ${order.supplierName} submitted`);
  }

  async function onConfirmCancel() {
    if (!cancelling) return;
    const order = cancelling;
    try {
      await cancel.mutateAsync(order.id);
      toast.success(`Order to ${order.supplierName} cancelled`);
    } finally {
      setCancelling(null);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Purchase orders"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={
          <PrimaryButton type="button" onClick={() => setCreating(true)}>
            New purchase order
          </PrimaryButton>
        }
      />
      <div>
        <QueryList
          query={orders}
          errorTitle="Purchase orders could not be loaded"
          emptyMessage="No purchase orders yet."
          renderRow={(order) => {
            const status = purchaseOrderStatusLabel[order.status];
            const actions = purchaseOrderActions(order.status);
            return (
              <ListCard key={order.id}>
                <div className="min-w-0 flex-1">
                  <p className="text-base font-semibold">
                    {order.controlNumber ? `${order.controlNumber}: ` : ''}
                    {order.supplierName}
                  </p>
                  <p className="text-sm text-ink-soft">
                    Deliver to {order.branchName}
                    {order.deliveryAddress ? `, ${order.deliveryAddress}` : ''}
                  </p>
                  {order.paymentAccountNumber && (
                    <p className="text-sm text-ink-soft">
                      Pay to {order.paymentAccountName ? `${order.paymentAccountName}, ` : ''}
                      {order.paymentAccountNumber}
                    </p>
                  )}
                  <ul className="mt-2 flex flex-col gap-1">
                    {order.lines.map((line) => (
                      <li key={line.id} className="text-base tabular-nums">
                        {line.itemName}: {line.quantityReceived} of {line.quantityOrdered} {line.unit ?? ''} delivered at {formatPeso(line.expectedUnitCost)} each
                      </li>
                    ))}
                  </ul>
                  {order.orderTotal !== undefined && order.orderTotal > 0 && (
                    <dl className="mt-2 grid max-w-xs grid-cols-[1fr_auto] gap-x-4 text-sm tabular-nums">
                      <dt className="text-ink-soft">Subtotal</dt>
                      <dd>{formatPeso(order.subtotal ?? 0)}</dd>
                      {(order.discountAmount ?? 0) > 0 && (
                        <>
                          <dt className="text-ink-soft">Less discount</dt>
                          <dd>{formatPeso(order.discountAmount ?? 0)}</dd>
                        </>
                      )}
                      <dt className="text-ink-soft">VAT {order.vatInclusive ? '(included)' : '(added)'}</dt>
                      <dd>{formatPeso(order.vatAmount ?? 0)}</dd>
                      <dt className="font-semibold">Order total</dt>
                      <dd className="font-semibold">{formatPeso(order.orderTotal)}</dd>
                    </dl>
                  )}
                  {order.receipts.length > 0 && (
                    <p className="mt-2 text-sm text-ink-soft">
                      {order.receipts.length} delivery {order.receipts.length === 1 ? 'report' : 'reports'}, last on {order.receipts[order.receipts.length - 1].deliveryDate}
                    </p>
                  )}
                  <button type="button" className="mt-3 h-12 rounded-control border border-line px-5 text-base font-semibold hover:border-brand" onClick={() => printPurchaseOrder(order)}>
                    Print
                  </button>
                  {actions.length > 0 && (
                    <div className="mt-3 flex flex-wrap gap-2">
                      {actions.includes('send') && (
                        <button type="button" className={actionButton} disabled={send.isPending} onClick={() => void onSend(order)}>
                          Submit order
                        </button>
                      )}
                      {actions.includes('record-delivery') && (
                        <Link to={`/inventory/incoming-receiving?po=${order.id}`} className={`${actionButton} inline-flex items-center`}>
                          Record delivery
                        </Link>
                      )}
                      {actions.includes('cancel') && (
                        <button type="button" className={dangerButton} onClick={() => setCancelling(order)}>
                          Cancel order
                        </button>
                      )}
                    </div>
                  )}
                </div>
                <Pill tone={status.tone}>{status.label}</Pill>
              </ListCard>
            );
          }}
        />

      </div>

      {creating && (
        <FormDialogLoader
          title="New purchase order"
          failed={suppliers.isError ? suppliers : items.isError ? items : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null}
          ready={!!(suppliers.data && items.data && branches)}
          onClose={() => setCreating(false)}
        >
          {suppliers.data && items.data && branches && (
            <PurchaseOrderDialog
              suppliers={suppliers.data}
              branches={branches}
              items={items.data}
              ingredients={(ingredients.data ?? []).filter((i) => !i.isAutoCreatedForItem && i.isActive !== false)}
              onClose={() => setCreating(false)} />
          )}
        </FormDialogLoader>
      )}

      <ConfirmModal
        open={cancelling !== null}
        destructive
        busy={cancel.isPending}
        title="Cancel this purchase order?"
        description={cancelling ? `The order to ${cancelling.supplierName} will be cancelled and can no longer receive deliveries.` : undefined}
        confirmLabel="Cancel order"
        onConfirm={() => void onConfirmCancel()}
        onCancel={() => setCancelling(null)}
      />
    </div>
  );
}

function PurchaseOrderDialog({
  suppliers,
  branches,
  items,
  ingredients,
  onClose,
}: {
  suppliers: { id: string; name: string }[];
  branches: Branch[];
  items: Item[];
  ingredients: InventoryItem[];
  onClose: () => void;
}) {
  const create = useCreatePurchaseOrder();
  const [vatInclusive, setVatInclusive] = useState(false);
  const [discount, setDiscount] = useState('');
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<PurchaseOrderForm>({
    resolver: zodResolver(purchaseOrderSchema),
    defaultValues: { supplierId: '', branchId: branches.length === 1 ? branches[0].id : '', lines: [{ itemId: '' }] as PurchaseOrderForm['lines'] },
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const submit = handleSubmit(async (v) => {
    const lines = v.lines.map((line) => {
      const ingredient = line.itemId.startsWith('ing:') ? ingredients.find((i) => i.id === line.itemId.slice(4)) : undefined;
      return ingredient
        ? { itemId: '00000000-0000-0000-0000-000000000000', inventoryItemId: ingredient.id, quantityOrdered: line.quantityOrdered, expectedUnitCost: line.expectedUnitCost, unit: ingredient.baseUnit }
        : line;
    });
    await create.mutateAsync({ ...v, lines, vatInclusive, discountAmount: Number(discount) || 0 });
    toast.success('Purchase order created as a draft');
    onClose();
  });

  return (
    <FormDialog title="New purchase order" wide busy={create.isPending} submitLabel="Create draft" onSubmit={submit} onClose={onClose}>
      <FormField label="Supplier" error={errors.supplierId?.message}>
        <select {...register('supplierId')} className={controlClass}>
          <option value="">Choose a supplier</option>
          {suppliers.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Deliver to branch" error={errors.branchId?.message}>
        <select {...register('branchId')} className={controlClass}>
          <option value="">Choose a branch</option>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </FormField>

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="flex h-12 items-center gap-2 text-base">
          <input type="checkbox" className="size-6" checked={vatInclusive} onChange={(e) => setVatInclusive(e.target.checked)} />
          Unit costs include VAT
        </label>
        <FormField label="Discount (whole order)">
          <input type="number" inputMode="decimal" min="0" step="0.01" value={discount} onChange={(e) => setDiscount(e.target.value)} className={controlClass} />
        </FormField>
      </div>

      <fieldset className="flex flex-col gap-4">
        <legend className="text-base font-semibold">Items</legend>
        {fields.map((field, index) => (
          <div key={field.id} className="flex flex-col gap-3 rounded-control border border-line p-3">
            <FormField label="Item" error={errors.lines?.[index]?.itemId?.message}>
              <select {...register(`lines.${index}.itemId`)} className={controlClass}>
                <option value="">Choose an item or ingredient</option>
                {items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
                {ingredients.length > 0 && (
                  <optgroup label="Ingredients">
                    {ingredients.map((ingredient) => (
                      <option key={ingredient.id} value={`ing:${ingredient.id}`}>
                        {ingredient.name} ({ingredient.baseUnit})
                      </option>
                    ))}
                  </optgroup>
                )}
              </select>
            </FormField>
            <div className="grid grid-cols-2 gap-3">
              <FormField label="Quantity" error={errors.lines?.[index]?.quantityOrdered?.message}>
                <input type="number" inputMode="decimal" step="any" {...register(`lines.${index}.quantityOrdered`, { valueAsNumber: true })} className={controlClass} />
              </FormField>
              <FormField label="Unit cost (PHP)" error={errors.lines?.[index]?.expectedUnitCost?.message}>
                <input type="number" inputMode="decimal" step="0.01" {...register(`lines.${index}.expectedUnitCost`, { valueAsNumber: true })} className={controlClass} />
              </FormField>
            </div>
            {fields.length > 1 && (
              <button type="button" onClick={() => remove(index)} className="h-12 self-start text-base font-semibold text-danger underline">
                Remove item
              </button>
            )}
          </div>
        ))}
        {(errors.lines?.message ?? errors.lines?.root?.message) && (
          <p role="alert" className="text-sm font-medium text-danger">
            {errors.lines?.message ?? errors.lines?.root?.message}
          </p>
        )}
        <SecondaryButton type="button" onClick={() => append({ itemId: '' } as PurchaseOrderForm['lines'][number])}>
          Add another item
        </SecondaryButton>
      </fieldset>
    </FormDialog>
  );
}
