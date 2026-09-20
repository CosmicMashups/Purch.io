import { useForm } from 'react-hook-form';
import { useParams } from 'react-router-dom';
import { useCreateVariant, useVariants } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { EmptyState } from '../../../components/EmptyState';
import { ItemSubPageHeader } from './ItemSubPageHeader';
import { useState } from 'react';
import { ApiError } from '../../../lib/apiError';

interface FormValues {
  attributeKey: string;
  attributeValue: string;
  sku: string;
  priceOverride: string;
  imageUrl: string;
}

export function VariantsPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const { data: variants, isLoading } = useVariants(itemId!);
  const createVariant = useCreateVariant(itemId!);
  const [error, setError] = useState<string | null>(null);
  const { register, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: { attributeKey: '', attributeValue: '', sku: '', priceOverride: '', imageUrl: '' },
  });

  async function onSubmit(values: FormValues) {
    setError(null);
    if (!values.attributeKey.trim() || !values.attributeValue.trim()) {
      setError('Add at least one attribute (e.g. Size = Large)');
      return;
    }
    try {
      await createVariant.mutateAsync({
        attributes: { [values.attributeKey.trim()]: values.attributeValue.trim() },
        sku: values.sku.trim() || null,
        priceOverride: values.priceOverride ? Number(values.priceOverride) : null,
        imageUrl: values.imageUrl.trim() || null,
      });
      reset();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add variant');
    }
  }

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Variants" />

      <form onSubmit={handleSubmit(onSubmit)} className="mb-6 flex max-w-md flex-col gap-3">
        <div className="flex gap-3">
          <Field label="Attribute name">
            <input {...register('attributeKey')} className={inputClass} placeholder="Size" />
          </Field>
          <Field label="Attribute value">
            <input {...register('attributeValue')} className={inputClass} placeholder="Large" />
          </Field>
        </div>
        <Field label="SKU">
          <input {...register('sku')} className={inputClass} />
        </Field>
        <Field label="Price override">
          <input type="number" step="0.01" {...register('priceOverride')} className={inputClass} />
        </Field>
        <Field label="Image URL">
          <input {...register('imageUrl')} className={inputClass} />
        </Field>
        {error && <p className="text-sm text-red-600">{error}</p>}
        <button
          type="submit"
          disabled={createVariant.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {createVariant.isPending ? 'Saving…' : 'Add Variant'}
        </button>
      </form>

      {isLoading && <p className="text-sm text-gray-500">Loading…</p>}
      {!isLoading && (variants ?? []).length === 0 && <EmptyState title="No variants yet" />}
      {(variants ?? []).length > 0 && (
        <table className="min-w-full divide-y divide-gray-200 rounded-lg border border-gray-200 bg-white text-sm">
          <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
            <tr>
              <th className="px-4 py-2">Attributes</th>
              <th className="px-4 py-2">SKU</th>
              <th className="px-4 py-2">Price Override</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {variants!.map((v) => (
              <tr key={v.id}>
                <td className="px-4 py-2">
                  {Object.entries(v.attributes)
                    .map(([k, val]) => `${k}: ${val}`)
                    .join(', ')}
                </td>
                <td className="px-4 py-2">{v.sku ?? '—'}</td>
                <td className="px-4 py-2">{v.priceOverride != null ? `₱${v.priceOverride.toFixed(2)}` : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
