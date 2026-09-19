import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useParams } from 'react-router-dom';
import type { z } from 'zod';
import { serviceDurationSchema } from '../schemas';
import { useItems, useUpdateServiceDuration } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { ItemSubPageHeader } from './ItemSubPageHeader';
import { ApiError } from '../../../lib/apiError';
import { useState } from 'react';

type FormValues = z.infer<typeof serviceDurationSchema>;

export function ServiceDurationPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const navigate = useNavigate();
  const { data: items } = useItems();
  const item = items?.find((i) => i.id === itemId);
  const updateServiceDuration = useUpdateServiceDuration(itemId!);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(serviceDurationSchema),
    values: item ? { durationMinutes: item.serviceDurationMinutes ?? 0 } : undefined,
  });

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      await updateServiceDuration.mutateAsync({ durationMinutes: values.durationMinutes });
      navigate('/catalog/items');
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to update service duration');
    }
  }

  if (!item) return <p className="text-sm text-gray-500">Loading…</p>;

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Service Duration" />
      <form onSubmit={handleSubmit(onSubmit)} className="flex max-w-sm flex-col gap-3">
        <Field label="Duration (minutes)" error={errors.durationMinutes?.message}>
          <input type="number" {...register('durationMinutes')} className={inputClass} />
        </Field>
        {submitError && <p className="text-sm text-red-600">{submitError}</p>}
        <button
          type="submit"
          disabled={updateServiceDuration.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {updateServiceDuration.isPending ? 'Saving…' : 'Save'}
        </button>
      </form>
    </div>
  );
}
