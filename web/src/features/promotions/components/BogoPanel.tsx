import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { toast } from '../../../components/feedback/toastStore';
import { describeWindow, isoToLocalInput, localInputToIso } from '../../../lib/dates';
import type { Item } from '../../catalog/types';
import { useBogoRules, useCreateBogo, useUpdateBogo } from '../queries';
import { bogoSchema, type BogoForm } from '../schemas';
import type { BogoRule } from '../types';
import { ActiveBadge, ItemSelect, ScheduleFields } from './shared';
import { itemNameOf } from '../format';
import { EditorCard } from '../../../components/forms/EditorCard';
import { ListCard as RuleCard, QueryList as RuleList } from '../../../components/lists/QueryList';

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

export function BogoPanel({ items }: { items: Item[] }) {
  const rules = useBogoRules();
  const create = useCreateBogo();
  const update = useUpdateBogo();
  const [editing, setEditing] = useState<BogoRule | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<BogoForm>({ resolver: zodResolver(bogoSchema), defaultValues: EMPTY });

  function startEdit(rule: BogoRule) {
    setEditing(rule);
    reset({
      name: rule.name,
      triggerItemId: rule.triggerItemId,
      triggerQuantity: rule.triggerQuantity,
      freeItemId: rule.freeItemId,
      freeQuantity: rule.freeQuantity,
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
      triggerItemId: v.triggerItemId,
      triggerQuantity: v.triggerQuantity,
      freeItemId: v.freeItemId,
      freeQuantity: v.freeQuantity,
      startsAt: localInputToIso(v.startsAt),
      endsAt: localInputToIso(v.endsAt),
    };
    if (editing) await update.mutateAsync({ id: editing.id, body: { ...body, isActive: v.isActive } });
    else await create.mutateAsync(body);
    toast.success(editing ? 'Promotion updated' : 'Promotion added');
    clear();
  });

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
      <RuleList
        query={rules}
        emptyMessage="No Buy 1 Take 1 promotions yet."
        renderRow={(rule) => (
          <RuleCard key={rule.id}>
            <div className="min-w-0">
              <p className="text-base font-semibold">{rule.name}</p>
              <p className="text-base">
                Buy {rule.triggerQuantity} {itemNameOf(items, rule.triggerItemId)}, get {rule.freeQuantity} {itemNameOf(items, rule.freeItemId)} free
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

      <EditorCard title="promotion" editing={!!editing} busy={create.isPending || update.isPending} onSubmit={submit} onCancel={clear}>
        <FormField label="Name" error={errors.name?.message}>
          <input {...register('name')} className={controlClass} />
        </FormField>
        <ItemSelect label="Buy this item" name="triggerItemId" register={register} errors={errors} items={items} />
        <FormField label="Buy quantity" error={errors.triggerQuantity?.message}>
          <input type="number" inputMode="numeric" {...register('triggerQuantity', { valueAsNumber: true })} className={controlClass} />
        </FormField>
        <ItemSelect label="Get this item free" name="freeItemId" register={register} errors={errors} items={items} />
        <FormField label="Free quantity" error={errors.freeQuantity?.message}>
          <input type="number" inputMode="numeric" {...register('freeQuantity', { valueAsNumber: true })} className={controlClass} />
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
