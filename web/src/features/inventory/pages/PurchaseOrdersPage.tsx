import { useState } from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { EditorCard } from '../../../components/forms/EditorCard';
import { FormField, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { ListCard, Pill, QueryList } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import { FormLoader } from '../../../components/forms/FormLoader';
import { useSelectableBranches } from '../../branches/queries';
import type { Branch } from '../../branches/types';
import { useItems } from '../../catalog/queries';
import type { Item } from '../../catalog/types';
import { formatPeso } from '../../dashboard/format';
import { purchaseOrderSchema, type PurchaseOrderForm } from '../purchasing';
import {
  useCancelPurchaseOrder,
  useCreatePurchaseOrder,
  useMarkPurchaseOrderSent,
  usePurchaseOrders,
  useReceivePurchaseOrder,
  useSuppliers,
} from '../queries';
import type { PurchaseOrder } from '../types';
import { buildReceiveLines, purchaseOrderActions, purchaseOrderStatusLabel, remainingToReceive, type ReceiveEntries } from '../workflow';

type Panel = { mode: 'create' } | { mode: 'receive'; order: PurchaseOrder };

const actionButton = 'h-12 rounded-control border border-line px-5 text-base font-semibold hover:border-brand disabled:opacity-60';

export function PurchaseOrdersPage() {
  const orders = usePurchaseOrders();
  const items = useItems();
  const suppliers = useSuppliers();
  const { branches, isError: branchesFailed, error: branchesError, refetch: refetchBranches } = useSelectableBranches();
  const send = useMarkPurchaseOrderSent();
  const cancel = useCancelPurchaseOrder();
  const [panel, setPanel] = useState<Panel>({ mode: 'create' });
  const [cancelling, setCancelling] = useState<PurchaseOrder | null>(null);

  async function onSend(order: PurchaseOrder) {
    await send.mutateAsync(order.id);
    toast.success(`Order to ${order.supplierName} marked as sent`);
  }

  async function onConfirmCancel() {
    if (!cancelling) return;
    const order = cancelling;
    try {
      await cancel.mutateAsync(order.id);
      toast.success(`Order to ${order.supplierName} cancelled`);
      if (panel.mode === 'receive' && panel.order.id === order.id) setPanel({ mode: 'create' });
    } finally {
      setCancelling(null);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Purchase orders" backTo={{ to: '/inventory', label: 'Inventory' }} />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,28rem)]">
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
                  <p className="text-base font-semibold">{order.supplierName}</p>
                  <p className="text-sm text-ink-soft">Deliver to {order.branchName}</p>
                  <ul className="mt-2 flex flex-col gap-1">
                    {order.lines.map((line) => (
                      <li key={line.id} className="text-base tabular-nums">
                        {line.itemName}: {line.quantityReceived} of {line.quantityOrdered} received at {formatPeso(line.expectedUnitCost)} each
                      </li>
                    ))}
                  </ul>
                  {actions.length > 0 && (
                    <div className="mt-3 flex flex-wrap gap-2">
                      {actions.includes('send') && (
                        <button type="button" className={actionButton} disabled={send.isPending} onClick={() => void onSend(order)}>
                          Mark as sent
                        </button>
                      )}
                      {actions.includes('receive') && (
                        <button type="button" className={actionButton} onClick={() => setPanel({ mode: 'receive', order })}>
                          Receive delivery
                        </button>
                      )}
                      {actions.includes('cancel') && (
                        <button type="button" className={`${actionButton} text-danger`} onClick={() => setCancelling(order)}>
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

        {panel.mode === 'receive' ? (
          <ReceivePanel key={panel.order.id} order={panel.order} onDone={() => setPanel({ mode: 'create' })} />
        ) : (
          <FormLoader
            failed={suppliers.isError ? suppliers : items.isError ? items : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null}
            ready={!!(suppliers.data && items.data && branches)}
          >
            {suppliers.data && items.data && branches && <PurchaseOrderForm suppliers={suppliers.data} branches={branches} items={items.data} />}
          </FormLoader>
        )}
      </div>

      <ConfirmModal
        open={cancelling !== null}
        destructive
        busy={cancel.isPending}
        title="Cancel this purchase order?"
        description={cancelling ? `The order to ${cancelling.supplierName} will be cancelled and can no longer be received.` : undefined}
        confirmLabel="Cancel order"
        onConfirm={() => void onConfirmCancel()}
        onCancel={() => setCancelling(null)}
      />
    </div>
  );
}

function PurchaseOrderForm({ suppliers, branches, items }: { suppliers: { id: string; name: string }[]; branches: Branch[]; items: Item[] }) {
  const create = useCreatePurchaseOrder();
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<PurchaseOrderForm>({
    resolver: zodResolver(purchaseOrderSchema),
    defaultValues: { supplierId: '', branchId: branches.length === 1 ? branches[0].id : '', lines: [{ itemId: '' }] as PurchaseOrderForm['lines'] },
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const submit = handleSubmit(async (v) => {
    await create.mutateAsync(v);
    toast.success('Purchase order created as a draft');
    reset({ supplierId: '', branchId: v.branchId, lines: [{ itemId: '' }] as PurchaseOrderForm['lines'] });
  });

  return (
    <EditorCard title="purchase order" editing={false} busy={create.isPending} submitLabel="Create draft" onSubmit={submit} onCancel={() => reset()}>
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

      <fieldset className="flex flex-col gap-4">
        <legend className="text-base font-semibold">Items</legend>
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
    </EditorCard>
  );
}

function ReceivePanel({ order, onDone }: { order: PurchaseOrder; onDone: () => void }) {
  const receive = useReceivePurchaseOrder();
  const [entries, setEntries] = useState<ReceiveEntries>({});
  const [problem, setProblem] = useState<{ lineId?: string; message: string } | null>(null);

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    const result = buildReceiveLines(entries);
    if (!result.ok) {
      setProblem({ lineId: result.lineId, message: result.message });
      return;
    }
    setProblem(null);
    await receive.mutateAsync({ id: order.id, body: { lines: result.lines } });
    toast.success('Delivery recorded');
    onDone();
  }

  return (
    <EditorCard
      title="delivery"
      heading={`Receive delivery: ${order.supplierName}`}
      cancelable
      editing={false}
      busy={receive.isPending}
      submitLabel="Record delivery"
      onSubmit={(e) => void onSubmit(e)}
      onCancel={onDone}
    >
      <p className="text-base text-ink-soft">Enter only what arrived today. It adds to what was already received.</p>
      {order.lines.map((line) => (
        <FormField
          key={line.id}
          label={`${line.itemName}: arrived now`}
          hint={`${remainingToReceive(line)} still to come`}
          error={problem?.lineId === line.id ? problem.message : undefined}
        >
          <input
            inputMode="decimal"
            value={entries[line.id] ?? ''}
            onChange={(e) => setEntries((current) => ({ ...current, [line.id]: e.target.value }))}
            className={controlClass}
          />
        </FormField>
      ))}
      {problem && !problem.lineId && (
        <p role="alert" className="text-sm font-medium text-danger">
          {problem.message}
        </p>
      )}
    </EditorCard>
  );
}
