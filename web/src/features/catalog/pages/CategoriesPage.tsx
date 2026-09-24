import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { z } from 'zod';
import { categorySchema } from '../schemas';
import { useCategories, useCreateCategory, useUpdateCategory } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { ImageUploadField } from '../../../components/forms/ImageUploadField';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonList } from '../../../components/Skeleton';
import { useState } from 'react';
import type { Category } from '../types';

type FormValues = z.infer<typeof categorySchema>;

export function CategoriesPage() {
  const { data: categories, isLoading, isError, error, refetch } = useCategories();
  const createCategory = useCreateCategory();
  const updateCategory = useUpdateCategory();
  const [editing, setEditing] = useState<Category | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    control,
    setValue,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: '', sortOrder: 0, imageUrl: null },
  });
  const imageUrl = useWatch({ control, name: 'imageUrl' });

  function startEdit(category: Category) {
    setEditing(category);
    reset({ name: category.name, sortOrder: category.sortOrder, imageUrl: category.imageUrl });
  }

  async function onSubmit(values: FormValues) {
    const body = { name: values.name, sortOrder: values.sortOrder, imageUrl: values.imageUrl ?? null };
    if (editing) {
      await updateCategory.mutateAsync({ categoryId: editing.id, body });
    } else {
      await createCategory.mutateAsync(body);
    }
    setEditing(null);
    reset({ name: '', sortOrder: 0, imageUrl: null });
  }

  const isSaving = createCategory.isPending || updateCategory.isPending;

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-xl font-semibold text-gray-900">Categories</h1>

      <form onSubmit={handleSubmit(onSubmit)} className="flex max-w-md flex-col gap-3">
        <Field label="Name" error={errors.name?.message}>
          <input {...register('name')} className={inputClass} />
        </Field>
        <Field label="Sort order" error={errors.sortOrder?.message}>
          <input type="number" {...register('sortOrder')} className={inputClass} />
        </Field>
        <input type="hidden" {...register('imageUrl')} />
        <ImageUploadField label="Image" value={imageUrl ?? null} onChange={(url) => setValue('imageUrl', url, { shouldDirty: true })} />
        <div className="flex gap-2">
          <button
            type="submit"
            disabled={isSaving}
            className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {isSaving ? 'Saving…' : editing ? 'Save Changes' : 'Add Category'}
          </button>
          {editing && (
            <button
              type="button"
              onClick={() => {
                setEditing(null);
                reset({ name: '', sortOrder: 0, imageUrl: null });
              }}
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-100"
            >
              Cancel
            </button>
          )}
        </div>
      </form>

      {isLoading && <SkeletonList />}
      {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
      {!isLoading && !isError && (categories ?? []).length === 0 && <EmptyState title="No categories yet" />}
      {!isError && (categories ?? []).length > 0 && (
        <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200 bg-white">
          {[...categories!].sort((a, b) => a.sortOrder - b.sortOrder).map((c) => (
            <li key={c.id} className="flex items-center justify-between px-4 py-2 text-sm">
              <span>
                {c.name} <span className="text-gray-400">#{c.sortOrder}</span>
              </span>
              <button onClick={() => startEdit(c)} className="text-gray-500 hover:text-gray-900 hover:underline">
                Edit
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
