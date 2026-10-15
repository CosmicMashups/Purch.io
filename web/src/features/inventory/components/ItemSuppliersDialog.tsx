import { useState } from 'react';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog } from '../../../components/forms/FormDialog';
import { useSetItemSuppliers } from '../queries';
import type { RestockSuggestion, Supplier } from '../types';

/** Which suppliers an item or ingredient is normally bought from, and which one is preferred. */
export function ItemSuppliersDialog({ row, suppliers, onClose }: { row: RestockSuggestion; suppliers: Supplier[]; onClose: () => void }) {
  const save = useSetItemSuppliers();
  const [chosen, setChosen] = useState<Set<string>>(() => new Set(row.suppliers.map((s) => s.supplierId)));
  const [preferred, setPreferred] = useState<string | null>(row.suppliers.find((s) => s.isPreferred)?.supplierId ?? null);
  const [costs, setCosts] = useState<Record<string, string>>(() => Object.fromEntries(row.suppliers.map((s) => [s.supplierId, s.unitCost === null ? '' : String(s.unitCost)])));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    const owner = row.itemId ? { itemId: row.itemId } : { inventoryItemId: row.inventoryItemId! };
    const list = [...chosen].map((supplierId) => ({
      supplierId,
      isPreferred: supplierId === preferred,
      unitCost: costs[supplierId]?.trim() ? Number(costs[supplierId]) : null,
    }));
    await save.mutateAsync({ owner, suppliers: list });
    toast.success(`Suppliers saved for ${row.name}`);
    onClose();
  }

  const active = suppliers.filter((s) => s.isActive);
  return (
    <FormDialog title={`Suppliers for ${row.name}`} busy={save.isPending} submitLabel="Save suppliers" onSubmit={(e) => void submit(e)} onClose={onClose}>
      {active.length === 0 && <p className="text-base text-ink-soft">Add a supplier first, then link it here.</p>}
      {active.map((s) => {
        const on = chosen.has(s.id);
        return (
          <div key={s.id} className="flex min-h-12 items-center gap-3 rounded-control border border-line px-3">
            <label className="flex flex-1 items-center gap-3 text-base">
              <input
                type="checkbox"
                className="size-6"
                checked={on}
                onChange={(e) => {
                  const next = new Set(chosen);
                  if (e.target.checked) next.add(s.id);
                  else next.delete(s.id);
                  setChosen(next);
                  if (!e.target.checked && preferred === s.id) setPreferred(null);
                }}
              />
              {s.name}
            </label>
            {on && (
              <input
                type="number"
                inputMode="decimal"
                min="0"
                step="0.01"
                aria-label={`Unit cost from ${s.name}`}
                placeholder="Unit cost"
                value={costs[s.id] ?? ''}
                onChange={(e) => setCosts({ ...costs, [s.id]: e.target.value })}
                className="h-12 w-28 rounded-control border border-line px-2 text-base"
              />
            )}
            {on && (
              <label className="flex items-center gap-2 text-sm font-semibold">
                <input type="radio" name="preferred" className="size-5" checked={preferred === s.id} onChange={() => setPreferred(s.id)} />
                Preferred
              </label>
            )}
          </div>
        );
      })}
    </FormDialog>
  );
}
