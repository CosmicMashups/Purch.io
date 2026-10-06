import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ErrorState } from '../../../components/ErrorState';
import { toast } from '../../../components/feedback/toastStore';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { FormCombobox, type ComboboxGroup } from '../../../components/forms/GroupedCombobox';
import { Modal } from '../../../components/Modal';
import { Skeleton } from '../../../components/Skeleton';
import { userMessage } from '../../../lib/apiError';
import { useSelectableBranches } from '../../branches/queries';
import type { Branch } from '../../branches/types';
import { RECORDABLE_TYPES, movementLabel, movementSchema, quantityHint, type MovementForm } from '../movement';
import { useRecordMovement } from '../queries';
import { stockRefFields, useStockOptions } from '../stockOptions';
import { MovementType } from '../types';

const FORM_ID = 'record-movement-form';

export interface MovementPrefill {
  /** `item:<id>` or `ingredient:<id>`. */
  stockRef?: string;
  type?: number;
}

/** Records one stock movement against an item or an ingredient. Opened from the movement log and the Inventory home. */
export function RecordMovementDialog({ prefill, onClose }: { prefill: MovementPrefill; onClose: () => void }) {
  const stock = useStockOptions();
  const record = useRecordMovement();
  const { branches, isError: branchesFailed, error: branchesError, refetch: refetchBranches } = useSelectableBranches();
  const loadFailed = stock.failed ? { error: stock.failed.error, refetch: stock.failed.refetch } : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null;

  return (
    <Modal open title="Record movement" onClose={onClose} footer={<Footer onClose={onClose} busy={record.isPending} ready={stock.ready && !!branches && !loadFailed} />}>
      {loadFailed ? (
        <ErrorState title="The form could not be loaded" message={userMessage(loadFailed.error)} onRetry={() => void loadFailed.refetch()} />
      ) : !stock.ready || !branches ? (
        <div className="flex flex-col gap-3" aria-busy="true">
          <Skeleton className="h-14 w-full" />
          <Skeleton className="h-14 w-full" />
          <Skeleton className="h-14 w-full" />
        </div>
      ) : (
        <RecordMovementForm record={record} groups={stock.groups} branches={branches} prefill={prefill} onDone={onClose} />
      )}
    </Modal>
  );
}

function Footer({ onClose, ready, busy }: { onClose: () => void; ready: boolean; busy: boolean }) {
  return (
    <div className="flex justify-end gap-3">
      <SecondaryButton type="button" onClick={onClose}>
        Cancel
      </SecondaryButton>
      <PrimaryButton type="submit" form={FORM_ID} busy={busy} disabled={!ready}>
        {busy ? 'Saving...' : 'Record movement'}
      </PrimaryButton>
    </div>
  );
}

function RecordMovementForm({
  record,
  groups,
  branches,
  prefill,
  onDone,
}: {
  record: ReturnType<typeof useRecordMovement>;
  groups: ComboboxGroup[];
  branches: Branch[];
  prefill: MovementPrefill;
  onDone: () => void;
}) {
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<MovementForm>({
    resolver: zodResolver(movementSchema),
    defaultValues: {
      stockRef: prefill.stockRef ?? '',
      // A single option (a branch-scoped account) needs no choosing.
      branchId: branches.length === 1 ? branches[0].id : '',
      type: prefill.type ?? MovementType.StockIn,
      reasonCategory: '',
      supplierReference: '',
      note: '',
    },
  });
  const type = Number(useWatch({ control, name: 'type' }));

  const submit = handleSubmit(async (v) => {
    await record.mutateAsync({
      ...stockRefFields(v.stockRef),
      branchId: v.branchId,
      type: v.type as MovementType,
      quantity: v.quantity,
      note: v.note.trim() || null,
      reasonCategory: v.reasonCategory.trim() || null,
      photoUrl: null,
      supplierReference: v.supplierReference.trim() || null,
    });
    toast.success('Movement recorded');
    onDone();
  });

  return (
    <form id={FORM_ID} onSubmit={submit} noValidate className="flex flex-col gap-4">
      <FormField label="Item or ingredient" error={errors.stockRef?.message}>
        <FormCombobox control={control} name="stockRef" groups={groups} placeholder="Search items and ingredients" />
      </FormField>

      <FormField label="Branch" error={errors.branchId?.message}>
        <select {...register('branchId')} className={controlClass}>
          <option value="">Choose a branch</option>
          {branches.map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name}
            </option>
          ))}
        </select>
      </FormField>

      <FormField label="Movement type">
        <select {...register('type', { valueAsNumber: true })} className={controlClass}>
          {RECORDABLE_TYPES.map((t) => (
            <option key={t} value={t}>
              {movementLabel(t)}
            </option>
          ))}
        </select>
      </FormField>

      <FormField label="Quantity" hint={quantityHint(type as MovementType)} error={errors.quantity?.message}>
        <input type="number" inputMode="decimal" step="any" {...register('quantity', { valueAsNumber: true })} className={controlClass} />
      </FormField>

      {type === MovementType.Spoiled && (
        <FormField label="Reason category" hint="For example: expired, dropped, contaminated" error={errors.reasonCategory?.message}>
          <input {...register('reasonCategory')} className={controlClass} />
        </FormField>
      )}

      {type === MovementType.ForReturn && (
        <FormField label="Supplier reference" error={errors.supplierReference?.message}>
          <input {...register('supplierReference')} className={controlClass} />
        </FormField>
      )}

      <FormField label="Note (optional)">
        <input {...register('note')} className={controlClass} />
      </FormField>
    </form>
  );
}
