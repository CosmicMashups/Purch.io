import { useParams } from 'react-router-dom';
import { useAttachModifierGroup, useItemModifierGroups, useModifierGroups } from '../queries';
import { EmptyState } from '../../../components/EmptyState';
import { ItemSubPageHeader } from './ItemSubPageHeader';
import { Field, inputClass } from '../../../components/Field';
import { useState } from 'react';

export function ItemModifierGroupsPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const { data: attached, isLoading } = useItemModifierGroups(itemId!);
  const { data: allGroups } = useModifierGroups();
  const attachModifierGroup = useAttachModifierGroup(itemId!);
  const [selectedGroupId, setSelectedGroupId] = useState('');

  const attachedIds = new Set((attached ?? []).map((g) => g.id));
  const availableGroups = (allGroups ?? []).filter((g) => !attachedIds.has(g.id));

  async function handleAttach(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedGroupId) return;
    await attachModifierGroup.mutateAsync({ modifierGroupId: selectedGroupId });
    setSelectedGroupId('');
  }

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Modifier Groups" />

      <form onSubmit={handleAttach} className="mb-6 flex max-w-md flex-col gap-3">
        <Field label="Attach a modifier group">
          <select value={selectedGroupId} onChange={(e) => setSelectedGroupId(e.target.value)} className={inputClass}>
            <option value="">Select a group</option>
            {availableGroups.map((g) => (
              <option key={g.id} value={g.id}>
                {g.name}
              </option>
            ))}
          </select>
        </Field>
        <button
          type="submit"
          disabled={!selectedGroupId || attachModifierGroup.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {attachModifierGroup.isPending ? 'Attaching…' : 'Attach'}
        </button>
      </form>

      {isLoading && <p className="text-sm text-gray-500">Loading…</p>}
      {!isLoading && (attached ?? []).length === 0 && <EmptyState title="No modifier groups attached" />}
      {(attached ?? []).length > 0 && (
        <ul className="flex flex-col gap-2">
          {attached!.map((g) => (
            <li key={g.id} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
              <p className="font-medium text-gray-900">{g.name}</p>
              <p className="text-xs text-gray-500">
                {g.allowMultipleSelection ? 'Multiple selection' : 'Single selection'} ·{' '}
                {g.isRequired ? 'Required' : 'Optional'}
              </p>
              {g.modifiers.length > 0 && (
                <p className="mt-1 text-sm text-gray-600">
                  {g.modifiers.map((m) => `${m.name} (+₱${m.priceDelta.toFixed(2)})`).join(', ')}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
