import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { toast } from '../../../components/feedback/toastStore';
import { describeWindow, isoToLocalInput, localInputToIso } from '../../../lib/dates';
import type { Item } from '../../catalog/types';
import { describeDiscount } from '../format';
import { useCreateItemDiscount, useItemDiscountRules, useUpdateItemDiscount } from '../queries';
import { itemDiscountSchema, type ItemDiscountForm } from '../schemas';
import { PromoDiscountType, type ItemDiscountRule } from '../types';
import { ActiveBadge, ItemSelect, PromoRowMenu, ScheduleFields } from './shared';
import { itemNameOf } from '../format';
import { DeletedRecordsPanel, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';
import { FormDialog } from '../../../components/forms/FormDialog';
import { QueryTable } from '../../../components/lists/QueryTable';
import type { DataColumn } from '../../../components/lists/DataTable';

const EMPTY: ItemDiscountForm = {
  name: '',
  itemId: '',
  discountType: PromoDiscountType.Percentage,
  discountValue: 0,
  startsAt: '',
  endsAt: '',
  isActive: true,
};

export function ItemDiscountPanel({ items, view }: { items: Item[]; view: StatusView }) {
  const { run, dialog: lifecycleDialog } = useLifecycle();
  const rules = useItemDiscountRules();
  const [dialog, setDialog] = useState<'new' | ItemDiscountRule | null>(null);

  const columns: DataColumn<ItemDiscountRule>[] = [
    { header: 'Discount', sortValue: (r) => r.name, cell: (r) => <span className="font-medium text-gray-900">{r.name}</span> },
    { header: 'Item', className: 'text-gray-600', sortValue: (r) => itemNameOf(items, r.itemId), cell: (r) => itemNameOf(items, r.itemId) },
    { header: 'Discount value', className: 'text-gray-600', sortValue: (r) => r.discountValue, cell: (r) => describeDiscount(r.discountType, r.discountValue) },
    { header: 'Runs', className: 'text-gray-600', sortValue: (r) => r.startsAt, cell: (r) => describeWindow(r.startsAt, r.endsAt) },
    { header: 'Status', sortValue: (r) => (r.isActive ? 'Active' : 'Off'), cell: (r) => <ActiveBadge active={r.isActive} /> },
    {
      header: '',
      className: 'text-right',
      cell: (r) => (
        <div className="flex justify-end">
          <PromoRowMenu subject={r.name} active={r.isActive} onEdit={() => setDialog(r)} onLifecycle={(action) => run({ kind: 'ItemDiscountPromo', id: r.id, name: r.name }, action)} />
        </div>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <div>
        <PrimaryButton type="button" onClick={() => setDialog('new')}>
          Add item discount
        </PrimaryButton>
      </div>
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="ItemDiscountPromo" noun="item discounts" />
      ) : (
      <QueryTable
        query={rules}
        label="Item discounts"
        columns={columns}
        getId={(r) => r.id}
        transform={(rows) => rows.filter((r) => (view === 'active' ? r.isActive : !r.isActive))}
        emptyMessage={view === 'active' ? "No item discounts yet." : "No inactive item discounts."}
      />
      )}
      {lifecycleDialog}
      {dialog && <ItemDiscountDialog rule={dialog === 'new' ? null : dialog} items={items} onClose={() => setDialog(null)} />}
    </div>
  );
}

function ItemDiscountDialog({ rule, items, onClose }: { rule: ItemDiscountRule | null; items: Item[]; onClose: () => void }) {
  const create = useCreateItemDiscount();
  const update = useUpdateItemDiscount();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ItemDiscountForm>({
    resolver: zodResolver(itemDiscountSchema),
    defaultValues: rule
      ? {
          name: rule.name,
          itemId: rule.itemId,
          discountType: rule.discountType,
          discountValue: rule.discountValue,
          startsAt: isoToLocalInput(rule.startsAt),
          endsAt: isoToLocalInput(rule.endsAt),
          isActive: rule.isActive,
        }
      : EMPTY,
  });

  const submit = handleSubmit(async (v) => {
    const body = {
      name: v.name,
      itemId: v.itemId,
      discountType: v.discountType,
      discountValue: v.discountValue,
      startsAt: localInputToIso(v.startsAt),
      endsAt: localInputToIso(v.endsAt),
    };
    if (rule) await update.mutateAsync({ id: rule.id, body: { ...body, isActive: v.isActive } });
    else await create.mutateAsync(body);
    toast.success(rule ? 'Discount updated' : 'Discount added');
    onClose();
  });

  return (
    <FormDialog
      title={rule ? 'Edit item discount' : 'Add item discount'}
      wide
      submitLabel={rule ? 'Save changes' : 'Add'}
      busy={create.isPending || update.isPending}
      onSubmit={submit}
      onClose={onClose}
    >
      <FormField label="Name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      <ItemSelect label="Item" name="itemId" register={register} errors={errors} items={items} />
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Discount type">
          <select {...register('discountType', { valueAsNumber: true })} className={controlClass}>
            <option value={PromoDiscountType.Percentage}>Percentage off</option>
            <option value={PromoDiscountType.FixedAmount}>Amount off (PHP)</option>
            <option value={PromoDiscountType.FixedPrice}>Set price (PHP)</option>
          </select>
        </FormField>
        <FormField label="Value" error={errors.discountValue?.message}>
          <input type="number" inputMode="decimal" step="0.01" {...register('discountValue', { valueAsNumber: true })} className={controlClass} />
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
