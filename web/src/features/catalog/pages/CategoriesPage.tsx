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
import { PageHeader } from '../../../components/PageHeader';
import { RowActionsMenu } from '../../../components/RowActionsMenu';
import { StatusBadge } from '../../../components/StatusBadge';
import { DeletedRecordsPanel, StatusFilter, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';

type FormValues = z.infer<typeof categorySchema>;

export function CategoriesPage() {
  const { data: categories, isLoading, isError, error, refetch } = useCategories();
  // null: closed; 'new': adding; otherwise the category being edited.
  const [dialog, setDialog] = useState<'new' | Category | null>(null);
  const [view, setView] = useState<StatusView>('active');
  const { run, dialog: lifecycleDialog } = useLifecycle();

  const shown = [...(categories ?? [])]
    .filter((c) => (view === 'active' ? c.isActive !== false : c.isActive === false))
    .sort((a, b) => a.sortOrder - b.sortOrder);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Categories"
        backTo={{ to: '/business', label: 'Business' }}
        action={
          <PrimaryButton type="button" onClick={() => setDialog('new')}>
            Add Category
          </PrimaryButton>
        }
      />
      <StatusFilter value={view} onChange={setView} />

      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="Category" noun="categories" />
      ) : (
        <>
          {isLoading && <SkeletonList />}
          {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
          {!isLoading && !isError && shown.length === 0 && <EmptyState title={view === 'active' ? 'No categories yet' : 'No inactive categories'} />}
          {!isError && shown.length > 0 && (
            <ul className="divide-y divide-line rounded-panel border border-line bg-surface">
              {shown.map((c) => (
                <li key={c.id} className="flex items-center justify-between gap-3 px-4 py-2">
                  <button type="button" onClick={() => setDialog(c)} aria-label={`Edit ${c.name}`} className="flex min-h-12 min-w-0 flex-1 items-center gap-3 text-left">
                    {c.imageUrl ? (
                      <PurchImage src={c.imageUrl} alt="" className="size-10 rounded object-cover" errorNode={<span className="size-10 rounded bg-canvas" />} />
                    ) : (
                      <span className="size-10 rounded bg-canvas" />
                    )}
                    <span className="min-w-0 truncate text-base font-semibold">
                      {c.name} <span className="font-normal text-ink-soft">#{c.sortOrder}</span>
                    </span>
                    {c.isActive === false && <StatusBadge label="Inactive" tone="warning" />}
                  </button>
                  <RowActionsMenu
                    subject={c.name}
                    actions={[
                      { label: 'Edit', onSelect: () => setDialog(c) },
                      c.isActive === false
                        ? { label: 'Make active', onSelect: () => run({ kind: 'Category', id: c.id, name: c.name }, 'reactivate'), separated: true }
                        : { label: 'Make inactive', onSelect: () => run({ kind: 'Category', id: c.id, name: c.name }, 'deactivate'), separated: true },
                      { label: 'Delete', danger: true, onSelect: () => run({ kind: 'Category', id: c.id, name: c.name }, 'delete') },
                    ]}
                  />
                </li>
              ))}
            </ul>
          )}
        </>
      )}
      {dialog && <CategoryDialog category={dialog === 'new' ? null : dialog} onClose={() => setDialog(null)} />}
      {lifecycleDialog}
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
