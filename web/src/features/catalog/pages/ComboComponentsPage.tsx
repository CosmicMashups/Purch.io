import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useParams } from 'react-router-dom';
import type { z } from 'zod';
import { comboComponentSchema } from '../schemas';
import { useCategories, useComboComponents, useCreateComboComponent } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonRows } from '../../../components/Skeleton';
import { ItemSubPageHeader } from './ItemSubPageHeader';

type FormValues = z.infer<typeof comboComponentSchema>;

export function ComboComponentsPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const { data: components, isLoading, isError, error, refetch } = useComboComponents(itemId!);
  const { data: categories } = useCategories();
  const createComboComponent = useCreateComboComponent(itemId!);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(comboComponentSchema), defaultValues: { quantity: 1, substitutionUpchargeAmount: 0 } });

  async function onSubmit(values: FormValues) {
    await createComboComponent.mutateAsync(values);
    reset();
  }

  const categoryNameById = new Map((categories ?? []).map((c) => [c.id, c.name]));

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Combo Components" />

      <form onSubmit={handleSubmit(onSubmit)} className="mb-6 flex max-w-md flex-col gap-3">
        <Field label="Slot label" error={errors.slotLabel?.message}>
          <input {...register('slotLabel')} className={inputClass} placeholder="Main" />
        </Field>
        <Field label="Component category" error={errors.componentCategoryId?.message}>
          <select {...register('componentCategoryId')} className={inputClass}>
            <option value="">Select a category</option>
            {(categories ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Quantity" error={errors.quantity?.message}>
          <input type="number" {...register('quantity')} className={inputClass} />
        </Field>
        <Field label="Substitution upcharge" error={errors.substitutionUpchargeAmount?.message}>
          <input type="number" step="0.01" {...register('substitutionUpchargeAmount')} className={inputClass} />
        </Field>
        <button
          type="submit"
          disabled={createComboComponent.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {createComboComponent.isPending ? 'Saving…' : 'Add Component'}
        </button>
      </form>

      {isLoading && <SkeletonRows rows={3} />}
      {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
      {!isLoading && !isError && (components ?? []).length === 0 && <EmptyState title="No combo components yet" />}
      {!isError && (components ?? []).length > 0 && (
        <table className="min-w-full divide-y divide-gray-200 rounded-lg border border-gray-200 bg-white text-sm">
          <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
            <tr>
              <th className="px-4 py-2">Slot</th>
              <th className="px-4 py-2">Category</th>
              <th className="px-4 py-2">Qty</th>
              <th className="px-4 py-2">Upcharge</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {components!.map((c) => (
              <tr key={c.id}>
                <td className="px-4 py-2">{c.slotLabel}</td>
                <td className="px-4 py-2">{categoryNameById.get(c.componentCategoryId) ?? '—'}</td>
                <td className="px-4 py-2">{c.quantity}</td>
                <td className="px-4 py-2">₱{c.substitutionUpchargeAmount.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
