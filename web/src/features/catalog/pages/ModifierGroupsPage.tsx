import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { z } from 'zod';
import { modifierGroupSchema, modifierSchema } from '../schemas';
import { useAddModifier, useCreateModifierGroup, useModifierGroups } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonList } from '../../../components/Skeleton';
import { useState } from 'react';

type GroupFormValues = z.infer<typeof modifierGroupSchema>;
type ModifierFormValues = z.infer<typeof modifierSchema>;

export function ModifierGroupsPage() {
  const { data: groups, isLoading, isError, error, refetch } = useModifierGroups();
  const createModifierGroup = useCreateModifierGroup();
  const [activeGroupId, setActiveGroupId] = useState<string | null>(null);

  const {
    register: registerGroup,
    handleSubmit: handleGroupSubmit,
    reset: resetGroup,
    formState: { errors: groupErrors },
  } = useForm<GroupFormValues>({
    resolver: zodResolver(modifierGroupSchema),
    defaultValues: { name: '', allowMultipleSelection: false, isRequired: false },
  });

  async function onCreateGroup(values: GroupFormValues) {
    await createModifierGroup.mutateAsync(values);
    resetGroup({ name: '', allowMultipleSelection: false, isRequired: false });
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-xl font-semibold text-gray-900">Modifier Groups</h1>

      <form onSubmit={handleGroupSubmit(onCreateGroup)} className="flex max-w-md flex-col gap-3">
        <Field label="Group name" error={groupErrors.name?.message}>
          <input {...registerGroup('name')} className={inputClass} placeholder="Spice Level" />
        </Field>
        <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
          <input type="checkbox" {...registerGroup('allowMultipleSelection')} />
          Allow multiple selection
        </label>
        <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
          <input type="checkbox" {...registerGroup('isRequired')} />
          Required
        </label>
        <button
          type="submit"
          disabled={createModifierGroup.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {createModifierGroup.isPending ? 'Saving…' : 'Add Group'}
        </button>
      </form>

      {isLoading && <SkeletonList />}
      {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
      {!isLoading && !isError && (groups ?? []).length === 0 && <EmptyState title="No modifier groups yet" />}
      <ul className="flex flex-col gap-3">
        {!isError && (groups ?? []).map((g) => (
          <li key={g.id} className="rounded-lg border border-gray-200 bg-white p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">{g.name}</p>
                <p className="text-xs text-gray-500">
                  {g.allowMultipleSelection ? 'Multiple selection' : 'Single selection'} ·{' '}
                  {g.isRequired ? 'Required' : 'Optional'}
                </p>
              </div>
              <button
                onClick={() => setActiveGroupId(activeGroupId === g.id ? null : g.id)}
                className="text-sm text-gray-500 hover:text-gray-900 hover:underline"
              >
                {activeGroupId === g.id ? 'Close' : 'Add Modifier'}
              </button>
            </div>

            {g.modifiers.length > 0 && (
              <p className="mt-2 text-sm text-gray-600">
                {g.modifiers.map((m) => `${m.name} (+₱${m.priceDelta.toFixed(2)})`).join(', ')}
              </p>
            )}

            {activeGroupId === g.id && <AddModifierForm groupId={g.id} />}
          </li>
        ))}
      </ul>
    </div>
  );
}

function AddModifierForm({ groupId }: { groupId: string }) {
  const addModifier = useAddModifier(groupId);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ModifierFormValues>({ resolver: zodResolver(modifierSchema), defaultValues: { priceDelta: 0 } });

  async function onSubmit(values: ModifierFormValues) {
    await addModifier.mutateAsync(values);
    reset({ name: '', priceDelta: 0 });
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="mt-3 flex max-w-sm flex-col gap-2 border-t border-gray-100 pt-3">
      <Field label="Modifier name" error={errors.name?.message}>
        <input {...register('name')} className={inputClass} />
      </Field>
      <Field label="Price delta" error={errors.priceDelta?.message}>
        <input type="number" step="0.01" {...register('priceDelta')} className={inputClass} />
      </Field>
      <button
        type="submit"
        disabled={addModifier.isPending}
        className="self-start rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
      >
        {addModifier.isPending ? 'Saving…' : 'Add'}
      </button>
    </form>
  );
}
