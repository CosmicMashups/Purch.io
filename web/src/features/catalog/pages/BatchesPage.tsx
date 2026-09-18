import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useParams } from 'react-router-dom';
import type { z } from 'zod';
import { batchSchema } from '../schemas';
import { useItemBatches, useReceiveBatch } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { EmptyState } from '../../../components/EmptyState';
import { ItemSubPageHeader } from './ItemSubPageHeader';

type FormValues = z.infer<typeof batchSchema>;

export function BatchesPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const { data: batches, isLoading } = useItemBatches(itemId!);
  const receiveBatch = useReceiveBatch(itemId!);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(batchSchema) });

  async function onSubmit(values: FormValues) {
    await receiveBatch.mutateAsync({
      lotNumber: values.lotNumber ?? null,
      expiryDate: values.expiryDate ?? null,
      quantityReceived: values.quantityReceived,
    });
    reset();
  }

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Batches" />

      <form onSubmit={handleSubmit(onSubmit)} className="mb-6 flex max-w-md flex-col gap-3">
        <Field label="Lot number">
          <input {...register('lotNumber')} className={inputClass} />
        </Field>
        <Field label="Expiry date">
          <input type="date" {...register('expiryDate')} className={inputClass} />
        </Field>
        <Field label="Quantity received" error={errors.quantityReceived?.message}>
          <input type="number" {...register('quantityReceived')} className={inputClass} />
        </Field>
        <button
          type="submit"
          disabled={receiveBatch.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {receiveBatch.isPending ? 'Saving…' : 'Receive Batch'}
        </button>
      </form>

      {isLoading && <p className="text-sm text-gray-500">Loading…</p>}
      {!isLoading && (batches ?? []).length === 0 && <EmptyState title="No batches received yet" />}
      {(batches ?? []).length > 0 && (
        <table className="min-w-full divide-y divide-gray-200 rounded-lg border border-gray-200 bg-white text-sm">
          <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
            <tr>
              <th className="px-4 py-2">Lot</th>
              <th className="px-4 py-2">Expiry</th>
              <th className="px-4 py-2">Quantity</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {batches!.map((b) => (
              <tr key={b.id}>
                <td className="px-4 py-2">{b.lotNumber ?? '—'}</td>
                <td className="px-4 py-2">{b.expiryDate ?? '—'}</td>
                <td className="px-4 py-2">{b.quantityReceived}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
