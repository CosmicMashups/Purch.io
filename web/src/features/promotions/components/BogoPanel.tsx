import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { toast } from '../../../components/feedback/toastStore';
import { describeWindow, isoToLocalInput, localInputToIso } from '../../../lib/dates';
import type { Item } from '../../catalog/types';
import { useBogoRules, useCreateBogo, useUpdateBogo } from '../queries';
import { bogoSchema, type BogoForm } from '../schemas';
import type { BogoRule } from '../types';
import { ActiveBadge, ItemSelect, PromoRowMenu, ScheduleFields } from './shared';
import { itemNameOf } from '../format';
import { DeletedRecordsPanel, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';
import { FormDialog } from '../../../components/forms/FormDialog';
import { QueryTable } from '../../../components/lists/QueryTable';
import type { DataColumn } from '../../../components/lists/DataTable';

const EMPTY: BogoForm = {
  name: '',
  triggerItemId: '',
  triggerQuantity: 1,
  freeItemId: '',
  freeQuantity: 1,
  startsAt: '',
  endsAt: '',
  isActive: true,
};

export function BogoPanel({ items, view }: { items: Item[]; view: StatusView }) {
  const { run, dialog: lifecycleDialog } = useLifecycle();
  const rules = useBogoRules();
  // null: closed; 'new': adding; otherwise the rule being edited.
  const [dialog, setDialog] = useState<'new' | BogoRule | null>(null);

  const columns: DataColumn<BogoRule>[] = [
    { header: 'Promotion', sortValue: (r) => r.name, cell: (r) => <span className="font-medium text-gray-900">{r.name}</span> },
    {
      header: 'Deal',
      className: 'text-gray-600',
      sortValue: (r) => itemNameOf(items, r.triggerItemId),
      cell: (r) => `Buy ${r.triggerQuantity} ${itemNameOf(items, r.triggerItemId)}, get ${r.freeQuantity} ${itemNameOf(items, r.freeItemId)} free`,
    },
    { header: 'Runs', className: 'text-gray-600', sortValue: (r) => r.startsAt, cell: (r) => describeWindow(r.startsAt, r.endsAt) },
    { header: 'Status', sortValue: (r) => (r.isActive ? 'Active' : 'Off'), cell: (r) => <ActiveBadge active={r.isActive} /> },
    {
      header: '',
      className: 'text-right',
      cell: (r) => (
        <div className="flex justify-end">
          <PromoRowMenu subject={r.name} active={r.isActive} onEdit={() => setDialog(r)} onLifecycle={(action) => run({ kind: 'BogoPromo', id: r.id, name: r.name }, action)} />
        </div>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <div>
        <PrimaryButton type="button" onClick={() => setDialog('new')}>
          Add promotion
        </PrimaryButton>
      </div>
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="BogoPromo" noun="Buy 1 Take 1 promotions" />
      ) : (
      <QueryTable
        query={rules}
        label="Buy 1 Take 1 promotions"
        columns={columns}
        getId={(r) => r.id}
        transform={(rows) => rows.filter((r) => (view === 'active' ? r.isActive : !r.isActive))}
        emptyMessage={view === 'active' ? "No Buy 1 Take 1 promotions yet." : "No inactive Buy 1 Take 1 promotions."}
      />
      )}
      {lifecycleDialog}
      {dialog && <BogoDialog rule={dialog === 'new' ? null : dialog} items={items} onClose={() => setDialog(null)} />}
    </div>
  );
}

function BogoDialog({ rule, items, onClose }: { rule: BogoRule | null; items: Item[]; onClose: () => void }) {
  const create = useCreateBogo();
  const update = useUpdateBogo();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<BogoForm>({
    resolver: zodResolver(bogoSchema),
    defaultValues: rule
      ? {
          name: rule.name,
          triggerItemId: rule.triggerItemId,
          triggerQuantity: rule.triggerQuantity,
          freeItemId: rule.freeItemId,
          freeQuantity: rule.freeQuantity,
          startsAt: isoToLocalInput(rule.startsAt),
          endsAt: isoToLocalInput(rule.endsAt),
          isActive: rule.isActive,
        }
      : EMPTY,
  });

  const submit = handleSubmit(async (v) => {
    const body = {
      name: v.name,
      triggerItemId: v.triggerItemId,
      triggerQuantity: v.triggerQuantity,
      freeItemId: v.freeItemId,
      freeQuantity: v.freeQuantity,
      startsAt: localInputToIso(v.startsAt),
      endsAt: localInputToIso(v.endsAt),
    };
    if (rule) await update.mutateAsync({ id: rule.id, body: { ...body, isActive: v.isActive } });
    else await create.mutateAsync(body);
    toast.success(rule ? 'Promotion updated' : 'Promotion added');
    onClose();
  });

  return (
    <FormDialog
      title={rule ? 'Edit promotion' : 'Add promotion'}
      wide
      submitLabel={rule ? 'Save changes' : 'Add'}
      busy={create.isPending || update.isPending}
      onSubmit={submit}
      onClose={onClose}
    >
      <FormField label="Name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      <div className="grid gap-4 sm:grid-cols-2">
        <ItemSelect label="Buy this item" name="triggerItemId" register={register} errors={errors} items={items} />
        <FormField label="Buy quantity" error={errors.triggerQuantity?.message}>
          <input type="number" inputMode="numeric" {...register('triggerQuantity', { valueAsNumber: true })} className={controlClass} />
        </FormField>
        <ItemSelect label="Get this item free" name="freeItemId" register={register} errors={errors} items={items} />
        <FormField label="Free quantity" error={errors.freeQuantity?.message}>
          <input type="number" inputMode="numeric" {...register('freeQuantity', { valueAsNumber: true })} className={controlClass} />
        </FormField>
      </div>
      <ScheduleFields register={register} errors={errors} startName="startsAt" endName="endsAt" />
      {rule && (
        <label className="flex h-12 items-center gap-3 text-base font-semibold">
          <input type="checkbox" {...register('isActive')} className="size-6 accent-brand" />
          Active
        </label>
      )}
    </FormDialog>
  );
}
