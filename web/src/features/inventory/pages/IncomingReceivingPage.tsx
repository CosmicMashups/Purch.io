import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog, FormDialogLoader } from '../../../components/forms/FormDialog';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { Pill } from '../../../components/lists/QueryList';
import { QueryTable } from '../../../components/lists/QueryTable';
import type { DataColumn } from '../../../components/lists/DataTable';
import { PageHeader } from '../../../components/PageHeader';
import { useSession } from '../../auth/useSession';
import { useSelectableBranches } from '../../branches/queries';
import type { Branch } from '../../branches/types';
import { useItems } from '../../catalog/queries';
import type { Item } from '../../catalog/types';
import { incomingReceivingSchema, type IncomingReceivingForm } from '../purchasing';
import { useCreateIncomingReceiving, useIncomingReceiving, useInventoryItems, useLinkIncomingReceiving, usePurchaseOrders, useSuppliers } from '../queries';
import { PurchaseOrderStatus, ReceivingCondition, ReceivingRemark, type IncomingReceiving, type InventoryItem, type PurchaseOrder } from '../types';
import { isPurchaseOrderOpen } from '../workflow';

const today = () => new Date().toISOString().slice(0, 10);

const blankLine = () =>
  ({ itemId: '', uom: 'pc', condition: '0', remark: '0' }) as IncomingReceivingForm['lines'][number];

const blankForm = (branchId = ''): IncomingReceivingForm => ({
  purchaseOrderId: '',
  supplierId: '',
  branchId,
  deliveryDate: today(),
  remarks: '',
  lines: [blankLine()],
});

export function IncomingReceivingPage() {
  const navigate = useNavigate();
  const reports = useIncomingReceiving();
  const orders = usePurchaseOrders();
  const items = useItems();
  const ingredients = useInventoryItems();
  const suppliers = useSuppliers();
  const { branches, isError: branchesFailed, error: branchesError, refetch: refetchBranches } = useSelectableBranches();
  const { role } = useSession();
  const canLink = role === 'Admin' || role === 'Manager';
  const [params, setParams] = useSearchParams();
  // Arriving from a purchase order's "Record delivery" button opens the dialog already filled from that order.
  const [dialog, setDialog] = useState<{ orderId: string } | null>(params.get('po') ? { orderId: params.get('po') ?? '' } : null);
  function closeDialog() {
    setDialog(null);
    if (params.has('po')) {
      const next = new URLSearchParams(params);
      next.delete('po');
      setParams(next, { replace: true });
    }
  }

  const columns: DataColumn<IncomingReceiving>[] = [
    {
      header: 'Report',
      className: 'whitespace-nowrap',
      sortValue: (r) => r.controlNumber ?? '',
      cell: (r) => <span className="font-medium text-gray-900">{r.controlNumber || 'Not numbered'}</span>,
    },
    { header: 'Delivered', className: 'whitespace-nowrap text-gray-600', sortValue: (r) => r.deliveryDate, cell: (r) => r.deliveryDate },
    { header: 'Supplier', sortValue: (r) => r.supplierName, cell: (r) => <span className="font-medium text-gray-900">{r.supplierName}</span> },
    { header: 'Branch', className: 'text-gray-600', sortValue: (r) => r.branchName, cell: (r) => r.branchName },
    { header: 'Received by', className: 'text-gray-600', sortValue: (r) => r.receivedByName, cell: (r) => r.receivedByName },
    {
      header: 'Items',
      className: 'text-gray-600',
      sortValue: (r) => r.lines.length,
      cell: (r) => {
        const rejected = r.lines.filter((l) => l.remark === ReceivingRemark.Rejected).length;
        return (
          <div>
            <p>{r.lines.length} {r.lines.length === 1 ? 'item' : 'items'}</p>
            {rejected > 0 && <p className="text-xs text-danger">{rejected} rejected</p>}
          </div>
        );
      },
    },
    {
      header: 'Purchase order',
      sortValue: (r) => (r.purchaseOrderId === null ? 'Not linked' : (() => { const po = orders.data?.find((o) => o.id === r.purchaseOrderId); return po?.controlNumber || `PO to ${po?.supplierName ?? ''}`; })()),
      cell: (r) => <ReportLink report={r} orders={orders.data ?? []} canLink={canLink} />,
    },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Incoming receiving report"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={
          <PrimaryButton type="button" onClick={() => setDialog({ orderId: '' })}>
            Record a delivery
          </PrimaryButton>
        }
      />
      <div>
        <QueryTable
          query={reports}
          label="Incoming receiving reports"
          columns={columns}
          getId={(r) => r.id}
          rowLabel={(r) => `Open ${r.controlNumber || `delivery from ${r.supplierName}`}`}
          onRowClick={(r) => navigate(`/inventory/incoming-receiving/${r.id}`)}
          errorTitle="Receiving reports could not be loaded"
          emptyMessage="No deliveries recorded yet."
        />
      </div>

      {dialog && (
        <FormDialogLoader
          title="Record a delivery"
          failed={suppliers.isError ? suppliers : items.isError ? items : orders.isError ? orders : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null}
          ready={!!(suppliers.data && items.data && orders.data && branches)}
          onClose={closeDialog}
        >
          {suppliers.data && items.data && orders.data && branches && (
            <ReportDialog suppliers={suppliers.data} branches={branches} items={items.data} ingredients={(ingredients.data ?? []).filter((i) => !i.isAutoCreatedForItem && i.isActive !== false)} orders={orders.data} initialOrderId={dialog.orderId} onClose={closeDialog} />
          )}
        </FormDialogLoader>
      )}
    </div>
  );
}

function ReportLink({ report, orders, canLink }: { report: IncomingReceiving; orders: PurchaseOrder[]; canLink: boolean }) {
  const link = useLinkIncomingReceiving();
  const [chosen, setChosen] = useState('');
  const linked = orders.find((o) => o.id === report.purchaseOrderId);
  const candidates = orders.filter((o) => o.supplierId === report.supplierId && o.branchId === report.branchId && isPurchaseOrderOpen(o.status));

  async function onLink() {
    if (!chosen) return;
    await link.mutateAsync({ id: report.id, purchaseOrderId: chosen });
    toast.success('Report linked to the purchase order');
    setChosen('');
  }

  return (
    <div className="flex flex-col items-start gap-2">
      {report.purchaseOrderId === null ? (
        <Pill tone="warn">Not linked</Pill>
      ) : (
        <Pill tone="brand">{linked ? linked.controlNumber || `PO to ${linked.supplierName}` : 'Linked to PO'}</Pill>
      )}
      {report.purchaseOrderId === null && canLink && (
        <div className="flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-sm font-medium">
            Link to purchase order
            <select value={chosen} onChange={(e) => setChosen(e.target.value)} className={controlClass}>
              <option value="">{candidates.length > 0 ? 'Choose a purchase order' : 'No matching open order'}</option>
              {candidates.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.supplierName}: {o.lines.map((l) => l.itemName).join(', ')}
                </option>
              ))}
            </select>
          </label>
          <SecondaryButton type="button" onClick={() => void onLink()} disabled={!chosen || link.isPending}>
            Link
          </SecondaryButton>
        </div>
      )}
    </div>
  );
}

function ReportDialog({
  suppliers,
  branches,
  items,
  ingredients,
  orders,
  initialOrderId,
  onClose,
}: {
  suppliers: { id: string; name: string }[];
  branches: Branch[];
  items: Item[];
  ingredients: InventoryItem[];
  orders: PurchaseOrder[];
  initialOrderId: string;
  onClose: () => void;
}) {
  const create = useCreateIncomingReceiving();
  const openOrders = orders.filter((o) => o.status === PurchaseOrderStatus.Sent || o.status === PurchaseOrderStatus.PartiallyReceived);
  const soleBranch = branches.length === 1 ? branches[0].id : '';
  const {
    register,
    control,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<IncomingReceivingForm>({ resolver: zodResolver(incomingReceivingSchema), defaultValues: blankForm(soleBranch) });
  const { fields, append, remove, replace } = useFieldArray({ control, name: 'lines' });

  function fillFromOrder(orderId: string) {
    setValue('purchaseOrderId', orderId);
    const order = openOrders.find((o) => o.id === orderId);
    if (!order) return;
    setValue('supplierId', order.supplierId);
    setValue('branchId', order.branchId);
    const remaining = order.lines.filter((l) => l.quantityOrdered > l.quantityReceived);
    replace(
      remaining.map(
        (l) =>
          ({
            itemId: l.inventoryItemId ? `ing:${l.inventoryItemId}` : l.itemId,
            quantityReceived: l.quantityOrdered - l.quantityReceived,
            uom: l.unit ?? 'pc',
            unitPrice: l.expectedUnitCost,
            condition: '0',
            remark: '0',
          }) as IncomingReceivingForm['lines'][number],
      ),
    );
  }

  useEffect(() => {
    if (initialOrderId) fillFromOrder(initialOrderId);
    // Only prefill once, when arriving from a purchase order's "Record delivery" button.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialOrderId]);

  const submit = handleSubmit(async (v) => {
    await create.mutateAsync({
      purchaseOrderId: v.purchaseOrderId || null,
      supplierId: v.supplierId,
      branchId: v.branchId,
      deliveryDate: v.deliveryDate,
      remarks: v.remarks.trim() || null,
      lines: v.lines.map((l) => ({
        itemId: l.itemId.startsWith('ing:') ? '00000000-0000-0000-0000-000000000000' : l.itemId,
        inventoryItemId: l.itemId.startsWith('ing:') ? l.itemId.slice(4) : null,
        quantityReceived: l.quantityReceived,
        uom: l.uom.trim(),
        unitPrice: l.unitPrice,
        condition: Number(l.condition) as ReceivingCondition,
        remark: Number(l.remark) as ReceivingRemark,
      })),
    });
    toast.success('Delivery recorded');
    onClose();
  });

  return (
    <FormDialog title="Record a delivery" wide busy={create.isPending} submitLabel="Save report" onSubmit={submit} onClose={onClose}>
      <FormField label="Purchase order (optional)" hint="Leave blank if the order has not been created yet. An admin can link it later.">
        <select {...register('purchaseOrderId', { onChange: (e) => fillFromOrder(e.target.value) })} className={controlClass}>
          <option value="">Not linked to an order</option>
          {openOrders.map((o) => (
            <option key={o.id} value={o.id}>
              {o.supplierName} to {o.branchName}: {o.lines.map((l) => l.itemName).join(', ')}
            </option>
          ))}
        </select>
      </FormField>
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
      <FormField label="Received at branch" error={errors.branchId?.message}>
        <select {...register('branchId')} className={controlClass}>
          <option value="">Choose a branch</option>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Date of delivery" error={errors.deliveryDate?.message}>
        <input type="date" {...register('deliveryDate')} className={controlClass} />
      </FormField>

      <fieldset className="flex flex-col gap-4">
        <legend className="text-base font-semibold">Items received</legend>
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
            <div className="grid grid-cols-3 gap-3">
              <FormField label="Quantity" error={errors.lines?.[index]?.quantityReceived?.message}>
                <input type="number" inputMode="decimal" step="any" {...register(`lines.${index}.quantityReceived`, { valueAsNumber: true })} className={controlClass} />
              </FormField>
              <FormField label="UOM" error={errors.lines?.[index]?.uom?.message}>
                <input {...register(`lines.${index}.uom`)} className={controlClass} />
              </FormField>
              <FormField label="Unit price (PHP)" error={errors.lines?.[index]?.unitPrice?.message}>
                <input type="number" inputMode="decimal" step="0.01" {...register(`lines.${index}.unitPrice`, { valueAsNumber: true })} className={controlClass} />
              </FormField>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <FormField label="Condition">
                <select {...register(`lines.${index}.condition`)} className={controlClass}>
                  <option value="0">Good</option>
                  <option value="1">Not good</option>
                </select>
              </FormField>
              <FormField label="Remarks" hint="Rejected items add no stock">
                <select {...register(`lines.${index}.remark`)} className={controlClass}>
                  <option value="0">Accepted</option>
                  <option value="1">Rejected</option>
                </select>
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
        <SecondaryButton type="button" onClick={() => append(blankLine())}>
          Add another item
        </SecondaryButton>
      </fieldset>

      <FormField label="Notes (optional)">
        <textarea {...register('remarks')} rows={2} className={controlClass} />
      </FormField>
    </FormDialog>
  );
}
