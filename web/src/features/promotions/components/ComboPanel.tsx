import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { toast } from '../../../components/feedback/toastStore';
import { describeWindow, isoToLocalInput, localInputToIso } from '../../../lib/dates';
import type { Item } from '../../catalog/types';
import { formatPeso } from '../../dashboard/format';
import { useComboRules, useCreateCombo, useUpdateCombo } from '../queries';
import { comboSchema, type ComboForm } from '../schemas';
import type { ComboRule } from '../types';
import { ActiveBadge, ItemSelect, PromoRowMenu, ScheduleFields } from './shared';
import { itemNameOf } from '../format';
import { DeletedRecordsPanel, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';
import { FormDialog } from '../../../components/forms/FormDialog';
import { ListCard as RuleCard, QueryList as RuleList } from '../../../components/lists/QueryList';

const EMPTY: ComboForm = { name: '', itemAId: '', itemBId: '', comboPrice: 0, startsAt: '', endsAt: '', isActive: true };

export function ComboPanel({ items, view }: { items: Item[]; view: StatusView }) {
  const { run, dialog: lifecycleDialog } = useLifecycle();
  const rules = useComboRules();
  const [dialog, setDialog] = useState<'new' | ComboRule | null>(null);

  return (
    <div className="flex flex-col gap-4">
      <div>
        <PrimaryButton type="button" onClick={() => setDialog('new')}>
          Add combo deal
        </PrimaryButton>
      </div>
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="ComboPromo" noun="combo deals" />
      ) : (
      <RuleList
        columns
        query={rules}
        transform={(rows) => rows.filter((r) => (view === 'active' ? r.isActive : !r.isActive))}
        emptyMessage={view === 'active' ? "No combo deals yet." : "No inactive combo deals."}
        renderRow={(rule) => (
          <RuleCard key={rule.id}>
            <div className="min-w-0">
              <p className="text-base font-semibold">{rule.name}</p>
              <p className="text-base">
                {itemNameOf(items, rule.itemAId)} + {itemNameOf(items, rule.itemBId)} for {formatPeso(rule.comboPrice)}
              </p>
              <p className="text-sm text-ink-soft">{describeWindow(rule.startsAt, rule.endsAt)}</p>
            </div>
            <div className="flex shrink-0 items-center gap-1">
              <ActiveBadge active={rule.isActive} />
              <PromoRowMenu subject={rule.name} active={rule.isActive} onEdit={() => setDialog(rule)} onLifecycle={(action) => run({ kind: 'ComboPromo', id: rule.id, name: rule.name }, action)} />
            </div>
          </RuleCard>
        )}
      />
      )}
      {lifecycleDialog}
      {dialog && <ComboDialog rule={dialog === 'new' ? null : dialog} items={items} onClose={() => setDialog(null)} />}
    </div>
  );
}

function ComboDialog({ rule, items, onClose }: { rule: ComboRule | null; items: Item[]; onClose: () => void }) {
  const create = useCreateCombo();
  const update = useUpdateCombo();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ComboForm>({
    resolver: zodResolver(comboSchema),
    defaultValues: rule
      ? {
          name: rule.name,
          itemAId: rule.itemAId,
          itemBId: rule.itemBId,
          comboPrice: rule.comboPrice,
          startsAt: isoToLocalInput(rule.startsAt),
          endsAt: isoToLocalInput(rule.endsAt),
          isActive: rule.isActive,
        }
      : EMPTY,
  });

  const submit = handleSubmit(async (v) => {
    const body = {
      name: v.name,
      itemAId: v.itemAId,
      itemBId: v.itemBId,
      comboPrice: v.comboPrice,
      startsAt: localInputToIso(v.startsAt),
      endsAt: localInputToIso(v.endsAt),
    };
    if (rule) await update.mutateAsync({ id: rule.id, body: { ...body, isActive: v.isActive } });
    else await create.mutateAsync(body);
    toast.success(rule ? 'Combo updated' : 'Combo added');
    onClose();
  });

  return (
    <FormDialog
      title={rule ? 'Edit combo deal' : 'Add combo deal'}
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
        <ItemSelect label="First item" name="itemAId" register={register} errors={errors} items={items} />
        <ItemSelect label="Second item" name="itemBId" register={register} errors={errors} items={items} />
      </div>
      <FormField label="Combo price (PHP)" error={errors.comboPrice?.message}>
        <input type="number" inputMode="decimal" step="0.01" {...register('comboPrice', { valueAsNumber: true })} className={controlClass} />
      </FormField>
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
