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
import { useBranches, useSelectableBranches } from '../../branches/queries';
import type { Branch } from '../../branches/types';
import { useItems } from '../../catalog/queries';
import type { Item } from '../../catalog/types';
import { transferSchema, type TransferForm } from '../purchasing';
import { useCancelTransfer, useCreateTransfer, useMarkTransferInTransit, useMarkTransferReceived, useTransfers } from '../queries';
import type { BranchTransfer } from '../types';
import { transferActions, transferStatusLabel, type TransferAction } from '../workflow';

const actionButton = 'h-12 rounded-control border border-line px-5 text-base font-semibold hover:border-brand disabled:opacity-60';

const CONFIRM_COPY: Record<TransferAction, { title: string; verb: string; describe: (t: BranchTransfer) => string; destructive: boolean }> = {
  ship: {
    title: 'Send this transfer?',
    verb: 'Send transfer',
    describe: (t) => `Stock leaves ${t.sourceBranchName} now and is on its way to ${t.destinationBranchName}.`,
    destructive: false,
  },
  receive: {
    title: 'Mark this transfer as received?',
    verb: 'Mark received',
    describe: (t) => `The stock is added to ${t.destinationBranchName}. Do this only once it has arrived.`,
    destructive: false,
  },
  cancel: {
    title: 'Cancel this transfer?',
    verb: 'Cancel transfer',
    describe: (t) => `The transfer to ${t.destinationBranchName} is called off. Stock already sent goes back to ${t.sourceBranchName}.`,
    destructive: true,
  },
};

export function TransfersPage() {
  const transfers = useTransfers();
  const items = useItems();
  const { branches, isError: branchesFailed, error: branchesError, refetch: refetchBranches } = useSelectableBranches();
  const allBranches = useBranches();
  const ship = useMarkTransferInTransit();
  const receive = useMarkTransferReceived();
  const cancel = useCancelTransfer();
  const [pending, setPending] = useState<{ action: TransferAction; transfer: BranchTransfer } | null>(null);

  const mutations = { ship, receive, cancel };
  const busy = pending ? mutations[pending.action].isPending : false;

  async function onConfirm() {
    if (!pending) return;
    const { action, transfer } = pending;
    try {
      await mutations[action].mutateAsync(transfer.id);
      toast.success(action === 'ship' ? 'Transfer sent' : action === 'receive' ? 'Transfer received' : 'Transfer cancelled');
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Stock transfers" backTo={{ to: '/inventory', label: 'Inventory' }} />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,28rem)]">
        <QueryList
          query={transfers}
          errorTitle="Transfers could not be loaded"
          emptyMessage="No transfers yet."
          renderRow={(transfer) => {
            const status = transferStatusLabel[transfer.status];
            const actions = transferActions(transfer.status);
            const label: Record<TransferAction, string> = { ship: 'Send', receive: 'Mark received', cancel: 'Cancel' };
            return (
              <ListCard key={transfer.id}>
                <div className="min-w-0 flex-1">
                  <p className="text-base font-semibold">
                    {transfer.sourceBranchName} to {transfer.destinationBranchName}
                  </p>
                  <ul className="mt-1 flex flex-col gap-1">
                    {transfer.lines.map((line) => (
                      <li key={line.id} className="text-base tabular-nums">
                        {line.itemName}: {line.quantity}
                      </li>
                    ))}
                  </ul>
                  {actions.length > 0 && (
                    <div className="mt-3 flex flex-wrap gap-2">
                      {actions.map((action) => (
                        <button
                          key={action}
                          type="button"
                          className={`${actionButton} ${action === 'cancel' ? 'text-danger' : ''}`}
                          onClick={() => setPending({ action, transfer })}
                        >
                          {label[action]}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
                <Pill tone={status.tone}>{status.label}</Pill>
              </ListCard>
            );
          }}
        />
        <FormLoader
          failed={items.isError ? items : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null}
          ready={!!(branches && allBranches.data && items.data)}
        >
          {branches && allBranches.data && items.data && <TransferForm sourceOptions={branches} destinationOptions={allBranches.data} items={items.data} />}
        </FormLoader>
      </div>

      <ConfirmModal
        open={pending !== null}
        destructive={pending ? CONFIRM_COPY[pending.action].destructive : false}
        busy={busy}
        title={pending ? CONFIRM_COPY[pending.action].title : ''}
        description={pending ? CONFIRM_COPY[pending.action].describe(pending.transfer) : undefined}
        confirmLabel={pending ? CONFIRM_COPY[pending.action].verb : 'Confirm'}
        onConfirm={() => void onConfirm()}
        onCancel={() => setPending(null)}
      />
    </div>
  );
}

function TransferForm({ sourceOptions, destinationOptions, items }: { sourceOptions: Branch[]; destinationOptions: Branch[]; items: Item[] }) {
  const create = useCreateTransfer();
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<TransferForm>({
    resolver: zodResolver(transferSchema),
    defaultValues: {
      sourceBranchId: sourceOptions.length === 1 ? sourceOptions[0].id : '',
      destinationBranchId: '',
      lines: [{ itemId: '' }] as TransferForm['lines'],
    },
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const submit = handleSubmit(async (v) => {
    await create.mutateAsync(v);
    toast.success('Transfer created');
    reset({ sourceBranchId: v.sourceBranchId, destinationBranchId: '', lines: [{ itemId: '' }] as TransferForm['lines'] });
  });

  return (
    <EditorCard title="transfer" editing={false} busy={create.isPending} submitLabel="Create transfer" onSubmit={submit} onCancel={() => reset()}>
      <FormField label="Send from" error={errors.sourceBranchId?.message}>
        <select {...register('sourceBranchId')} className={controlClass}>
          <option value="">Choose a branch</option>
          {sourceOptions.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Send to" error={errors.destinationBranchId?.message}>
        <select {...register('destinationBranchId')} className={controlClass}>
          <option value="">Choose a branch</option>
          {destinationOptions.map((b) => (
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
            <FormField label="Quantity" error={errors.lines?.[index]?.quantity?.message}>
              <input type="number" inputMode="decimal" step="any" {...register(`lines.${index}.quantity`, { valueAsNumber: true })} className={controlClass} />
            </FormField>
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
        <SecondaryButton type="button" onClick={() => append({ itemId: '' } as TransferForm['lines'][number])}>
          Add another item
        </SecondaryButton>
      </fieldset>
    </EditorCard>
  );
}
