import { useState } from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog, FormDialogLoader } from '../../../components/forms/FormDialog';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { FormCombobox, type ComboboxGroup } from '../../../components/forms/GroupedCombobox';
import { ErrorState } from '../../../components/ErrorState';
import { CompactList, CompactRow } from '../../../components/lists/CompactList';
import { Pill } from '../../../components/lists/QueryList';
import { RowActionsMenu } from '../../../components/RowActionsMenu';
import { SkeletonList } from '../../../components/Skeleton';
import { userMessage } from '../../../lib/apiError';
import { PageHeader } from '../../../components/PageHeader';
import { useBranches, useSelectableBranches } from '../../branches/queries';
import type { Branch } from '../../branches/types';
import { stockRefFields, useStockOptions } from '../stockOptions';
import { transferSchema, type TransferForm } from '../purchasing';
import { useCancelTransfer, useCreateTransfer, useMarkTransferInTransit, useMarkTransferReceived, useTransfers } from '../queries';
import type { BranchTransfer } from '../types';
import { transferActions, transferStatusLabel, type TransferAction } from '../workflow';

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
  const stock = useStockOptions();
  const [creating, setCreating] = useState(false);
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
      <PageHeader
        title="Stock transfers"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={
          <PrimaryButton type="button" onClick={() => setCreating(true)}>
            New transfer
          </PrimaryButton>
        }
      />
      <div>
        {transfers.isPending && <SkeletonList />}
        {transfers.isError && <ErrorState title="Transfers could not be loaded" message={userMessage(transfers.error)} onRetry={() => void transfers.refetch()} />}
        {transfers.isSuccess && transfers.data.length === 0 && (
          <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">No transfers yet.</p>
        )}
        {transfers.isSuccess && transfers.data.length > 0 && (
          <CompactList label="Stock transfers">
            {transfers.data.map((transfer) => {
              const status = transferStatusLabel[transfer.status];
              const label: Record<TransferAction, string> = { ship: 'Send', receive: 'Mark received', cancel: 'Cancel transfer' };
              const actions = transferActions(transfer.status).map((action) => ({
                label: label[action],
                danger: action === 'cancel',
                separated: action === 'cancel',
                onSelect: () => setPending({ action, transfer }),
              }));
              return (
                <CompactRow
                  key={transfer.id}
                  title={`${transfer.sourceBranchName} to ${transfer.destinationBranchName}`}
                  detail={<p className="truncate">{transfer.lines.map((line) => `${line.itemName} ${line.quantity}`).join(', ')}</p>}
                  aside={<Pill tone={status.tone}>{status.label}</Pill>}
                  menu={<RowActionsMenu subject={`${transfer.sourceBranchName} to ${transfer.destinationBranchName}`} actions={actions} />}
                />
              );
            })}
          </CompactList>
        )}
      </div>

      {creating && (
        <FormDialogLoader
          title="New transfer"
          failed={stock.failed ? stock.failed : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null}
          ready={!!(branches && allBranches.data && stock.ready)}
          onClose={() => setCreating(false)}
        >
          {branches && allBranches.data && (
            <TransferDialog sourceOptions={branches} destinationOptions={allBranches.data} groups={stock.groups} onClose={() => setCreating(false)} />
          )}
        </FormDialogLoader>
      )}

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

function TransferDialog({
  sourceOptions,
  destinationOptions,
  groups,
  onClose,
}: {
  sourceOptions: Branch[];
  destinationOptions: Branch[];
  groups: ComboboxGroup[];
  onClose: () => void;
}) {
  const create = useCreateTransfer();
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<TransferForm>({
    resolver: zodResolver(transferSchema),
    defaultValues: {
      sourceBranchId: sourceOptions.length === 1 ? sourceOptions[0].id : '',
      destinationBranchId: '',
      lines: [{ stockRef: '' }] as TransferForm['lines'],
    },
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const submit = handleSubmit(async (v) => {
    await create.mutateAsync({
      sourceBranchId: v.sourceBranchId,
      destinationBranchId: v.destinationBranchId,
      lines: v.lines.map((l) => ({ ...stockRefFields(l.stockRef), quantity: l.quantity })),
    });
    toast.success('Transfer created');
    onClose();
  });

  return (
    <FormDialog title="New transfer" wide busy={create.isPending} submitLabel="Create transfer" onSubmit={submit} onClose={onClose}>
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
        <legend className="text-base font-semibold">Stock to send</legend>
        {fields.map((field, index) => (
          <div key={field.id} className="flex flex-col gap-3 rounded-control border border-line p-3">
            <FormField label="Item or ingredient" error={errors.lines?.[index]?.stockRef?.message}>
              <FormCombobox control={control} name={`lines.${index}.stockRef`} groups={groups} placeholder="Search items and ingredients" />
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
        <SecondaryButton type="button" onClick={() => append({ stockRef: '' } as TransferForm['lines'][number])}>
          Add another item
        </SecondaryButton>
      </fieldset>
    </FormDialog>
  );
}
