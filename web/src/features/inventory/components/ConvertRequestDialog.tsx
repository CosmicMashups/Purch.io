import { useMemo, useState } from 'react';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog } from '../../../components/forms/FormDialog';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { useConvertPurchaseRequest } from '../queries';
import type { PurchaseRequest, RestockSuggestion, Supplier } from '../types';

/** Turns an approved request into one draft purchase order per supplier. Lines left unassigned are dropped. */
export function ConvertRequestDialog({
  request,
  suppliers,
  suggestions,
  onClose,
}: {
  request: PurchaseRequest;
  suppliers: Supplier[];
  suggestions: RestockSuggestion[];
  onClose: () => void;
}) {
  const convert = useConvertPurchaseRequest();
  const active = suppliers.filter((s) => s.isActive);
  const [error, setError] = useState<string | null>(null);
  const [vat, setVat] = useState<Record<string, boolean>>({});
  const [discount, setDiscount] = useState<Record<string, string>>({});

  // The preferred supplier is pre-selected; a lone link counts as preferred. No link means "choose one or skip".
  const [choice, setChoice] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      request.lines.map((line) => {
        const links = suggestions.find((s) => (line.inventoryItemId ? s.inventoryItemId === line.inventoryItemId : s.itemId === line.itemId))?.suppliers ?? [];
        const pick = links.find((l) => l.isPreferred) ?? (links.length === 1 ? links[0] : undefined);
        return [line.id, pick?.supplierId ?? ''];
      }),
    ),
  );

  const groups = useMemo(() => {
    const map = new Map<string, string[]>();
    for (const line of request.lines) {
      const supplierId = choice[line.id];
      if (supplierId) map.set(supplierId, [...(map.get(supplierId) ?? []), line.id]);
    }
    return map;
  }, [choice, request.lines]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (groups.size === 0) return setError('Choose a supplier for at least one line.');
    setError(null);
    const orders = [...groups].map(([supplierId, lineIds]) => ({
      supplierId,
      lineIds,
      vatInclusive: vat[supplierId] ?? false,
      discountAmount: Number(discount[supplierId]) || 0,
    }));
    const made = await convert.mutateAsync({
      id: request.id,
      body: { orders },
    });
    toast.success(`${made.length} purchase ${made.length === 1 ? 'order' : 'orders'} created as drafts`);
    onClose();
  }

  const nameOf = (id: string) => suppliers.find((s) => s.id === id)?.name ?? 'Supplier';

  return (
    <FormDialog
      title={`Create purchase orders from ${request.controlNumber}`}
      wide
      busy={convert.isPending}
      submitLabel="Create orders"
      onSubmit={(e) => void submit(e)}
      onClose={onClose}
    >
      <fieldset className="flex flex-col gap-2">
        <legend className="text-base font-semibold">Supplier for each line</legend>
        {request.lines.map((line) => (
          <div key={line.id} className="flex flex-wrap items-center gap-3 rounded-control border border-line p-3">
            <div className="min-w-0 flex-1">
              <p className="truncate text-base font-semibold">{line.name}</p>
              <p className="text-sm text-ink-soft">
                {line.quantity} {line.unit}
              </p>
            </div>
            <select
              aria-label={`Supplier for ${line.name}`}
              value={choice[line.id] ?? ''}
              onChange={(e) => setChoice({ ...choice, [line.id]: e.target.value })}
              className={`${controlClass} sm:w-60`}
            >
              <option value="">Unassigned: choose or skip</option>
              {active.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        ))}
      </fieldset>
      {[...groups].map(([supplierId, lineIds]) => (
        <div key={supplierId} className="flex flex-wrap items-end gap-4 rounded-control bg-canvas p-3">
          <p className="min-w-0 flex-1 text-base font-semibold">
            {nameOf(supplierId)}{' '}
            <span className="font-normal text-ink-soft">
              ({lineIds.length} {lineIds.length === 1 ? 'line' : 'lines'})
            </span>
          </p>
          <label className="flex h-12 items-center gap-2 text-base">
            <input type="checkbox" className="size-6" checked={vat[supplierId] ?? false} onChange={(e) => setVat({ ...vat, [supplierId]: e.target.checked })} />
            Prices include VAT
          </label>
          <FormField label="Discount">
            <input
              type="number"
              inputMode="decimal"
              min="0"
              step="0.01"
              value={discount[supplierId] ?? ''}
              onChange={(e) => setDiscount({ ...discount, [supplierId]: e.target.value })}
              className={`${controlClass} w-32`}
            />
          </FormField>
        </div>
      ))}
      <p className="text-sm text-ink-soft">Unit prices come from the supplier link or the item cost. Lines left unassigned are dropped from the orders.</p>
      {error && (
        <p role="alert" className="text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </FormDialog>
  );
}
