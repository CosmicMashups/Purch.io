import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { EditorCard } from '../../../components/forms/EditorCard';
import { FormField, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { FormLoader } from '../../../components/forms/FormLoader';
import { ListCard, Pill, QueryList } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import { useSession } from '../../auth/useSession';
import { useSelectableBranches } from '../../branches/queries';
import type { Branch } from '../../branches/types';
import { useItems } from '../../catalog/queries';
import type { Item } from '../../catalog/types';
import { formatPeso } from '../../dashboard/format';
import { incomingReceivingSchema, type IncomingReceivingForm } from '../purchasing';
import { useCreateIncomingReceiving, useIncomingReceiving, useLinkIncomingReceiving, usePurchaseOrders, useSuppliers } from '../queries';
import { PurchaseOrderStatus, ReceivingCondition, ReceivingRemark, type IncomingReceiving, type PurchaseOrder } from '../types';
import { conditionLabel, isPurchaseOrderOpen, remarkLabel } from '../workflow';

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
  const reports = useIncomingReceiving();
  const orders = usePurchaseOrders();
  const items = useItems();
  const suppliers = useSuppliers();
  const { branches, isError: branchesFailed, error: branchesError, refetch: refetchBranches } = useSelectableBranches();
  const { role } = useSession();
  const canLink = role === 'Admin' || role === 'Manager';
  const [params] = useSearchParams();

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Incoming receiving report" backTo={{ to: '/inventory', label: 'Inventory' }} />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,30rem)]">
        <QueryList
          query={reports}
          errorTitle="Receiving reports could not be loaded"
          emptyMessage="No deliveries recorded yet."
          renderRow={(report) => <ReportRow key={report.id} report={report} orders={orders.data ?? []} canLink={canLink} />}
        />
        <FormLoader
          failed={suppliers.isError ? suppliers : items.isError ? items : orders.isError ? orders : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null}
          ready={!!(suppliers.data && items.data && orders.data && branches)}
        >
          {suppliers.data && items.data && orders.data && branches && (
            <ReportForm
              suppliers={suppliers.data}
              branches={branches}
              items={items.data}
              orders={orders.data}
              initialOrderId={params.get('po') ?? ''}
            />
          )}
        </FormLoader>
      </div>
    </div>
  );
}

function ReportRow({ report, orders, canLink }: { report: IncomingReceiving; orders: PurchaseOrder[]; canLink: boolean }) {
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
    <ListCard>
      <div className="min-w-0 flex-1">
        <p className="text-base font-semibold">{report.supplierName}</p>
        <p className="text-sm text-ink-soft">
          Delivered {report.deliveryDate} to {report.branchName}. Received by {report.receivedByName}.
        </p>
        <ul className="mt-2 flex flex-col gap-1">
          {report.lines.map((line) => {
            const remark = remarkLabel[line.remark];
            return (
              <li key={line.id} className="flex flex-wrap items-center gap-x-2 text-base tabular-nums">
                <span>
                  {line.itemName}: {line.quantityReceived} {line.uom} at {formatPeso(line.unitPrice)}
                </span>
                <span className="text-sm text-ink-soft">{conditionLabel[line.condition]}</span>
                <Pill tone={remark.tone}>{remark.label}</Pill>
              </li>
            );
          })}
        </ul>
        {report.remarks && <p className="mt-2 text-sm text-ink-soft">{report.remarks}</p>}
        {report.purchaseOrderId === null && canLink && (
          <div className="mt-3 flex flex-wrap items-end gap-2">
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
      {report.purchaseOrderId === null ? (
        <Pill tone="warn">Not linked</Pill>
      ) : (
        <Pill tone="brand">{linked ? `PO to ${linked.supplierName}` : 'Linked to PO'}</Pill>
      )}
    </ListCard>
  );
}

function ReportForm({
  suppliers,
  branches,
  items,
  orders,
  initialOrderId,
}: {
  suppliers: { id: string; name: string }[];
  branches: Branch[];
  items: Item[];
  orders: PurchaseOrder[];
  initialOrderId: string;
}) {
  const create = useCreateIncomingReceiving();
  const openOrders = orders.filter((o) => o.status === PurchaseOrderStatus.Sent || o.status === PurchaseOrderStatus.PartiallyReceived);
  const soleBranch = branches.length === 1 ? branches[0].id : '';
  const {
    register,
    control,
    handleSubmit,
    reset,
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
            itemId: l.itemId,
            quantityReceived: l.quantityOrdered - l.quantityReceived,
            uom: 'pc',
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
        itemId: l.itemId,
        quantityReceived: l.quantityReceived,
        uom: l.uom.trim(),
        unitPrice: l.unitPrice,
        condition: Number(l.condition) as ReceivingCondition,
        remark: Number(l.remark) as ReceivingRemark,
      })),
    });
    toast.success('Delivery recorded');
    reset(blankForm(v.branchId));
  });

  return (
    <EditorCard title="delivery" heading="Record a delivery" editing={false} busy={create.isPending} submitLabel="Save report" onSubmit={submit} onCancel={() => reset(blankForm(soleBranch))}>
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
                <option value="">Choose an item</option>
                {items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
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
    </EditorCard>
  );
}
