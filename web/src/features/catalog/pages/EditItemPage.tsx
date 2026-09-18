import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useParams } from 'react-router-dom';
import type { z } from 'zod';
import { updateItemSchema } from '../schemas';
import { useCategories, useItems, useUpdateItem } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { StatusBadge } from '../../../components/StatusBadge';
import { pricingTypeLabels } from '../labels';
import { ApiError } from '../../../lib/apiError';
import { useState } from 'react';

type FormValues = z.infer<typeof updateItemSchema>;

export function EditItemPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const navigate = useNavigate();
  const { data: items } = useItems();
  const { data: categories } = useCategories();
  const updateItem = useUpdateItem();
  const [submitError, setSubmitError] = useState<string | null>(null);

  const item = items?.find((i) => i.id === itemId);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(updateItemSchema),
    values: item
      ? {
          name: item.name,
          sku: item.sku,
          barcode: item.barcode,
          categoryId: item.categoryId,
          basePrice: item.basePrice,
          imageUrl: item.imageUrl,
          isActive: item.isActive,
          departmentId: item.departmentId,
        }
      : undefined,
  });

  if (!item || !itemId) {
    return <p className="text-sm text-gray-500">Loading…</p>;
  }

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      await updateItem.mutateAsync({
        itemId: itemId!,
        body: {
          name: values.name,
          sku: values.sku ?? null,
          barcode: values.barcode ?? null,
          categoryId: values.categoryId ?? null,
          basePrice: values.basePrice,
          imageUrl: values.imageUrl ?? null,
          isActive: values.isActive,
          departmentId: values.departmentId ?? null,
        },
      });
      navigate('/catalog/items');
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to update item');
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex max-w-xl flex-col gap-4">
      <div className="flex items-center gap-2">
        <h1 className="text-xl font-semibold text-gray-900">Edit Item</h1>
        <StatusBadge label={pricingTypeLabels[item.pricingType]} />
      </div>
      <p className="text-xs text-gray-500">Pricing type is fixed at creation and cannot be changed.</p>

      <Field label="Name" error={errors.name?.message}>
        <input {...register('name')} className={inputClass} />
      </Field>

      <Field label="SKU">
        <input {...register('sku')} className={inputClass} />
      </Field>

      <Field label="Barcode">
        <input {...register('barcode')} className={inputClass} />
      </Field>

      <Field label="Category">
        <select {...register('categoryId')} className={inputClass}>
          <option value="">None (Uncategorized)</option>
          {(categories ?? []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </Field>

      <Field label="Base Price" error={errors.basePrice?.message}>
        <input type="number" step="0.01" {...register('basePrice')} className={inputClass} />
      </Field>

      <Field label="Image URL">
        <input {...register('imageUrl')} className={inputClass} />
      </Field>

      <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
        <input type="checkbox" {...register('isActive')} />
        Active
      </label>

      {submitError && <p className="text-sm text-red-600">{submitError}</p>}

      <button
        type="submit"
        disabled={updateItem.isPending}
        className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
      >
        {updateItem.isPending ? 'Saving…' : 'Save Changes'}
      </button>
    </form>
  );
}
