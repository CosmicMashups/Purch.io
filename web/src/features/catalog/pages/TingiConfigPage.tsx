import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useParams } from 'react-router-dom';
import type { z } from 'zod';
import { tingiConfigSchema } from '../schemas';
import { useItems, useUpdateTingiConfig } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { ItemSubPageHeader } from './ItemSubPageHeader';
import { TingiMode } from '../types';
import { tingiModeLabels } from '../labels';
import { ApiError } from '../../../lib/apiError';
import { useState } from 'react';

type FormValues = z.infer<typeof tingiConfigSchema>;

export function TingiConfigPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const navigate = useNavigate();
  const { data: items } = useItems();
  const item = items?.find((i) => i.id === itemId);
  const updateTingiConfig = useUpdateTingiConfig(itemId!);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(tingiConfigSchema),
    values: item
      ? {
          tingiMode: item.tingiMode,
          packagedSize: item.packagedSize,
          tingiIncrementStep: item.tingiIncrementStep,
          allowedSizesCsv: item.tingiAllowedSizes.join(', '),
        }
      : undefined,
  });

  const tingiMode = Number(watch('tingiMode'));

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    const allowedSizes = (values.allowedSizesCsv ?? '')
      .split(',')
      .map((s) => Number(s.trim()))
      .filter((n) => Number.isFinite(n) && n > 0);

    try {
      await updateTingiConfig.mutateAsync({
        tingiMode: values.tingiMode,
        packagedSize: values.packagedSize ?? null,
        tingiIncrementStep: values.tingiIncrementStep ?? null,
        allowedSizes,
      });
      navigate('/catalog/items');
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to update tingi config');
    }
  }

  if (!item) return <p className="text-sm text-gray-500">Loading…</p>;

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Tingi Config" />
      <form onSubmit={handleSubmit(onSubmit)} className="flex max-w-md flex-col gap-3">
        <Field label="Mode">
          <select {...register('tingiMode', { valueAsNumber: true })} className={inputClass}>
            {Object.entries(tingiModeLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Pack size" error={errors.packagedSize?.message}>
          <input type="number" step="0.01" {...register('packagedSize')} className={inputClass} />
        </Field>

        {tingiMode === TingiMode.Increment && (
          <Field label="Increment step" error={errors.tingiIncrementStep?.message}>
            <input type="number" step="0.01" {...register('tingiIncrementStep')} className={inputClass} />
          </Field>
        )}

        {tingiMode === TingiMode.Fixed && (
          <Field label="Allowed sizes (comma-separated)">
            <input {...register('allowedSizesCsv')} className={inputClass} />
          </Field>
        )}

        {submitError && <p className="text-sm text-red-600">{submitError}</p>}

        <button
          type="submit"
          disabled={updateTingiConfig.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {updateTingiConfig.isPending ? 'Saving…' : 'Save'}
        </button>
      </form>
    </div>
  );
}
