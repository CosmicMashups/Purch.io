import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useParams } from 'react-router-dom';
import type { z } from 'zod';
import { bundleRuleSchema } from '../schemas';
import { useBundleRules, useCreateBundleRule } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonRows } from '../../../components/Skeleton';
import { ItemSubPageHeader } from './ItemSubPageHeader';

type FormValues = z.infer<typeof bundleRuleSchema>;

export function BundleRulesPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const { data: rules, isLoading, isError, error, refetch } = useBundleRules(itemId!);
  const createBundleRule = useCreateBundleRule(itemId!);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(bundleRuleSchema) });

  async function onSubmit(values: FormValues) {
    await createBundleRule.mutateAsync(values);
    reset();
  }

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Bundle Rules" />

      <form onSubmit={handleSubmit(onSubmit)} className="mb-6 flex max-w-md flex-col gap-3">
        <Field label="Description" error={errors.description?.message}>
          <input {...register('description')} className={inputClass} placeholder="Buy 2 Get 1" />
        </Field>
        <Field label="Trigger quantity" error={errors.triggerQuantity?.message}>
          <input type="number" {...register('triggerQuantity')} className={inputClass} />
        </Field>
        <Field label="Bundle price" error={errors.bundlePrice?.message}>
          <input type="number" step="0.01" {...register('bundlePrice')} className={inputClass} />
        </Field>
        <button
          type="submit"
          disabled={createBundleRule.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {createBundleRule.isPending ? 'Saving…' : 'Add Rule'}
        </button>
      </form>

      {isLoading && <SkeletonRows rows={3} />}
      {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
      {!isLoading && !isError && (rules ?? []).length === 0 && <EmptyState title="No bundle rules yet" />}
      {!isError && (rules ?? []).length > 0 && (
        <table className="min-w-full divide-y divide-gray-200 rounded-lg border border-gray-200 bg-white text-sm">
          <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
            <tr>
              <th className="px-4 py-2">Description</th>
              <th className="px-4 py-2">Trigger Qty</th>
              <th className="px-4 py-2">Bundle Price</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rules!.map((r) => (
              <tr key={r.id}>
                <td className="px-4 py-2">{r.description}</td>
                <td className="px-4 py-2">{r.triggerQuantity}</td>
                <td className="px-4 py-2">₱{r.bundlePrice.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
