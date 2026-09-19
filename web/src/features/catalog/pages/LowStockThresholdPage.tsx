import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useParams } from 'react-router-dom';
import type { z } from 'zod';
import { lowStockThresholdSchema } from '../schemas';
import { useItems, useUpdateLowStockThreshold } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { ItemSubPageHeader } from './ItemSubPageHeader';
import { ApiError } from '../../../lib/apiError';
import { useState } from 'react';

type FormValues = z.infer<typeof lowStockThresholdSchema>;

export function LowStockThresholdPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const navigate = useNavigate();
  const { data: items } = useItems();
  const item = items?.find((i) => i.id === itemId);
  const updateLowStockThreshold = useUpdateLowStockThreshold(itemId!);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(lowStockThresholdSchema),
    values: item ? { threshold: item.lowStockThreshold } : undefined,
  });

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      await updateLowStockThreshold.mutateAsync({ threshold: values.threshold ?? null });
      navigate('/catalog/items');
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to update threshold');
    }
  }

  if (!item) return <p className="text-sm text-gray-500">Loading…</p>;

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Low-Stock Threshold" />
      <form onSubmit={handleSubmit(onSubmit)} className="flex max-w-sm flex-col gap-3">
        <Field label="Threshold (blank clears it)" error={errors.threshold?.message}>
          <input type="number" {...register('threshold')} className={inputClass} />
        </Field>
        {submitError && <p className="text-sm text-red-600">{submitError}</p>}
        <button
          type="submit"
          disabled={updateLowStockThreshold.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {updateLowStockThreshold.isPending ? 'Saving…' : 'Save'}
        </button>
      </form>
    </div>
  );
}
