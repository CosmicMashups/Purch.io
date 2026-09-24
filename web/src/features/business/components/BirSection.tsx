import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { useUpdateBir } from '../../tenant/queries';
import type { TenantSettings } from '../../tenant/types';
import { birSchema, toBirBody, type BirForm } from '../settingsRules';

export function BirSection({ settings }: { settings: TenantSettings }) {
  const save = useUpdateBir();
  const [retentionError, setRetentionError] = useState<string | undefined>();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<BirForm>({
    resolver: zodResolver(birSchema),
    defaultValues: {
      tin: settings.tin ?? '',
      registeredBusinessName: settings.registeredBusinessName ?? '',
      registeredAddress: settings.registeredAddress ?? '',
      creditLedgerRetentionDays: settings.creditLedgerRetentionDays === null ? '' : String(settings.creditLedgerRetentionDays),
    },
  });

  const submit = handleSubmit((v) => {
    const built = toBirBody(v);
    if (!built.ok) return setRetentionError(built.message);
    setRetentionError(undefined);
    save.mutate(built.body, { onSuccess: () => toast.success('Business details saved') });
  });

  return (
    <section aria-labelledby="bir-heading" className="rounded-panel border border-line bg-surface p-6">
      <h2 id="bir-heading" className="text-xl font-bold">
        Business details
      </h2>
      <p className="mt-1 text-base text-ink-soft">The details your receipts and BIR readings are registered under.</p>

      <form onSubmit={submit} noValidate className="mt-5 flex flex-col gap-5">
        <FormField label="TIN" error={errors.tin?.message}>
          <input {...register('tin')} autoComplete="off" className={controlClass} />
        </FormField>
        <FormField label="Registered business name" error={errors.registeredBusinessName?.message}>
          <input {...register('registeredBusinessName')} className={controlClass} />
        </FormField>
        <FormField label="Registered address" error={errors.registeredAddress?.message}>
          <input {...register('registeredAddress')} className={controlClass} />
        </FormField>
        <FormField
          label="Keep customer credit records for (days)"
          hint="Leave blank to keep them until you remove them"
          error={retentionError ?? errors.creditLedgerRetentionDays?.message}
        >
          <input inputMode="numeric" {...register('creditLedgerRetentionDays')} className={controlClass} />
        </FormField>
        <div>
          <PrimaryButton type="submit" busy={save.isPending}>
            {save.isPending ? 'Saving...' : 'Save business details'}
          </PrimaryButton>
        </div>
      </form>
    </section>
  );
}
