import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog, FormDialogLoader } from '../../../components/forms/FormDialog';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { Pill } from '../../../components/lists/QueryList';
import { QueryTable } from '../../../components/lists/QueryTable';
import type { DataColumn } from '../../../components/lists/DataTable';
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
  useUpdatePurchaseOrder,
  usePurchaseOrders,
  useSuppliers,
} from '../queries';
import { PurchaseOrderStatus, type InventoryItem, type PurchaseOrder } from '../types';
import { purchaseOrderActions, purchaseOrderStatusLabel } from '../workflow';

const actionButton = 'h-12 rounded-control bg-brand px-5 text-base font-semibold text-on-brand hover:bg-brand-strong disabled:opacity-60';
const dangerButton = 'h-12 rounded-control bg-danger px-5 text-base font-semibold text-white hover:opacity-90 disabled:opacity-60';

export function PurchaseOrdersPage() {
  const navigate = useNavigate();
  const orders = usePurchaseOrders();
  const items = useItems();
  const ingredients = useInventoryItems();
  const suppliers = useSuppliers();
  const { branches, isError: branchesFailed, error: branchesError, refetch: refetchBranches } = useSelectableBranches();
  const send = useMarkPurchaseOrderSent();
  const cancel = useCancelPurchaseOrder();
  const [cancelling, setCancelling] = useState<PurchaseOrder | null>(null);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<PurchaseOrder | null>(null);

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

  const columns: DataColumn<PurchaseOrder>[] = [
    {
      header: 'Order',
      sortValue: (o) => o.controlNumber ?? '',
      cell: (o) => <span className="font-medium text-gray-900">{o.controlNumber || 'Purchase order'}</span>,
    },
    {
      header: 'Supplier',
      sortValue: (o) => o.supplierName,
      cell: (o) => (
        <div>
          <p className="font-medium text-gray-900">{o.supplierName}</p>
          {o.paymentAccountNumber && (
            <p className="text-xs text-gray-500">
              Pay to {o.paymentAccountName ? `${o.paymentAccountName}, ` : ''}
              {o.paymentAccountNumber}
            </p>
          )}
        </div>
      ),
    },
    {
      header: 'Deliver to',
      className: 'text-gray-600',
      sortValue: (o) => o.branchName,
      cell: (o) => (
        <div>
          <p>{o.branchName}</p>
          {o.deliveryAddress && <p className="text-xs text-gray-500">{o.deliveryAddress}</p>}
        </div>
      ),
    },
    {
      header: 'Items',
      className: 'text-gray-600',
      sortValue: (o) => o.lines.length,
      cell: (o) => {
        const ordered = o.lines.reduce((n, l) => n + l.quantityOrdered, 0);
        const received = o.lines.reduce((n, l) => n + l.quantityReceived, 0);
        return (
          <div>
            <p>{o.lines.length} {o.lines.length === 1 ? 'item' : 'items'}</p>
            <p className="text-xs text-gray-500">{received} of {ordered} delivered</p>
          </div>
        );
      },
    },
    {
      header: 'Total',
      className: 'text-right tabular-nums',
      sortValue: (o) => o.orderTotal ?? null,
      cell: (o) =>
        o.orderTotal !== undefined && o.orderTotal > 0 ? (
          <div>
            <p className="font-semibold text-gray-900">{formatPeso(o.orderTotal)}</p>
            <p className="text-xs text-gray-500">VAT {o.vatInclusive ? 'included' : 'added'}: {formatPeso(o.vatAmount ?? 0)}</p>
          </div>
        ) : (
          ''
        ),
    },
    {
      header: 'Status',
      sortValue: (o) => purchaseOrderStatusLabel[o.status].label,
      cell: (o) => <Pill tone={purchaseOrderStatusLabel[o.status].tone}>{purchaseOrderStatusLabel[o.status].label}</Pill>,
    },
    {
      header: '',
      className: 'text-right',
      cell: (order) => {
        const actions = purchaseOrderActions(order.status);
        return (
          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" className="h-12 rounded-control border border-line px-5 text-base font-semibold hover:border-brand" onClick={() => printPurchaseOrder(order)}>
              Print
            </button>
            {order.status === PurchaseOrderStatus.Draft && (
              <button type="button" className={actionButton} onClick={() => setEditing(order)}>
                Edit order
              </button>
            )}
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
        );
      },
    },
  ];

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
        <QueryTable
          query={orders}
          label="Purchase orders"
          columns={columns}
          getId={(o) => o.id}
          rowLabel={(o) => `Open ${o.controlNumber || `order to ${o.supplierName}`}`}
          onRowClick={(o) => navigate(`/inventory/purchase-orders/${o.id}`)}
          errorTitle="Purchase orders could not be loaded"
          emptyMessage="No purchase orders yet."
        />

      </div>

      {(creating || editing) && (
        <FormDialogLoader
          title={editing ? 'Edit purchase order' : 'New purchase order'}
          failed={suppliers.isError ? suppliers : items.isError ? items : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null}
          ready={!!(suppliers.data && items.data && branches)}
          onClose={() => (editing ? setEditing(null) : setCreating(false))}
        >
          {suppliers.data && items.data && branches && (
            <PurchaseOrderDialog
              suppliers={suppliers.data}
              branches={branches}
              items={items.data}
              ingredients={(ingredients.data ?? []).filter((i) => !i.isAutoCreatedForItem && i.isActive !== false)}
              order={editing ?? undefined}
              onClose={() => (editing ? setEditing(null) : setCreating(false))}
            />
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
  order,
  onClose,
}: {
  suppliers: { id: string; name: string }[];
  branches: Branch[];
  items: Item[];
  ingredients: InventoryItem[];
  /** Set to edit a draft instead of creating a new order. */
  order?: PurchaseOrder;
  onClose: () => void;
}) {
  const create = useCreatePurchaseOrder();
  const update = useUpdatePurchaseOrder();
  const [vatInclusive, setVatInclusive] = useState(order?.vatInclusive ?? false);
  const [discount, setDiscount] = useState(order?.discountAmount ? String(order.discountAmount) : '');
  const [remarks, setRemarks] = useState(order?.remarks ?? '');
  const [dateNeeded, setDateNeeded] = useState(order?.dateNeeded ?? '');
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<PurchaseOrderForm>({
    resolver: zodResolver(purchaseOrderSchema),
    defaultValues: order
      ? {
          supplierId: order.supplierId,
          branchId: order.branchId,
          lines: order.lines.map((l) => ({
            itemId: l.inventoryItemId ? `ing:${l.inventoryItemId}` : l.itemId,
            quantityOrdered: l.quantityOrdered,
            expectedUnitCost: l.expectedUnitCost,
          })) as PurchaseOrderForm['lines'],
        }
      : { supplierId: '', branchId: branches.length === 1 ? branches[0].id : '', lines: [{ itemId: '' }] as PurchaseOrderForm['lines'] },
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const submit = handleSubmit(async (v) => {
    const lines = v.lines.map((line) => {
      const ingredient = line.itemId.startsWith('ing:') ? ingredients.find((i) => i.id === line.itemId.slice(4)) : undefined;
      return ingredient
        ? { itemId: '00000000-0000-0000-0000-000000000000', inventoryItemId: ingredient.id, quantityOrdered: line.quantityOrdered, expectedUnitCost: line.expectedUnitCost, unit: ingredient.baseUnit }
        : line;
    });
    if (order) {
      await update.mutateAsync({
        id: order.id,
        body: { supplierId: v.supplierId, lines, vatInclusive, discountAmount: Number(discount) || 0, remarks: remarks.trim() || null, dateNeeded: dateNeeded || null },
      });
      toast.success('Purchase order updated');
      onClose();
      return;
    }
    await create.mutateAsync({ ...v, lines, vatInclusive, discountAmount: Number(discount) || 0, remarks: remarks.trim() || null, dateNeeded: dateNeeded || null });
    toast.success('Purchase order created as a draft');
    onClose();
  });

  return (
    <FormDialog
      title={order ? 'Edit purchase order' : 'New purchase order'}
      wide
      busy={create.isPending || update.isPending}
      submitLabel={order ? 'Save changes' : 'Create draft'}
      onSubmit={submit}
      onClose={onClose}
    >
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
        <select {...register('branchId')} disabled={Boolean(order)} className={controlClass}>
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

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Date needed (optional)">
          <input type="date" value={dateNeeded} onChange={(e) => setDateNeeded(e.target.value)} className={controlClass} />
        </FormField>
        <FormField label="Remarks (optional)">
          <input value={remarks} onChange={(e) => setRemarks(e.target.value)} className={controlClass} />
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
