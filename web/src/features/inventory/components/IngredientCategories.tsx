import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { EditorCard } from '../../../components/forms/EditorCard';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { ListCard, QueryList } from '../../../components/lists/QueryList';
import { userMessage } from '../../../lib/apiError';
import {
  useCreateInventoryCategory,
  useDeleteInventoryCategory,
  useInventoryCategories,
  useInventoryItems,
  useUpdateInventoryCategory,
} from '../queries';
import type { InventoryCategory } from '../types';

const categorySchema = z.object({
  name: z.string().trim().min(1, 'Enter a name'),
  sortOrder: z.number({ invalid_type_error: 'Enter a number', required_error: 'Enter a number' }).int('Whole numbers only'),
});
type CategoryForm = z.infer<typeof categorySchema>;

const linkButton = 'h-12 text-base font-semibold text-brand-strong underline';

/** The groups ingredients are filed under. Anyone in inventory can read them; only Admin and Manager change them. */
export function IngredientCategories({ canEdit }: { canEdit: boolean }) {
  const categories = useInventoryCategories();
  const ingredients = useInventoryItems();
  const [editing, setEditing] = useState<InventoryCategory | null>(null);
  const [deleting, setDeleting] = useState<InventoryCategory | null>(null);
  const remove = useDeleteInventoryCategory();

  const countIn = (id: string) => (ingredients.data ?? []).filter((i) => i.categoryId === id).length;

  async function confirmDelete() {
    if (!deleting) return;
    try {
      await remove.mutateAsync(deleting.id);
      toast.success(`${deleting.name} removed`);
      if (editing?.id === deleting.id) setEditing(null);
    } catch (error) {
      toast.error(userMessage(error));
    }
    setDeleting(null);
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
      <QueryList
        query={categories}
        errorTitle="Categories could not be loaded"
        emptyMessage={canEdit ? 'No ingredient categories yet. Add one to group your ingredients.' : 'No ingredient categories yet.'}
        renderRow={(category) => (
          <ListCard key={category.id}>
            <div className="min-w-0">
              <p className="text-base font-semibold">{category.name}</p>
              <p className="text-sm text-ink-soft">
                {countIn(category.id)} ingredient{countIn(category.id) === 1 ? '' : 's'}, shown in position {category.sortOrder}
              </p>
              {canEdit && (
                <div className="mt-2 flex flex-wrap gap-x-5">
                  <button type="button" className={linkButton} onClick={() => setEditing(category)}>
                    Edit
                  </button>
                  <button type="button" className={linkButton} onClick={() => setDeleting(category)}>
                    Delete
                  </button>
                </div>
              )}
            </div>
          </ListCard>
        )}
      />
      {canEdit && <CategoryEditor key={editing?.id ?? 'new'} category={editing} onDone={() => setEditing(null)} />}
      <ConfirmModal
        open={deleting !== null}
        destructive
        busy={remove.isPending}
        title={`Delete ${deleting?.name ?? 'category'}?`}
        description="Its ingredients are not deleted. They just become uncategorised."
        confirmLabel="Delete"
        onConfirm={() => void confirmDelete()}
        onCancel={() => setDeleting(null)}
      />
    </div>
  );
}

function CategoryEditor({ category, onDone }: { category: InventoryCategory | null; onDone: () => void }) {
  const create = useCreateInventoryCategory();
  const update = useUpdateInventoryCategory();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CategoryForm>({
    resolver: zodResolver(categorySchema),
    defaultValues: category ? { name: category.name, sortOrder: category.sortOrder } : { name: '', sortOrder: 0 },
  });

  const submit = handleSubmit(async (v) => {
    if (category) await update.mutateAsync({ id: category.id, body: v });
    else await create.mutateAsync(v);
    toast.success(category ? 'Category updated' : 'Category added');
    reset({ name: '', sortOrder: 0 });
    onDone();
  });

  return (
    <EditorCard title="category" editing={!!category} busy={create.isPending || update.isPending} onSubmit={submit} onCancel={onDone}>
      <FormField label="Name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      <FormField label="Position" hint="Lower numbers come first" error={errors.sortOrder?.message}>
        <input type="number" inputMode="numeric" {...register('sortOrder', { valueAsNumber: true })} className={controlClass} />
      </FormField>
    </EditorCard>
  );
}
