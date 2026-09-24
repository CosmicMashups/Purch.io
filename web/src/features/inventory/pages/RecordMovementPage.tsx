import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ErrorState } from '../../../components/ErrorState';
import { Skeleton } from '../../../components/Skeleton';
import { toast } from '../../../components/feedback/toastStore';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { PageHeader } from '../../../components/PageHeader';
import { userMessage } from '../../../lib/apiError';
import type { Branch } from '../../branches/types';
import { useSelectableBranches } from '../../branches/queries';
import { useItems } from '../../catalog/queries';
import type { Item } from '../../catalog/types';
import { RECORDABLE_TYPES, movementLabel, movementSchema, quantityHint, type MovementForm } from '../movement';
import { useRecordMovement } from '../queries';
import { MovementType } from '../types';

function initialType(raw: string | null): number {
  const parsed = raw === null ? NaN : Number(raw);
  return (RECORDABLE_TYPES as readonly number[]).includes(parsed) ? parsed : MovementType.StockIn;
}

export function RecordMovementPage() {
  const items = useItems();
  const { branches, isError: branchesFailed, error: branchesError, refetch: refetchBranches } = useSelectableBranches();
  const loadFailed = items.isError ? items : branchesFailed ? { error: branchesError, refetch: refetchBranches } : null;

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <PageHeader title="Record movement" backTo={{ to: '/inventory', label: 'Inventory' }} />
      {loadFailed ? (
        <ErrorState title="The form could not be loaded" message={userMessage(loadFailed.error)} onRetry={() => void loadFailed.refetch()} />
      ) : !items.data || !branches ? (
        <div className="flex flex-col gap-3" aria-busy="true">
          <Skeleton className="h-14 w-full" />
          <Skeleton className="h-14 w-full" />
          <Skeleton className="h-14 w-full" />
        </div>
      ) : (
        <RecordMovementForm items={items.data} branches={branches} />
      )}
    </div>
  );
}

function RecordMovementForm({ items, branches }: { items: Item[]; branches: Branch[] }) {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const record = useRecordMovement();

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<MovementForm>({
    resolver: zodResolver(movementSchema),
    defaultValues: {
      itemId: params.get('itemId') ?? '',
      // A single option (a branch-scoped account) needs no choosing.
      branchId: branches.length === 1 ? branches[0].id : '',
      type: initialType(params.get('type')),
      reasonCategory: '',
      supplierReference: '',
      note: '',
    },
  });
  const type = Number(useWatch({ control, name: 'type' }));

  const submit = handleSubmit(async (v) => {
    await record.mutateAsync({
      itemId: v.itemId,
      branchId: v.branchId,
      type: v.type as MovementType,
      quantity: v.quantity,
      note: v.note.trim() || null,
      reasonCategory: v.reasonCategory.trim() || null,
      photoUrl: null,
      supplierReference: v.supplierReference.trim() || null,
    });
    toast.success('Movement recorded');
    navigate('/inventory/movements');
  });

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-5">
      <FormField label="Item" error={errors.itemId?.message}>
        <select {...register('itemId')} className={controlClass}>
          <option value="">Choose an item</option>
          {items.map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
            </option>
          ))}
        </select>
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

      <div>
        <PrimaryButton type="submit" busy={record.isPending}>
          {record.isPending ? 'Saving...' : 'Record movement'}
        </PrimaryButton>
      </div>
    </form>
  );
}
