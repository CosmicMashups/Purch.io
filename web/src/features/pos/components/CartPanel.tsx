import { useState } from 'react';
import { ApproverPinDialog } from '../../../components/ApproverPinDialog';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { formatPeso } from '../../dashboard/format';
import { vatOf } from '../../../hardware/display/channel';
import { posApi } from '../api';
import { useApplyPromoCode, useApplySeniorPwd, useSetOrderType } from '../queries';
import { useApproverGatedAction } from '../useApproverGatedAction';
import type { PendingRow } from '../addQueue';
import type { Transaction, TransactionLine } from '../types';

const ORDER_TYPES = ['Dine In', 'Take Out'] as const;

interface CartPanelProps {
  cart: Transaction;
  /** Admin or Manager. The API restricts Senior/PWD and voiding to them; this only hides what would be refused. */
  isSupervisor: boolean;
  onCheckout: () => void;
  /** Adds still on their way to the server. Shown with the device's preview price when it has one, and the cart cannot be changed or charged until they land. */
  pending?: PendingRow[];
}

const stepper = 'grid size-9 place-items-center rounded-control border border-line text-lg font-semibold hover:border-brand disabled:opacity-40';

function lineDetails(line: TransactionLine): string[] {
  return [
    ...Object.values(line.itemVariantAttributes),
    ...line.comboSelections.map((c) => `${c.slotLabel}: ${c.selectedItemName}`),
    ...line.modifierSelections.map((m) => m.modifierName),
  ];
}

export function CartPanel({ cart, isSupervisor, onCheckout, pending = [] }: CartPanelProps) {
  const promo = useApplyPromoCode();
  const senior = useApplySeniorPwd();
  const orderType = useSetOrderType();
  const gated = useApproverGatedAction();
  const [code, setCode] = useState('');
  const [confirmVoid, setConfirmVoid] = useState(false);
  const [gatedActionBusy, setGatedActionBusy] = useState(false);

  const updating = pending.length > 0;
  const busy = updating || gatedActionBusy || promo.isPending || senior.isPending || orderType.isPending;
  const empty = cart.lines.length === 0;

  function applyCode(event: React.FormEvent) {
    event.preventDefault();
    const trimmed = code.trim();
    if (!trimmed) return;
    promo.mutate(trimmed.toUpperCase(), { onSuccess: () => setCode('') });
  }

  /** Every gated action runs through here so `busy` covers it even before a PIN dialog (if any) opens. */
  async function runGated(title: string, send: (approverPin?: string) => Promise<Transaction>, onLanded?: (cart: Transaction) => void) {
    setGatedActionBusy(true);
    try {
      await gated.run(title, send, onLanded);
    } finally {
      setGatedActionBusy(false);
    }
  }

  function onVoid() {
    setConfirmVoid(false);
    void runGated('Clear the cart', (approverPin) => posApi.voidCart(approverPin), () => toast.info('Cart cleared'));
  }

  return (
    <section aria-label="Cart" className="flex h-full min-h-0 flex-col rounded-panel border border-line bg-surface">
      <header className="flex items-center justify-between border-b border-line px-4 py-2">
        <h2 className="text-base font-bold">Cart</h2>
        {!empty && (
          <button type="button" disabled={busy} onClick={() => setConfirmVoid(true)} className="h-9 px-2 text-sm font-semibold text-danger underline disabled:opacity-40">
            Clear cart
          </button>
        )}
      </header>

      <div className="min-h-24 flex-1 overflow-y-auto px-4">
        {empty && !updating ? (
          <p className="py-10 text-center text-base text-ink-soft">The cart is empty. Tap an item to add it.</p>
        ) : (
          <ul className="divide-y divide-line">
            {cart.lines.map((line) => {
              const details = lineDetails(line);
              const whole = Number.isInteger(line.quantity);
              return (
                <li key={line.id} className="py-2">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="text-sm font-semibold">{line.itemName}</p>
                      {details.length > 0 && <p className="text-xs text-ink-soft">{details.join(', ')}</p>}
                      {line.appliedPromoLabel && <p className="text-xs font-semibold text-brand-strong">Promo: {line.appliedPromoLabel}</p>}
                    </div>
                    <p className="shrink-0 text-sm font-semibold tabular-nums">{formatPeso(line.lineTotal)}</p>
                  </div>
                  <div className="mt-1 flex items-center justify-between gap-3">
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        aria-label={`Decrease ${line.itemName}`}
                        className={stepper}
                        disabled={busy || !whole || line.quantity <= 1}
                        onClick={() => void runGated(`Change the quantity of ${line.itemName}`, (approverPin) => posApi.updateLine(line.id, line.quantity - 1, approverPin))}
                      >
                        -
                      </button>
                      <span className="min-w-8 text-center text-base font-bold tabular-nums">{line.quantity}</span>
                      <button
                        type="button"
                        aria-label={`Increase ${line.itemName}`}
                        className={stepper}
                        disabled={busy || !whole}
                        onClick={() => void runGated(`Change the quantity of ${line.itemName}`, (approverPin) => posApi.updateLine(line.id, line.quantity + 1, approverPin))}
                      >
                        +
                      </button>
                    </div>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => void runGated(`Remove ${line.itemName}`, (approverPin) => posApi.removeLine(line.id, approverPin))}
                      className="h-9 px-2 text-sm font-semibold text-danger underline disabled:opacity-40"
                    >
                      Remove
                    </button>
                  </div>
                </li>
              );
            })}
            {pending.map((row) => (
              <li key={row.key} aria-label={`Adding ${row.label}`} className="flex items-center justify-between gap-3 py-2 text-ink-soft">
                <div className="min-w-0">
                  <p className="text-sm font-semibold">
                    {row.label} x {row.quantity}
                  </p>
                  {row.details && row.details.length > 0 && <p className="text-sm">{row.details.join(', ')}</p>}
                </div>
                <p className="shrink-0 text-sm tabular-nums">{row.unitPrice === undefined ? 'Adding...' : formatPeso(row.unitPrice * row.quantity)}</p>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="flex flex-col gap-2 border-t border-line px-4 py-3">
        <div role="group" aria-label="Order type" className="grid grid-cols-2 gap-2">
          {ORDER_TYPES.map((type) => (
            <button
              key={type}
              type="button"
              aria-pressed={cart.orderType === type}
              disabled={busy}
              onClick={() => orderType.mutate(type)}
              className={`h-9 rounded-control text-sm font-semibold ${cart.orderType === type ? 'bg-brand text-on-brand' : 'border border-line hover:border-brand'}`}
            >
              {type}
            </button>
          ))}
        </div>

        {cart.promoCode ? (
          <div className="flex items-center justify-between gap-3 rounded-control bg-brand-tint px-3 py-1">
            <p className="text-sm">
              <span className="font-mono font-semibold">{cart.promoCode}</span>
              {cart.promoDiscountAmount > 0 ? '' : ' saved, no extra discount right now'}
            </p>
            <button type="button" disabled={busy} onClick={() => promo.mutate(null)} className="h-9 text-sm font-semibold underline">
              Remove
            </button>
          </div>
        ) : (
          <form onSubmit={applyCode} className="flex gap-2">
            <input
              aria-label="Promo code"
              placeholder="Promo code"
              autoCapitalize="characters"
              autoComplete="off"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              className="h-9 min-w-0 flex-1 rounded-control border border-ink-soft/40 bg-surface px-3 text-sm uppercase"
            />
            <button type="submit" disabled={busy || !code.trim()} className="h-9 rounded-control border border-line px-4 text-sm font-semibold hover:border-brand disabled:opacity-40">
              Apply
            </button>
          </form>
        )}

        <label className={`flex min-h-9 items-center justify-between gap-3 text-sm font-semibold ${isSupervisor ? '' : 'opacity-60'}`}>
          <span>
            Senior / PWD discount
            {!isSupervisor && <span className="block text-xs font-normal text-ink-soft">A manager applies this</span>}
          </span>
          <input
            type="checkbox"
            role="switch"
            checked={cart.seniorPwdDiscountApplied}
            disabled={busy || !isSupervisor}
            onChange={(e) => senior.mutate(e.target.checked)}
            className="size-6 accent-brand"
          />
        </label>

        <dl className="flex flex-col text-sm">
          <Row label="Subtotal" value={cart.subtotal} />
          {cart.itemPromoDiscountAmount > 0 && <Row label="Item promotions" value={-cart.itemPromoDiscountAmount} />}
          {cart.promoDiscountAmount > 0 && <Row label={`Promo code ${cart.promoCode ?? ''}`.trim()} value={-cart.promoDiscountAmount} />}
          {cart.vatExemptAmount > 0 && <Row label="VAT exemption (12%)" value={-cart.vatExemptAmount} />}
          {cart.discountAmount > 0 && <Row label="Senior / PWD (20%)" value={-cart.discountAmount} />}
          <Row label="VAT (12%)" value={vatOf(cart)} />
          <div className="mt-1 flex items-baseline justify-between border-t border-line pt-1 text-lg font-bold">
            <dt>Total</dt>
            <dd className="tabular-nums" data-testid="cart-total">
              {formatPeso(cart.totalAmount)}
            </dd>
          </div>
        </dl>

        <button
          type="button"
          disabled={empty || busy}
          onClick={onCheckout}
          className="h-12 rounded-control bg-brand text-base font-bold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-50"
        >
          {updating ? 'Updating...' : `Charge ${formatPeso(cart.totalAmount)}`}
        </button>
      </div>

      <ConfirmModal
        open={confirmVoid}
        destructive
        title="Clear the cart?"
        description="Every item is removed and this sale starts over."
        confirmLabel="Clear cart"
        onConfirm={onVoid}
        onCancel={() => setConfirmVoid(false)}
      />

      {gated.dialogProps && <ApproverPinDialog open {...gated.dialogProps} />}
    </section>
  );
}

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-baseline justify-between">
      <dt className="text-ink-soft">{label}</dt>
      <dd className="tabular-nums">{formatPeso(value)}</dd>
    </div>
  );
}
