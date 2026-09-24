import { useState } from 'react';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { useUpdateBarcode, useUpdateCreditLedger, useUpdateInventoryTracking } from '../../tenant/queries';
import type { TenantSettings } from '../../tenant/types';

function Switch({ label, description, checked, busy, onChange }: { label: string; description: string; checked: boolean; busy: boolean; onChange: (next: boolean) => void }) {
  return (
    <li className="flex items-center justify-between gap-4 py-4">
      <div className="min-w-0">
        <p className="text-base font-semibold">{label}</p>
        <p className="text-sm text-ink-soft">{description}</p>
      </div>
      <input type="checkbox" role="switch" aria-label={label} checked={checked} disabled={busy} onChange={(e) => onChange(e.target.checked)} className="size-8 shrink-0 accent-brand" />
    </li>
  );
}

export function OptionsSection({ settings }: { settings: TenantSettings }) {
  const barcode = useUpdateBarcode();
  const credit = useUpdateCreditLedger();
  const tracking = useUpdateInventoryTracking();
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
