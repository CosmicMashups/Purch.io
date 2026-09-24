import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { toast } from '../../../components/feedback/toastStore';
import { describeWindow, isoToLocalInput, localInputToIso } from '../../../lib/dates';
import type { Item } from '../../catalog/types';
import { describeDiscount } from '../format';
import { useCreateItemDiscount, useItemDiscountRules, useUpdateItemDiscount } from '../queries';
import { itemDiscountSchema, type ItemDiscountForm } from '../schemas';
import { PromoDiscountType, type ItemDiscountRule } from '../types';
import { ActiveBadge, ItemSelect, ScheduleFields } from './shared';
import { itemNameOf } from '../format';
import { EditorCard } from '../../../components/forms/EditorCard';
import { ListCard as RuleCard, QueryList as RuleList } from '../../../components/lists/QueryList';

const EMPTY: ItemDiscountForm = {
  name: '',
  itemId: '',
  discountType: PromoDiscountType.Percentage,
  discountValue: 0,
  startsAt: '',
  endsAt: '',
  isActive: true,
};

export function ItemDiscountPanel({ items }: { items: Item[] }) {
  const rules = useItemDiscountRules();
  const create = useCreateItemDiscount();
  const update = useUpdateItemDiscount();
  const [editing, setEditing] = useState<ItemDiscountRule | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ItemDiscountForm>({ resolver: zodResolver(itemDiscountSchema), defaultValues: EMPTY });

  function startEdit(rule: ItemDiscountRule) {
    setEditing(rule);
    reset({
      name: rule.name,
      itemId: rule.itemId,
      discountType: rule.discountType,
      discountValue: rule.discountValue,
      startsAt: isoToLocalInput(rule.startsAt),
      endsAt: isoToLocalInput(rule.endsAt),
      isActive: rule.isActive,
    });
  }

  function clear() {
    setEditing(null);
    reset(EMPTY);
  }

  const submit = handleSubmit(async (v) => {
    const body = {
      name: v.name,
      itemId: v.itemId,
      discountType: v.discountType,
      discountValue: v.discountValue,
      startsAt: localInputToIso(v.startsAt),
      endsAt: localInputToIso(v.endsAt),
    };
    if (editing) await update.mutateAsync({ id: editing.id, body: { ...body, isActive: v.isActive } });
    else await create.mutateAsync(body);
    toast.success(editing ? 'Discount updated' : 'Discount added');
    clear();
  });

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
      <RuleList
        query={rules}
        emptyMessage="No item discounts yet."
        renderRow={(rule) => (
          <RuleCard key={rule.id}>
            <div className="min-w-0">
              <p className="text-base font-semibold">{rule.name}</p>
              <p className="text-base">
                {itemNameOf(items, rule.itemId)}: {describeDiscount(rule.discountType, rule.discountValue)}
              </p>
              <p className="text-sm text-ink-soft">{describeWindow(rule.startsAt, rule.endsAt)}</p>
              <button type="button" onClick={() => startEdit(rule)} className="mt-2 h-12 text-base font-semibold text-brand-strong underline">
                Edit
              </button>
            </div>
            <ActiveBadge active={rule.isActive} />
          </RuleCard>
        )}
      />

      <EditorCard title="item discount" editing={!!editing} busy={create.isPending || update.isPending} onSubmit={submit} onCancel={clear}>
        <FormField label="Name" error={errors.name?.message}>
          <input {...register('name')} className={controlClass} />
        </FormField>
        <ItemSelect label="Item" name="itemId" register={register} errors={errors} items={items} />
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
        <ScheduleFields register={register} errors={errors} startName="startsAt" endName="endsAt" />
        {editing && (
          <label className="flex h-12 items-center gap-3 text-base font-semibold">
            <input type="checkbox" {...register('isActive')} className="size-6 accent-brand" />
            Active
          </label>
        )}
      </EditorCard>
    </div>
  );
}
