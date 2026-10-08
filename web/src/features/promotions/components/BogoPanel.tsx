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
import { ActiveBadge, ItemSelect, ScheduleFields } from './shared';
import { itemNameOf } from '../format';
import { FormDialog } from '../../../components/forms/FormDialog';
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
  // null: closed; 'new': adding; otherwise the rule being edited.
  const [dialog, setDialog] = useState<'new' | BogoRule | null>(null);

  return (
    <div className="flex flex-col gap-4">
      <div>
        <PrimaryButton type="button" onClick={() => setDialog('new')}>
          Add promotion
        </PrimaryButton>
      </div>
      <RuleList
        columns
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
              <button type="button" onClick={() => setDialog(rule)} className="mt-2 h-12 text-base font-semibold text-brand-strong underline">
                Edit
              </button>
            </div>
            <ActiveBadge active={rule.isActive} />
          </RuleCard>
        )}
      />
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
