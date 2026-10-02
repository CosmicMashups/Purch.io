import { useMemo, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { FormField, PrimaryButton, controlClass } from '../../components/forms/FormField';
import { userMessage } from '../../lib/apiError';
import { useItems, useVariants } from '../catalog/queries';
import { PricingType, type Item, type ItemVariant } from '../catalog/types';
import { formatPeso } from '../dashboard/format';
import { posApi } from './api';
import { usePosStore } from './posStore';
import { PaymentMethod, TransactionStatus, type Adjustment } from './types';

/** The only ways an exchange difference can be settled today (the server rejects the rest). */
const SETTLEMENT_OPTIONS = [
  { value: PaymentMethod.Cash, label: 'Cash' },
  { value: PaymentMethod.BankTransfer, label: 'Bank transfer' },
  { value: PaymentMethod.ManualGcashQr, label: 'GCash' },
] as const;

interface Replacement {
  key: string;
  itemId: string;
  itemVariantId: string | null;
  name: string;
  detail: string | null;
  unitPrice: number;
  quantity: number;
}

function variantLabel(variant: ItemVariant) {
  return Object.values(variant.attributes).join(', ') || variant.sku || 'Variant';
}

/**
 * Exchange against a completed sale: some of its lines come back at the price they sold for, other items
 * go out at today's price, and the difference is settled. The totals shown here are only a preview for the
 * cashier; the server prices and validates everything and always needs a manager or admin PIN.
 */
export function ExchangePage() {
  const receipt = usePosStore((s) => s.receipt);
  const { data: items = [] } = useItems();

  const [returns, setReturns] = useState<Record<string, number>>({});
  const [replacements, setReplacements] = useState<Replacement[]>([]);
  const [search, setSearch] = useState('');
  const [pickingVariantFor, setPickingVariantFor] = useState<Item | null>(null);
  const [settlement, setSettlement] = useState<PaymentMethod>(PaymentMethod.Cash);
  const [tendered, setTendered] = useState('');
  const [reason, setReason] = useState('');
  const [pin, setPin] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<Adjustment | null>(null);

  const returnedTotal = useMemo(
    () => (receipt?.lines ?? []).reduce((sum, line) => sum + (returns[line.id] ?? 0) * line.unitPrice, 0),
    [receipt, returns],
  );
  const replacementTotal = replacements.reduce((sum, r) => sum + r.unitPrice * r.quantity, 0);
  const difference = replacementTotal - returnedTotal;

  const matches = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return [];
    return items
      .filter((i) => i.isActive && (i.pricingType === PricingType.Unit || i.pricingType === PricingType.VariantMatrix))
      .filter((i) => i.name.toLowerCase().includes(term) || i.sku?.toLowerCase() === term || i.barcode === term)
      .slice(0, 6);
  }, [items, search]);

  if (!receipt || receipt.status !== TransactionStatus.Completed) return <Navigate to="/sell" replace />;

  const hasReturn = Object.values(returns).some((q) => q > 0);
  const owed = difference > 0;
  const needsSettlement = difference !== 0;
  const tenderedAmount = Number(tendered);
  const cashShort = owed && settlement === PaymentMethod.Cash && !(tenderedAmount >= difference);
  const ready = hasReturn && replacements.length > 0 && reason.trim() && pin.trim() && !cashShort && !busy;

  function setReturnQuantity(lineId: string, quantity: number) {
    setReturns((current) => ({ ...current, [lineId]: quantity }));
  }

  function addReplacement(item: Item, variant: ItemVariant | null) {
    const key = `${item.id}:${variant?.id ?? ''}`;
    setReplacements((current) => {
      const existing = current.find((r) => r.key === key);
      if (existing) return current.map((r) => (r.key === key ? { ...r, quantity: r.quantity + 1 } : r));
      return [
        ...current,
        {
          key,
          itemId: item.id,
          itemVariantId: variant?.id ?? null,
          name: item.name,
          detail: variant ? variantLabel(variant) : null,
          unitPrice: variant?.priceOverride ?? item.basePrice,
          quantity: 1,
        },
      ];
    });
    setSearch('');
    setPickingVariantFor(null);
  }

  function chooseItem(item: Item) {
    if (item.pricingType === PricingType.VariantMatrix) {
      setPickingVariantFor(item);
    } else {
      addReplacement(item, null);
    }
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!ready || !receipt) return;
    setBusy(true);
    setError(null);
    try {
      const result = await posApi.exchange(receipt.id, {
        returnLines: receipt.lines.filter((l) => (returns[l.id] ?? 0) > 0).map((l) => ({ originalLineId: l.id, quantity: returns[l.id] })),
        replacementLines: replacements.map((r) => ({ itemId: r.itemId, itemVariantId: r.itemVariantId, quantity: r.quantity })),
        reason: reason.trim(),
        approverPin: pin.trim(),
        ...(needsSettlement ? { settlementMethod: settlement } : {}),
        ...(owed && settlement === PaymentMethod.Cash ? { settlementAmountTendered: tenderedAmount } : {}),
      });
      setDone(result);
    } catch (submitError) {
      // The server's own words (wrong PIN, nothing left to return, an item that can't be exchanged yet)
      // are the useful ones, so they stay on the page with every field still filled in.
      setError(userMessage(submitError));
    } finally {
      setBusy(false);
    }
  }

  if (done) return <ExchangeDone adjustment={done} />;

  return (
    <form onSubmit={submit} className="flex max-w-2xl flex-col gap-8 pb-12">
      <div className="flex flex-col gap-1">
        <Link to="/sell/receipt" className="text-base font-semibold text-brand-strong underline">
          Back to receipt
        </Link>
        <h1 className="text-3xl font-bold tracking-tight">Exchange receipt No. {receipt.receiptNumber ?? 'pending'}</h1>
        <p className="text-base text-ink-soft">Choose what comes back, then what the customer takes instead.</p>
      </div>

      <section aria-labelledby="coming-back" className="flex flex-col gap-3">
        <h2 id="coming-back" className="text-xl font-bold">
          Coming back
        </h2>
        <ul className="divide-y divide-line rounded-panel border border-line bg-surface">
          {receipt.lines.map((line) => {
            const quantity = returns[line.id] ?? 0;
            return (
              <li key={line.id} className="flex items-center justify-between gap-4 px-4 py-3">
                <div className="min-w-0">
                  <p className="truncate text-base font-semibold">{line.itemName}</p>
                  <p className="text-sm text-ink-soft">
                    Bought {line.quantity} at {formatPeso(line.unitPrice)}
                  </p>
                </div>
                <Stepper label={`Return quantity for ${line.itemName}`} value={quantity} max={line.quantity} onChange={(q) => setReturnQuantity(line.id, q)} />
              </li>
            );
          })}
        </ul>
      </section>

      <section aria-labelledby="taking-instead" className="flex flex-col gap-3">
        <h2 id="taking-instead" className="text-xl font-bold">
          Taking instead
        </h2>
        <FormField label="Find an item" hint="Plain items and variants only. Combos and weighed items can't be exchanged yet.">
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} className={controlClass} placeholder="Name, SKU or barcode" />
        </FormField>

        {pickingVariantFor && <VariantChooser item={pickingVariantFor} onChoose={(variant) => addReplacement(pickingVariantFor, variant)} onCancel={() => setPickingVariantFor(null)} />}

        {!pickingVariantFor && search.trim() && (
          <ul className="flex flex-col gap-2">
            {matches.length === 0 && <li className="text-base text-ink-soft">No exchangeable item matches &ldquo;{search.trim()}&rdquo;.</li>}
            {matches.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  onClick={() => chooseItem(item)}
                  className="flex w-full items-center justify-between rounded-control border border-line bg-surface px-4 py-3 text-left hover:border-brand"
                >
                  <span className="text-base font-semibold">{item.name}</span>
                  <span className="text-base tabular-nums text-ink-soft">
                    {item.pricingType === PricingType.VariantMatrix ? 'Choose a variant' : formatPeso(item.basePrice)}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}

        {replacements.length === 0 ? (
          <p className="rounded-panel border border-dashed border-line px-4 py-6 text-base text-ink-soft">Nothing added yet. Search above to add what the customer is taking.</p>
        ) : (
          <ul className="divide-y divide-line rounded-panel border border-line bg-surface">
            {replacements.map((r) => (
              <li key={r.key} className="flex items-center justify-between gap-4 px-4 py-3">
                <div className="min-w-0">
                  <p className="truncate text-base font-semibold">{r.name}</p>
                  <p className="text-sm text-ink-soft">
                    {r.detail ? `${r.detail}, ` : ''}
                    {formatPeso(r.unitPrice)} each
                  </p>
                </div>
                <Stepper
                  label={`Quantity of ${r.name}`}
                  value={r.quantity}
                  min={0}
                  onChange={(q) => setReplacements((current) => (q === 0 ? current.filter((x) => x.key !== r.key) : current.map((x) => (x.key === r.key ? { ...x, quantity: q } : x))))}
                />
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-label="Difference" className="flex flex-col gap-1 rounded-panel border border-line bg-surface p-5" aria-live="polite">
        <dl className="flex flex-col gap-1 text-base">
          <div className="flex justify-between">
            <dt className="text-ink-soft">Coming back</dt>
            <dd className="tabular-nums">{formatPeso(returnedTotal)}</dd>
          </div>
          <div className="flex justify-between">
            <dt className="text-ink-soft">Taking instead</dt>
            <dd className="tabular-nums">{formatPeso(replacementTotal)}</dd>
          </div>
          <div className="mt-1 flex items-baseline justify-between border-t border-line pt-3 text-2xl font-bold">
            <dt>{!hasReturn || replacements.length === 0 ? 'Difference' : difference === 0 ? 'Even exchange' : owed ? 'Customer pays' : 'Refund to customer'}</dt>
            <dd className="tabular-nums">{formatPeso(Math.abs(difference))}</dd>
          </div>
        </dl>
        <p className="text-sm text-ink-soft">A preview. The server prices the exchange and confirms the final amounts.</p>
      </section>

      {needsSettlement && hasReturn && replacements.length > 0 && (
        <section aria-labelledby="settle" className="flex flex-col gap-3">
          <h2 id="settle" className="text-xl font-bold">
            {owed ? 'How is the customer paying?' : 'How is the refund given?'}
          </h2>
          <div role="radiogroup" aria-labelledby="settle" className="flex flex-wrap gap-2">
            {SETTLEMENT_OPTIONS.map((option) => (
              <button
                key={option.value}
                type="button"
                role="radio"
                aria-checked={settlement === option.value}
                onClick={() => setSettlement(option.value)}
                className={`h-12 rounded-control border px-5 text-base font-semibold ${settlement === option.value ? 'border-brand bg-brand text-on-brand' : 'border-line bg-surface hover:border-brand'}`}
              >
                {option.label}
              </button>
            ))}
          </div>
          {owed && settlement === PaymentMethod.Cash && (
            <FormField label="Cash received" error={tendered && cashShort ? `Needs at least ${formatPeso(difference)}.` : undefined}>
              <input type="text" inputMode="decimal" value={tendered} onChange={(e) => setTendered(e.target.value)} className={controlClass} />
            </FormField>
          )}
          {owed && settlement === PaymentMethod.Cash && tenderedAmount >= difference && (
            <p className="text-base font-semibold">Change: {formatPeso(tenderedAmount - difference)}</p>
          )}
        </section>
      )}

      <section aria-labelledby="approval" className="flex flex-col gap-3">
        <h2 id="approval" className="text-xl font-bold">
          Approval
        </h2>
        <FormField label="Reason">
          <textarea rows={2} value={reason} onChange={(e) => setReason(e.target.value)} className={`${controlClass} h-auto py-2`} />
        </FormField>
        <FormField label="Manager or admin PIN">
          <input type="password" inputMode="numeric" autoComplete="off" value={pin} onChange={(e) => setPin(e.target.value)} className={`${controlClass} tracking-[0.3em]`} />
        </FormField>
        {error && (
          <p role="alert" className="text-base font-medium text-danger">
            {error}
          </p>
        )}
        <PrimaryButton type="submit" busy={busy} disabled={!ready}>
          {busy ? 'Recording...' : 'Record exchange'}
        </PrimaryButton>
      </section>
    </form>
  );
}

function Stepper({ label, value, min = 0, max, onChange }: { label: string; value: number; min?: number; max?: number; onChange: (value: number) => void }) {
  const button = 'flex h-11 w-11 items-center justify-center rounded-control border border-line bg-surface text-xl font-bold hover:border-brand active:translate-y-px disabled:opacity-40';
  return (
    <div role="group" aria-label={label} className="flex shrink-0 items-center gap-2">
      <button type="button" aria-label="Decrease" disabled={value <= min} onClick={() => onChange(value - 1)} className={button}>
        -
      </button>
      <span className="w-8 text-center text-lg font-bold tabular-nums" aria-live="polite">
        {value}
      </span>
      <button type="button" aria-label="Increase" disabled={max !== undefined && value >= max} onClick={() => onChange(value + 1)} className={button}>
        +
      </button>
    </div>
  );
}

function VariantChooser({ item, onChoose, onCancel }: { item: Item; onChoose: (variant: ItemVariant) => void; onCancel: () => void }) {
  const { data: variants, isLoading } = useVariants(item.id);
  return (
    <div className="flex flex-col gap-2 rounded-panel border border-line bg-surface p-4">
      <p className="text-base font-semibold">Which {item.name}?</p>
      {isLoading && <p className="text-base text-ink-soft">Loading variants...</p>}
      {variants && variants.length === 0 && <p className="text-base text-ink-soft">This item has no variants set up.</p>}
      <div className="flex flex-wrap gap-2">
        {variants?.map((variant) => (
          <button key={variant.id} type="button" onClick={() => onChoose(variant)} className="rounded-control border border-line px-4 py-2 text-base font-semibold hover:border-brand">
            {variantLabel(variant)} <span className="tabular-nums text-ink-soft">{formatPeso(variant.priceOverride ?? item.basePrice)}</span>
          </button>
        ))}
      </div>
      <button type="button" onClick={onCancel} className="self-start text-base font-semibold text-brand-strong underline">
        Cancel
      </button>
    </div>
  );
}

function ExchangeDone({ adjustment }: { adjustment: Adjustment }) {
  const owed = adjustment.priceDifference > 0;
  const headline = adjustment.priceDifference === 0 ? 'Even exchange recorded' : owed ? `Collected ${formatPeso(adjustment.priceDifference)}` : `Refund ${formatPeso(-adjustment.priceDifference)} to the customer`;
  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-bold tracking-tight text-brand-strong">Exchange recorded</h1>
        <p className="text-base text-ink-soft">
          Against receipt No. {adjustment.originalReceiptNumber ?? 'pending'}, approved by {adjustment.approvedByName}.
        </p>
      </div>
      <dl className="grid gap-4 rounded-panel border border-line bg-surface p-5 text-base sm:grid-cols-2">
        <Lines title="Came back" lines={adjustment.returnLines} />
        <Lines title="Taken instead" lines={adjustment.replacementLines} />
      </dl>
      <p className="text-2xl font-bold">{headline}</p>
      {adjustment.changeGiven !== null && adjustment.changeGiven > 0 && <p className="text-base font-semibold">Change: {formatPeso(adjustment.changeGiven)}</p>}
      <Link to="/sell/receipt" className="flex h-16 w-fit items-center rounded-control bg-brand px-8 text-xl font-bold text-on-brand hover:bg-brand-strong">
        Back to receipt
      </Link>
    </div>
  );
}

function Lines({ title, lines }: { title: string; lines: Adjustment['returnLines'] }) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="font-semibold">{title}</dt>
      {lines.map((line, index) => (
        <dd key={`${line.itemId}-${index}`} className="flex justify-between gap-3">
          <span>
            {line.itemName} x {line.quantity}
          </span>
          <span className="tabular-nums">{formatPeso(line.lineTotal)}</span>
        </dd>
      ))}
    </div>
  );
}
