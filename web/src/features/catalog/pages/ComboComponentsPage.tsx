import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { Field, inputClass } from '../../../components/Field';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonRows } from '../../../components/Skeleton';
import { toast } from '../../../components/feedback/toastStore';
import { userMessage } from '../../../lib/apiError';
import { formatPeso } from '../../dashboard/format';
import { useCategories, useComboComponents, useCreateComboComponent, useDeleteComboComponent, useItems, useUpdateComboComponent } from '../queries';
import { PricingType, type ComboChoiceUpcharge, type CreateItemComboComponentRequest, type ItemComboComponent } from '../types';
import { ItemSubPageHeader } from './ItemSubPageHeader';

type SlotKind = 'choose' | 'fixed';

interface FormState {
  kind: SlotKind;
  slotLabel: string;
  quantity: string;
  categoryId: string;
  fixedItemId: string;
  /** A flat charge added once for the slot, whichever item is picked. */
  flatUpcharge: string;
  /** item id -> extra price typed for that choice; empty means the choice is included. */
  choiceUpcharges: Record<string, string>;
}

const EMPTY: FormState = { kind: 'choose', slotLabel: '', quantity: '1', categoryId: '', fixedItemId: '', flatUpcharge: '', choiceUpcharges: {} };

function fromSlot(slot: ItemComboComponent): FormState {
  return {
    kind: slot.componentItemId ? 'fixed' : 'choose',
    slotLabel: slot.slotLabel,
    quantity: String(slot.quantity),
    categoryId: slot.componentCategoryId,
    fixedItemId: slot.componentItemId ?? '',
    flatUpcharge: slot.substitutionUpchargeAmount ? String(slot.substitutionUpchargeAmount) : '',
    choiceUpcharges: Object.fromEntries((slot.choiceUpcharges ?? []).map((entry) => [entry.itemId, String(entry.amount)])),
  };
}

/**
 * The parts of a combo or deal. Any number of slots, each either a choice from a category (with an optional extra
 * price on particular choices) or one fixed item, so "Buy 1 Take 1" is just a combo whose slot is two of the same item.
 */
export function ComboComponentsPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const { data: components, isLoading, isError, error, refetch } = useComboComponents(itemId!);
  const { data: categories } = useCategories();
  const { data: items } = useItems();
  const create = useCreateComboComponent(itemId!);
  const update = useUpdateComboComponent(itemId!);
  const remove = useDeleteComboComponent(itemId!);
  const [form, setForm] = useState<FormState>(EMPTY);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState<string | null>(null);

  const itemNameById = new Map((items ?? []).map((item) => [item.id, item.name]));
  const categoryNameById = new Map((categories ?? []).map((category) => [category.id, category.name]));
  // A combo cannot contain another combo (or itself), so only ordinary items can be fixed in a slot.
  const fixedChoices = (items ?? []).filter((item) => item.isActive && item.pricingType !== PricingType.Combo && item.id !== itemId);
  const categoryItems = (items ?? []).filter((item) => item.isActive && form.categoryId !== '' && item.categoryId === form.categoryId);
  const busy = create.isPending || update.isPending;

  function set<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function toRequest(): CreateItemComboComponentRequest | null {
    const quantity = Number(form.quantity);
    if (form.slotLabel.trim() === '') return fail('Give the slot a label, such as "Choose a drink".');
    if (!Number.isInteger(quantity) || quantity < 1) return fail('Quantity must be a whole number, 1 or more.');

    if (form.kind === 'fixed') {
      if (form.fixedItemId === '') return fail('Pick the item this slot always contains.');
      return { componentCategoryId: form.categoryId, slotLabel: form.slotLabel.trim(), quantity, substitutionUpchargeAmount: null, componentItemId: form.fixedItemId };
    }

    if (form.categoryId === '') return fail('Pick the category the customer chooses from.');
    const flat = form.flatUpcharge.trim() === '' ? null : Number(form.flatUpcharge);
    if (flat !== null && !(flat >= 0)) return fail('The slot surcharge cannot be negative.');
    const choiceUpcharges: ComboChoiceUpcharge[] = [];
    for (const [id, text] of Object.entries(form.choiceUpcharges)) {
      if (text.trim() === '') continue;
      const amount = Number(text);
      if (!(amount >= 0)) return fail('A choice surcharge cannot be negative.');
      if (amount > 0) choiceUpcharges.push({ itemId: id, amount });
    }
    return { componentCategoryId: form.categoryId, slotLabel: form.slotLabel.trim(), quantity, substitutionUpchargeAmount: flat, choiceUpcharges };
  }

  function fail(message: string): null {
    setProblem(message);
    return null;
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setProblem(null);
    const body = toRequest();
    if (!body) return;
    try {
      if (editingId) await update.mutateAsync({ componentId: editingId, body });
      else await create.mutateAsync(body);
      setForm(EMPTY);
      setEditingId(null);
    } catch (e) {
      setProblem(userMessage(e));
    }
  }

  async function confirmDelete(componentId: string) {
    try {
      await remove.mutateAsync(componentId);
      setConfirmingDelete(null);
      if (editingId === componentId) {
        setEditingId(null);
        setForm(EMPTY);
      }
    } catch (e) {
      toast.error(userMessage(e));
    }
  }

  return (
    <div>
      <ItemSubPageHeader itemId={itemId!} title="Combo Components" />

      <form onSubmit={submit} className="mb-6 flex max-w-xl flex-col gap-3" aria-label={editingId ? 'Edit slot' : 'Add slot'}>
        <fieldset className="flex gap-6">
          <legend className="mb-1 text-sm font-medium">This slot is</legend>
          <label className="flex items-center gap-2 text-sm">
            <input type="radio" name="slot-kind" checked={form.kind === 'choose'} onChange={() => set('kind', 'choose')} />
            A choice from a category
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="radio" name="slot-kind" checked={form.kind === 'fixed'} onChange={() => set('kind', 'fixed')} />
            Always one specific item
          </label>
        </fieldset>

        <Field label="Slot label">
          <input value={form.slotLabel} onChange={(e) => set('slotLabel', e.target.value)} className={inputClass} placeholder={form.kind === 'fixed' ? '2 pcs Fried Chicken' : 'Choose a drink'} />
        </Field>

        {form.kind === 'choose' ? (
          <Field label="Customer chooses from category">
            <select
              value={form.categoryId}
              onChange={(e) => setForm((current) => ({ ...current, categoryId: e.target.value, choiceUpcharges: {} }))}
              className={inputClass}
            >
              <option value="">Select a category</option>
              {(categories ?? []).map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </Field>
        ) : (
          <Field label="Item">
            <select value={form.fixedItemId} onChange={(e) => set('fixedItemId', e.target.value)} className={inputClass}>
              <option value="">Select an item</option>
              {fixedChoices.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </Field>
        )}

        <Field label={form.kind === 'fixed' ? 'How many of it' : 'How many the customer picks'}>
          <input type="number" min={1} step={1} value={form.quantity} onChange={(e) => set('quantity', e.target.value)} className={inputClass} />
        </Field>

        {form.kind === 'choose' && (
          <>
            <Field label="Slot surcharge (optional, charged once whatever is picked)">
              <input type="number" min={0} step="0.01" value={form.flatUpcharge} onChange={(e) => set('flatUpcharge', e.target.value)} className={inputClass} />
            </Field>
            {categoryItems.length > 0 && (
              <fieldset className="flex flex-col gap-2 rounded-md border border-gray-200 p-3">
                <legend className="px-1 text-sm font-medium">Extra price for particular choices (leave blank if included)</legend>
                {categoryItems.map((item) => (
                  <label key={item.id} className="flex items-center justify-between gap-3 text-sm">
                    <span>{item.name}</span>
                    <input
                      type="number"
                      min={0}
                      step="0.01"
                      aria-label={`Extra price for ${item.name}`}
                      value={form.choiceUpcharges[item.id] ?? ''}
                      onChange={(e) => setForm((current) => ({ ...current, choiceUpcharges: { ...current.choiceUpcharges, [item.id]: e.target.value } }))}
                      className={`${inputClass} w-28`}
                    />
                  </label>
                ))}
              </fieldset>
            )}
          </>
        )}

        {problem && (
          <p role="alert" className="text-sm text-red-700">
            {problem}
          </p>
        )}
        <div className="flex gap-3">
          <button type="submit" disabled={busy} className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
            {busy ? 'Saving…' : editingId ? 'Save changes' : 'Add Component'}
          </button>
          {editingId && (
            <button
              type="button"
              onClick={() => {
                setEditingId(null);
                setForm(EMPTY);
                setProblem(null);
              }}
              className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium"
            >
              Cancel
            </button>
          )}
        </div>
      </form>

      {isLoading && <SkeletonRows rows={3} />}
      {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
      {!isLoading && !isError && (components ?? []).length === 0 && <EmptyState title="No combo components yet" />}
      {!isError && (components ?? []).length > 0 && (
        <table className="min-w-full divide-y divide-gray-200 rounded-lg border border-gray-200 bg-white text-sm">
          <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
            <tr>
              <th className="px-4 py-2">Slot</th>
              <th className="px-4 py-2">Contains</th>
              <th className="px-4 py-2">Qty</th>
              <th className="px-4 py-2">Surcharges</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {components!.map((c) => (
              <tr key={c.id}>
                <td className="px-4 py-2">{c.slotLabel}</td>
                <td className="px-4 py-2">
                  {c.componentItemId ? `Always ${c.componentItemName ?? itemNameById.get(c.componentItemId) ?? 'an item'}` : `Choice from ${c.componentCategoryName ?? categoryNameById.get(c.componentCategoryId) ?? 'a category'}`}
                </td>
                <td className="px-4 py-2">{c.quantity}</td>
                <td className="px-4 py-2">
                  {[
                    c.substitutionUpchargeAmount ? `${formatPeso(c.substitutionUpchargeAmount)} per slot` : null,
                    ...(c.choiceUpcharges ?? []).map((entry) => `${itemNameById.get(entry.itemId) ?? 'Choice'} +${formatPeso(entry.amount)}`),
                  ]
                    .filter(Boolean)
                    .join(', ') || 'None'}
                </td>
                <td className="px-4 py-2 text-right">
                  <button
                    type="button"
                    onClick={() => {
                      setEditingId(c.id);
                      setForm(fromSlot(c));
                      setProblem(null);
                    }}
                    className="mr-3 text-sm font-medium underline"
                  >
                    Edit
                  </button>
                  {confirmingDelete === c.id ? (
                    <>
                      <button type="button" onClick={() => void confirmDelete(c.id)} disabled={remove.isPending} className="mr-2 text-sm font-medium text-red-700 underline">
                        Confirm delete
                      </button>
                      <button type="button" onClick={() => setConfirmingDelete(null)} className="text-sm font-medium underline">
                        Keep
                      </button>
                    </>
                  ) : (
                    <button type="button" onClick={() => setConfirmingDelete(c.id)} className="text-sm font-medium text-red-700 underline">
                      Delete
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
