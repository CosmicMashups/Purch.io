import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useParams } from 'react-router-dom';
import type { z } from 'zod';
import { departmentAssignmentSchema } from '../schemas';
import { useDepartments, useItems, useUpdateItemDepartment } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { ItemSubPageHeader } from './ItemSubPageHeader';
import { ApiError } from '../../../lib/apiError';
import { useState } from 'react';

type FormValues = z.infer<typeof departmentAssignmentSchema>;

export function AssignDepartmentPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const navigate = useNavigate();
  const { data: items } = useItems();
  const { data: departments } = useDepartments();
  const item = items?.find((i) => i.id === itemId);
  const updateItemDepartment = useUpdateItemDepartment(itemId!);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const { register, handleSubmit } = useForm<FormValues>({
    resolver: zodResolver(departmentAssignmentSchema),
    values: item ? { departmentId: item.departmentId } : undefined,
  });

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      await updateItemDepartment.mutateAsync({ departmentId: values.departmentId ?? null });
      navigate('/catalog/items');
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to assign department');
    }
  }

  if (!item) return <p className="text-sm text-gray-500">Loading…</p>;

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Assign Department" />
      <form onSubmit={handleSubmit(onSubmit)} className="flex max-w-sm flex-col gap-3">
        <Field label="Department">
          <select {...register('departmentId')} className={inputClass}>
            <option value="">None</option>
            {(departments ?? []).map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </select>
        </Field>
        {submitError && <p className="text-sm text-red-600">{submitError}</p>}
        <button
          type="submit"
          disabled={updateItemDepartment.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {updateItemDepartment.isPending ? 'Saving…' : 'Save'}
        </button>
      </form>
    </div>
  );
}
