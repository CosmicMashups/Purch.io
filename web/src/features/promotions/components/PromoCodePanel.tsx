import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { toast } from '../../../components/feedback/toastStore';
import { formatDateTime, localInputToIso } from '../../../lib/dates';
import { describeDiscount } from '../format';
import { useCreatePromoCode, usePromoCodes } from '../queries';
import { promoCodeSchema, type PromoCodeForm } from '../schemas';
import { PromoDiscountType } from '../types';
import { ActiveBadge, ScheduleFields } from './shared';
import { FormDialog } from '../../../components/forms/FormDialog';
import { ListCard as RuleCard, QueryList as RuleList } from '../../../components/lists/QueryList';

const EMPTY: PromoCodeForm = { code: '', discountType: PromoDiscountType.Percentage, discountValue: 0, expiresAt: '' };

/** The API has no update endpoint for promo codes, so this panel lists and creates only. */
export function PromoCodePanel() {
  const codes = usePromoCodes();
  const [adding, setAdding] = useState(false);

  return (
    <div className="flex flex-col gap-4">
      <div>
        <PrimaryButton type="button" onClick={() => setAdding(true)}>
          Add promo code
        </PrimaryButton>
      </div>
      <RuleList
        columns
        query={codes}
        emptyMessage="No promo codes yet."
        renderRow={(code) => (
          <RuleCard key={code.id}>
            <div className="min-w-0">
              <p className="font-mono text-lg font-semibold tracking-wide">{code.code}</p>
              <p className="text-base">{describeDiscount(code.discountType, code.discountValue)}</p>
              <p className="text-sm text-ink-soft">{code.expiresAt ? `Expires ${formatDateTime(code.expiresAt)}` : 'Never expires'}</p>
            </div>
            <ActiveBadge active={code.isActive} />
          </RuleCard>
        )}
      />
      {adding && <PromoCodeDialog onClose={() => setAdding(false)} />}
    </div>
  );
}

function PromoCodeDialog({ onClose }: { onClose: () => void }) {
  const create = useCreatePromoCode();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<PromoCodeForm>({ resolver: zodResolver(promoCodeSchema), defaultValues: EMPTY });

  const submit = handleSubmit(async (v) => {
    await create.mutateAsync({
      code: v.code,
      discountType: v.discountType,
      discountValue: v.discountValue,
      expiresAt: localInputToIso(v.expiresAt),
    });
    toast.success('Promo code added');
    onClose();
  });

  return (
    <FormDialog title="Add promo code" submitLabel="Add" busy={create.isPending} onSubmit={submit} onClose={onClose}>
      <FormField label="Code" error={errors.code?.message}>
        <input autoCapitalize="characters" autoComplete="off" {...register('code')} className={controlClass} />
      </FormField>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Discount type">
          <select {...register('discountType', { valueAsNumber: true })} className={controlClass}>
            <option value={PromoDiscountType.Percentage}>Percentage off</option>
            <option value={PromoDiscountType.FixedAmount}>Amount off (PHP)</option>
          </select>
        </FormField>
        <FormField label="Value" error={errors.discountValue?.message}>
          <input type="number" inputMode="decimal" step="0.01" {...register('discountValue', { valueAsNumber: true })} className={controlClass} />
        </FormField>
      </div>
      <ScheduleFields register={register} errors={errors} endName="expiresAt" endLabel="Expires" />
    </FormDialog>
  );
}
