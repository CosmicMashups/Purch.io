import { useForm, useWatch } from 'react-hook-form';
import { ITEM_SAMPLES } from '../../../lib/images';
import { zodResolver } from '@hookform/resolvers/zod';
import type { z } from 'zod';
import { categorySchema } from '../schemas';
import { useCategories, useCreateCategory, useUpdateCategory } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { FormDialog } from '../../../components/forms/FormDialog';
import { PrimaryButton } from '../../../components/forms/FormField';
import { ImageUploadField } from '../../../components/forms/ImageUploadField';
import { PurchImage } from '../../../components/brand/PurchImage';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonList } from '../../../components/Skeleton';
import { useState } from 'react';
import type { Category } from '../types';

type FormValues = z.infer<typeof categorySchema>;

export function CategoriesPage() {
  const { data: categories, isLoading, isError, error, refetch } = useCategories();
  // null: closed; 'new': adding; otherwise the category being edited.
  const [dialog, setDialog] = useState<'new' | Category | null>(null);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-xl font-semibold text-gray-900">Categories</h1>
        <PrimaryButton type="button" onClick={() => setDialog('new')}>
          Add Category
        </PrimaryButton>
      </div>

      {isLoading && <SkeletonList />}
      {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
      {!isLoading && !isError && (categories ?? []).length === 0 && <EmptyState title="No categories yet" />}
      {!isError && (categories ?? []).length > 0 && (
        <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200 bg-white">
          {[...categories!].sort((a, b) => a.sortOrder - b.sortOrder).map((c) => (
            <li key={c.id} className="flex items-center justify-between px-4 py-2 text-sm">
              <span className="flex items-center gap-3">
                {c.imageUrl ? (
                  <PurchImage src={c.imageUrl} alt="" className="h-8 w-8 rounded object-cover" errorNode={<span className="h-8 w-8 rounded bg-gray-100" />} />
                ) : (
                  <span className="h-8 w-8 rounded bg-gray-100" />
                )}
                <span>
                  {c.name} <span className="text-gray-400">#{c.sortOrder}</span>
                </span>
              </span>
              <button onClick={() => setDialog(c)} className="text-gray-500 hover:text-gray-900 hover:underline">
                Edit
              </button>
            </li>
          ))}
        </ul>
      )}
      {dialog && <CategoryDialog category={dialog === 'new' ? null : dialog} onClose={() => setDialog(null)} />}
    </div>
  );
}

function CategoryDialog({ category, onClose }: { category: Category | null; onClose: () => void }) {
  const createCategory = useCreateCategory();
  const updateCategory = useUpdateCategory();
  const {
    register,
    handleSubmit,
    control,
    setValue,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: category
      ? { name: category.name, sortOrder: category.sortOrder, imageUrl: category.imageUrl }
      : { name: '', sortOrder: 0, imageUrl: null },
  });
  const imageUrl = useWatch({ control, name: 'imageUrl' });

  async function onSubmit(values: FormValues) {
    const body = { name: values.name, sortOrder: values.sortOrder, imageUrl: values.imageUrl ?? null };
    if (category) {
      await updateCategory.mutateAsync({ categoryId: category.id, body });
    } else {
      await createCategory.mutateAsync(body);
    }
    onClose();
  }

  return (
    <FormDialog
      title={category ? 'Edit category' : 'Add category'}
      busy={createCategory.isPending || updateCategory.isPending}
      submitLabel={category ? 'Save Changes' : 'Add Category'}
      onSubmit={handleSubmit(onSubmit)}
      onClose={onClose}
    >
      <Field label="Name" error={errors.name?.message}>
        <input {...register('name')} className={inputClass} />
      </Field>
      <Field label="Sort order" error={errors.sortOrder?.message}>
        <input type="number" {...register('sortOrder')} className={inputClass} />
      </Field>
      <input type="hidden" {...register('imageUrl')} />
      <ImageUploadField label="Image" allowUrl samples={ITEM_SAMPLES} value={imageUrl ?? null} onChange={(url) => setValue('imageUrl', url, { shouldDirty: true })} />
    </FormDialog>
  );
}
