import { useState } from 'react';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { useUpdateCostOnReceiving, useUpdatePos, useUpdateBarcode, useUpdateCreditLedger, useUpdateDepartmentTracking, useUpdateInventoryTracking } from '../../tenant/queries';
import type { TenantSettings } from '../../tenant/types';

function Switch({ label, description, checked, busy, onChange }: { label: string; description: string; checked: boolean; busy: boolean; onChange: (next: boolean) => void }) {
  return (
    <li className="flex items-center justify-between gap-4 py-4">
      <div className="min-w-0">
        <p className="text-base font-semibold">{label}</p>
        <p className="text-sm text-ink-soft">{description}</p>
      </div>
      <input type="checkbox" role="switch" aria-label={label} checked={checked} disabled={busy} onChange={(e) => onChange(e.target.checked)} className="size-12 shrink-0 accent-brand" />
    </li>
  );
}

export function PosSection({ settings }: { settings: TenantSettings }) {
  const pos = useUpdatePos();
  const costOnReceiving = useUpdateCostOnReceiving();
  const saved = settings.parkedCartTtlHours ?? 24;
  const [typed, setHours] = useState<string | null>(null);
  const hours = typed ?? String(saved);
  const [error, setError] = useState<string | null>(null);

  function saveHours() {
    const value = Number(hours);
    if (hours.trim() === '' || !Number.isInteger(value) || value < 1 || value > 168) {
      setError('Enter a whole number of hours from 1 to 168');
      return;
    }
    setError(null);
    pos.mutate(
      { requiresBarcodePerItem: settings.requiresBarcodePerItem, parkedCartTtlHours: value },
      { onSuccess: () => { setHours(null); toast.success('Parked cart time saved'); } },
    );
  }

  return (
    <section aria-labelledby="pos-heading" className="rounded-panel border border-line bg-surface p-6">
      <h2 id="pos-heading" className="text-xl font-bold">
        Point of sale
      </h2>
      <ul className="mt-2 divide-y divide-line">
        <Switch
          label="Allow open items"
          description="Lets a cashier ring up something that is not in the catalog by typing a name and price."
          checked={settings.openItemEnabled ?? true}
          busy={pos.isPending}
          onChange={(next) =>
            pos.mutate(
              { requiresBarcodePerItem: settings.requiresBarcodePerItem, openItemEnabled: next },
              { onSuccess: () => toast.success(next ? 'Open items are on' : 'Open items are off') },
            )
          }
        />
        <Switch
          label="Update item cost when stock is received"
          description="Receiving stock replaces the saved cost with the cost on that delivery. Off keeps the cost you entered."
          checked={settings.updateCostOnReceiving ?? false}
          busy={costOnReceiving.isPending}
          onChange={(next) =>
            costOnReceiving.mutate(
              { useSeparateInventoryTracking: settings.useSeparateInventoryTracking, updateCostOnReceiving: next },
              { onSuccess: () => toast.success(next ? 'Cost will update on receiving' : 'Cost will stay as entered') },
            )
          }
        />
        <li className="flex flex-col gap-2 py-4">
          <label htmlFor="parked-ttl" className="text-base font-semibold">
            Keep parked carts for (hours)
          </label>
          <p id="parked-ttl-help" className="text-sm text-ink-soft">
            A parked cart that waits longer than this is released and its items go back on the shelf. From 1 to 168 hours.
          </p>
          <div className="flex items-start gap-3">
            <input
              id="parked-ttl"
              type="number"
              inputMode="numeric"
              min={1}
              max={168}
              value={hours}
              onChange={(e) => setHours(e.target.value)}
              aria-describedby="parked-ttl-help"
              aria-invalid={error !== null}
              className="h-12 w-32 rounded-control border border-line bg-surface px-3 text-base"
            />
            <button
              type="button"
              onClick={saveHours}
              disabled={pos.isPending}
              className="h-12 rounded-control bg-brand px-6 text-base font-semibold text-on-brand hover:bg-brand-strong disabled:opacity-50"
            >
              Save hours
            </button>
          </div>
          {error && (
            <p role="alert" className="text-sm text-danger">
              {error}
            </p>
          )}
        </li>
      </ul>
    </section>
  );
}

export function OptionsSection({ settings }: { settings: TenantSettings }) {
  const barcode = useUpdateBarcode();
  const credit = useUpdateCreditLedger();
  const tracking = useUpdateInventoryTracking();
  const departments = useUpdateDepartmentTracking();
  const [confirmTracking, setConfirmTracking] = useState<boolean | null>(null);

  function applyTracking() {
    if (confirmTracking === null) return;
    tracking.mutate(confirmTracking, { onSuccess: () => toast.success(confirmTracking ? 'Ingredient tracking is on' : 'Ingredient tracking is off') });
    setConfirmTracking(null);
  }

  return (
    <section aria-labelledby="options-heading" className="rounded-panel border border-line bg-surface p-6">
      <h2 id="options-heading" className="text-xl font-bold">
        Options
      </h2>
      <ul className="mt-2 divide-y divide-line">
        <Switch
          label="Require a barcode on every item"
          description="New items cannot be saved without a barcode."
          checked={settings.requiresBarcodePerItem}
          busy={barcode.isPending}
          onChange={(next) => barcode.mutate(next, { onSuccess: () => toast.success('Barcode rule saved') })}
        />
        <Switch
          label="Customer credit (utang)"
          description="Lets cashiers charge a sale to a customer's account."
          checked={settings.creditLedgerEnabled}
          busy={credit.isPending}
          onChange={(next) => credit.mutate(next, { onSuccess: () => toast.success('Customer credit setting saved') })}
        />
        <Switch
          label="Track ingredients separately"
          description="Items are made from ingredients through a recipe instead of holding their own stock."
          checked={settings.useSeparateInventoryTracking}
          busy={tracking.isPending}
          onChange={setConfirmTracking}
        />
        <Switch
          label="Monitor departments separately"
          description="Assign items to departments or concessionaires and report on them. When off, every department setting, field, report and chart is hidden."
          checked={settings.useDepartmentTracking}
          busy={departments.isPending}
          onChange={(next) => departments.mutate(next, { onSuccess: () => toast.success(next ? 'Department monitoring is on' : 'Department monitoring is off') })}
        />
      </ul>

      <ConfirmModal
        open={confirmTracking !== null}
        title={confirmTracking ? 'Track ingredients separately?' : 'Stop tracking ingredients separately?'}
        description={
          confirmTracking
            ? 'Items can then be made from ingredients using recipes. Existing items keep their own stock until you give them a recipe.'
            : 'Recipes stop being used for stock. Check your inventory afterwards.'
        }
        confirmLabel={confirmTracking ? 'Turn on' : 'Turn off'}
        onConfirm={applyTracking}
        onCancel={() => setConfirmTracking(null)}
      />
    </section>
  );
}
