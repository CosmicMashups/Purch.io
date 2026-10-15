import { useState } from 'react';
import { Trash } from '@phosphor-icons/react';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog } from '../../../components/forms/FormDialog';
import { FormField, controlClass } from '../../../components/forms/FormField';
import type { Branch } from '../../branches/types';
import { useItems } from '../../catalog/queries';
import { useCreatePurchaseRequest, useInventoryItems } from '../queries';
import type { RestockSuggestion } from '../types';

interface Line {
  key: string;
  itemId: string | null;
  inventoryItemId: string | null;
  name: string;
  unit: string;
  quantity: string;
  note: string | null;
}

const fromSuggestion = (r: RestockSuggestion): Line => ({
  key: r.itemId ?? `ing:${r.inventoryItemId}`,
  itemId: r.itemId,
  inventoryItemId: r.inventoryItemId,
  name: r.name,
  unit: r.unit,
  quantity: String(r.suggestedQuantity),
  note: r.pending ? `Already on ${r.pending}` : null,
});

/**
 * Raises one purchase request. It opens with whatever needs restocking (when started from there) and any other item
 * or ingredient can be added as a line, so a request is never limited to what is short.
 */
export function PurchaseRequestDialog({ rows = [], branches, onClose }: { rows?: RestockSuggestion[]; branches: Branch[]; onClose: () => void }) {
  const create = useCreatePurchaseRequest();
  const items = useItems();
  const ingredients = useInventoryItems();
  const [branchId, setBranchId] = useState(branches.length === 1 ? branches[0].id : '');
  const [dateNeeded, setDateNeeded] = useState('');
  const [remarks, setRemarks] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [lines, setLines] = useState<Line[]>(() => rows.map(fromSuggestion));
  const [adding, setAdding] = useState('');

  const options = [
    ...(items.data ?? []).filter((i) => i.isActive !== false).map((i) => ({ key: i.id, label: i.name, group: 'Items' })),
    ...(ingredients.data ?? [])
      .filter((i) => i.isActive && !i.isAutoCreatedForItem)
      .map((i) => ({ key: `ing:${i.id}`, label: `${i.name} (${i.baseUnit})`, group: 'Ingredients' })),
  ].filter((o) => !lines.some((l) => l.key === o.key));

  function addLine() {
    if (!adding) return;
    if (adding.startsWith('ing:')) {
      const found = (ingredients.data ?? []).find((i) => i.id === adding.slice(4));
      if (!found) return;
      setLines([...lines, { key: adding, itemId: null, inventoryItemId: found.id, name: found.name, unit: found.baseUnit, quantity: '1', note: null }]);
    } else {
      const found = (items.data ?? []).find((i) => i.id === adding);
      if (!found) return;
      setLines([...lines, { key: adding, itemId: found.id, inventoryItemId: null, name: found.name, unit: found.unit?.trim() || 'pc', quantity: '1', note: null }]);
    }
    setAdding('');
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!branchId) return setError('Choose the branch the goods are for.');
    if (lines.length === 0) return setError('Add at least one line.');
    if (lines.some((l) => !(Number(l.quantity) > 0))) return setError('Every quantity must be more than zero.');
    setError(null);
    const created = await create.mutateAsync({
      branchId,
      dateNeeded: dateNeeded || null,
      remarks: remarks.trim() || null,
      lines: lines.map((l) => ({ itemId: l.itemId, inventoryItemId: l.inventoryItemId, quantity: Number(l.quantity), unit: l.unit })),
    });
    toast.success(`Purchase request ${created.controlNumber} created`);
    onClose();
  }

  return (
    <FormDialog title="New purchase request" wide busy={create.isPending} submitLabel="Create request" onSubmit={(e) => void submit(e)} onClose={onClose}>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="For branch">
          <select value={branchId} onChange={(e) => setBranchId(e.target.value)} className={controlClass}>
            <option value="">Choose a branch</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Date needed (optional)">
          <input type="date" value={dateNeeded} onChange={(e) => setDateNeeded(e.target.value)} className={controlClass} />
        </FormField>
      </div>

      <fieldset className="flex flex-col gap-2">
        <legend className="text-base font-semibold">Line items</legend>
        {lines.length === 0 && <p className="text-base text-ink-soft">No lines yet. Add an item or ingredient below.</p>}
        {lines.map((line) => (
          <div key={line.key} className="flex items-center gap-3 rounded-control border border-line p-3">
            <div className="min-w-0 flex-1">
              <p className="truncate text-base font-semibold">{line.name}</p>
              {line.note && <p className="text-sm text-warn">{line.note}</p>}
            </div>
            <label className="flex shrink-0 items-center gap-2 text-base">
              <span className="sr-only">Quantity for {line.name}</span>
              <input
                type="number"
                inputMode="decimal"
                min="0"
                step="any"
                value={line.quantity}
                onChange={(e) => setLines(lines.map((l) => (l.key === line.key ? { ...l, quantity: e.target.value } : l)))}
                className={`${controlClass} w-24`}
              />
              <span className="w-10 text-ink-soft">{line.unit}</span>
            </label>
            <button
              type="button"
              aria-label={`Remove ${line.name}`}
              onClick={() => setLines(lines.filter((l) => l.key !== line.key))}
              className="grid size-12 shrink-0 place-items-center rounded-control text-ink-soft hover:bg-canvas hover:text-danger"
            >
              <Trash size={22} aria-hidden="true" />
            </button>
          </div>
        ))}
        <div className="flex items-center gap-2">
          <select aria-label="Add a line item" value={adding} onChange={(e) => setAdding(e.target.value)} className={`${controlClass} min-w-0 flex-1`}>
            <option value="">Add an item or ingredient</option>
            {['Items', 'Ingredients'].map(
              (group) =>
                options.some((o) => o.group === group) && (
                  <optgroup key={group} label={group}>
                    {options
                      .filter((o) => o.group === group)
                      .map((o) => (
                        <option key={o.key} value={o.key}>
                          {o.label}
                        </option>
                      ))}
                  </optgroup>
                ),
            )}
          </select>
          <button
            type="button"
            onClick={addLine}
            disabled={!adding}
            className="h-12 shrink-0 rounded-control bg-brand px-5 text-base font-semibold text-on-brand hover:bg-brand-strong disabled:opacity-50"
          >
            Add line
          </button>
        </div>
      </fieldset>

      <FormField label="Remarks (optional)">
        <textarea value={remarks} onChange={(e) => setRemarks(e.target.value)} rows={2} className={controlClass} />
      </FormField>
      {error && (
        <p role="alert" className="text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </FormDialog>
  );
}
