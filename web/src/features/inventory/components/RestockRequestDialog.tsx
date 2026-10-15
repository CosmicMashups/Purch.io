import { useState } from 'react';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog } from '../../../components/forms/FormDialog';
import { FormField, controlClass } from '../../../components/forms/FormField';
import type { Branch } from '../../branches/types';
import { useCreatePurchaseRequest } from '../queries';
import type { RestockSuggestion } from '../types';

const rowKey = (row: RestockSuggestion) => row.itemId ?? row.inventoryItemId ?? row.name;

/** Raises one purchase request from restock rows. Rows already covered by an open request or order start unticked. */
export function RestockRequestDialog({ rows, branches, onClose }: { rows: RestockSuggestion[]; branches: Branch[]; onClose: () => void }) {
  const create = useCreatePurchaseRequest();
  const [branchId, setBranchId] = useState(branches.length === 1 ? branches[0].id : '');
  const [dateNeeded, setDateNeeded] = useState('');
  const [remarks, setRemarks] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [picked, setPicked] = useState<Record<string, boolean>>(() => Object.fromEntries(rows.map((r) => [rowKey(r), r.pending === null])));
  const [quantity, setQuantity] = useState<Record<string, string>>(() => Object.fromEntries(rows.map((r) => [rowKey(r), String(r.suggestedQuantity)])));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    const lines = rows
      .filter((r) => picked[rowKey(r)])
      .map((r) => ({
        itemId: r.itemId,
        inventoryItemId: r.inventoryItemId,
        quantity: Number(quantity[rowKey(r)]),
        unit: r.unit,
      }));
    if (!branchId) return setError('Choose the branch the goods are for.');
    if (lines.length === 0) return setError('Tick at least one item.');
    if (lines.some((l) => !(l.quantity > 0))) return setError('Every quantity must be more than zero.');
    setError(null);
    const created = await create.mutateAsync({
      branchId,
      dateNeeded: dateNeeded || null,
      remarks: remarks.trim() || null,
      lines,
    });
    toast.success(`Purchase request ${created.controlNumber} created`);
    onClose();
  }

  return (
    <FormDialog title="Create purchase request" wide busy={create.isPending} submitLabel="Create request" onSubmit={(e) => void submit(e)} onClose={onClose}>
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
        <legend className="text-base font-semibold">Items to request</legend>
        {rows.map((row) => {
          const key = rowKey(row);
          return (
            <div key={key} className="flex items-center gap-3 rounded-control border border-line p-3">
              <input
                type="checkbox"
                aria-label={`Request ${row.name}`}
                checked={picked[key] ?? false}
                onChange={(e) => setPicked({ ...picked, [key]: e.target.checked })}
                className="size-6 shrink-0"
              />
              <div className="min-w-0 flex-1">
                <p className="truncate text-base font-semibold">{row.name}</p>
                <p className="text-sm text-ink-soft">
                  {row.onHand <= 0 ? 'Out of stock' : `${row.onHand} ${row.unit} left`}
                  {row.pending ? `, already on ${row.pending}` : ''}
                </p>
              </div>
              <label className="flex shrink-0 items-center gap-2 text-base">
                <span className="sr-only">Quantity for {row.name}</span>
                <input
                  type="number"
                  inputMode="decimal"
                  min="0"
                  step="any"
                  value={quantity[key] ?? ''}
                  onChange={(e) => setQuantity({ ...quantity, [key]: e.target.value })}
                  className={`${controlClass} w-24`}
                />
                <span className="w-10 text-ink-soft">{row.unit}</span>
              </label>
            </div>
          );
        })}
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
