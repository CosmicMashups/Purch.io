import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { toast } from '../../../components/feedback/toastStore';
import { formatDateTime, localInputToIso } from '../../../lib/dates';
import { describeDiscount } from '../format';
import { useCreatePromoCode, usePromoCodes } from '../queries';
import { promoCodeSchema, type PromoCodeForm } from '../schemas';
import { PromoDiscountType, type PromoCode } from '../types';
import { ActiveBadge, PromoRowMenu, ScheduleFields } from './shared';
import { DeletedRecordsPanel, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';
import { FormDialog } from '../../../components/forms/FormDialog';
import { QueryTable } from '../../../components/lists/QueryTable';
import type { DataColumn } from '../../../components/lists/DataTable';

const EMPTY: PromoCodeForm = { code: '', discountType: PromoDiscountType.Percentage, discountValue: 0, expiresAt: '' };

/** The API has no update endpoint for promo codes, so this panel lists, creates, switches on or off, and deletes. */
export function PromoCodePanel({ view }: { view: StatusView }) {
  const { run, dialog: lifecycleDialog } = useLifecycle();
  const codes = usePromoCodes();
  const [adding, setAdding] = useState(false);

  const columns: DataColumn<PromoCode>[] = [
    { header: 'Code', sortValue: (r) => r.code, cell: (r) => <span className="font-mono font-semibold tracking-wide text-gray-900">{r.code}</span> },
    { header: 'Discount', className: 'text-gray-600', sortValue: (r) => r.discountValue, cell: (r) => describeDiscount(r.discountType, r.discountValue) },
    {
      header: 'Expires',
      className: 'text-gray-600',
      sortValue: (r) => (r.expiresAt ? new Date(r.expiresAt) : null),
      cell: (r) => (r.expiresAt ? formatDateTime(r.expiresAt) : 'Never expires'),
    },
    { header: 'Status', sortValue: (r) => (r.isActive ? 'Active' : 'Off'), cell: (r) => <ActiveBadge active={r.isActive} /> },
    {
      header: '',
      className: 'text-right',
      cell: (r) => (
        <div className="flex justify-end">
          <PromoRowMenu subject={r.code} active={r.isActive} onLifecycle={(action) => run({ kind: 'PromoCode', id: r.id, name: r.code }, action)} />
        </div>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <div>
        <PrimaryButton type="button" onClick={() => setAdding(true)}>
          Add promo code
        </PrimaryButton>
      </div>
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="PromoCode" noun="promo codes" />
      ) : (
      <QueryTable
        query={codes}
        label="Promo codes"
        columns={columns}
        getId={(r) => r.id}
        transform={(rows) => rows.filter((c) => (view === 'active' ? c.isActive : !c.isActive))}
        emptyMessage={view === 'active' ? 'No promo codes yet.' : 'No inactive promo codes.'}
      />
      )}
      {lifecycleDialog}
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
