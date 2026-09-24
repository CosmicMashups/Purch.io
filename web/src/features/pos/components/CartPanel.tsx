import { useState } from 'react';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { formatPeso } from '../../dashboard/format';
import { useApplyPromoCode, useApplySeniorPwd, useRemoveLine, useSetOrderType, useUpdateLine, useVoidCart } from '../queries';
import type { PendingRow } from '../addQueue';
import type { Transaction, TransactionLine } from '../types';

const ORDER_TYPES = ['Dine In', 'Take Out'] as const;

interface CartPanelProps {
  cart: Transaction;
  /** Admin or Manager. The API restricts Senior/PWD and voiding to them; this only hides what would be refused. */
  isSupervisor: boolean;
  onCheckout: () => void;
  /** Adds still on their way to the server. Shown as rows without a price, and the cart cannot be changed or charged until they land. */
  pending?: PendingRow[];
}

const stepper = 'grid size-12 place-items-center rounded-control border border-line text-xl font-semibold hover:border-brand disabled:opacity-40';

function lineDetails(line: TransactionLine): string[] {
  return [
    ...Object.values(line.itemVariantAttributes),
    ...line.comboSelections.map((c) => `${c.slotLabel}: ${c.selectedItemName}`),
    ...line.modifierSelections.map((m) => m.modifierName),
  ];
}

export function CartPanel({ cart, isSupervisor, onCheckout, pending = [] }: CartPanelProps) {
  const update = useUpdateLine();
  const remove = useRemoveLine();
  const promo = useApplyPromoCode();
  const senior = useApplySeniorPwd();
  const orderType = useSetOrderType();
  const voidCart = useVoidCart();
  const [code, setCode] = useState('');
  const [confirmVoid, setConfirmVoid] = useState(false);

  const updating = pending.length > 0;
  const busy = updating || update.isPending || remove.isPending || promo.isPending || senior.isPending || orderType.isPending || voidCart.isPending;
  const empty = cart.lines.length === 0;

  function applyCode(event: React.FormEvent) {
    event.preventDefault();
    const trimmed = code.trim();
    if (!trimmed) return;
    promo.mutate(trimmed.toUpperCase(), { onSuccess: () => setCode('') });
  }

  function onVoid() {
    setConfirmVoid(false);
    voidCart.mutate(undefined, { onSuccess: () => toast.info('Cart cleared') });
  }

  return (
    <section aria-label="Cart" className="flex h-full min-h-0 flex-col rounded-panel border border-line bg-surface">
      <header className="flex items-center justify-between border-b border-line px-5 py-4">
        <h2 className="text-xl font-bold">Cart</h2>
        {isSupervisor && !empty && (
          <button type="button" disabled={busy} onClick={() => setConfirmVoid(true)} className="h-12 px-3 text-base font-semibold text-danger underline disabled:opacity-40">
            Clear cart
          </button>
        )}
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto px-5">
        {empty && !updating ? (
          <p className="py-10 text-center text-base text-ink-soft">The cart is empty. Tap an item to add it.</p>
        ) : (
          <ul className="divide-y divide-line">
            {cart.lines.map((line) => {
              const details = lineDetails(line);
              const whole = Number.isInteger(line.quantity);
              return (
                <li key={line.id} className="py-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="text-base font-semibold">{line.itemName}</p>
                      {details.length > 0 && <p className="text-sm text-ink-soft">{details.join(', ')}</p>}
                      {line.appliedPromoLabel && <p className="text-sm font-semibold text-brand-strong">Promo: {line.appliedPromoLabel}</p>}
                    </div>
                    <p className="shrink-0 text-base font-semibold tabular-nums">{formatPeso(line.lineTotal)}</p>
                  </div>
                  <div className="mt-2 flex items-center justify-between gap-3">
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        aria-label={`Decrease ${line.itemName}`}
                        className={stepper}
                        disabled={busy || !whole || line.quantity <= 1}
                        onClick={() => update.mutate({ lineId: line.id, quantity: line.quantity - 1 })}
                      >
                        -
                      </button>
                      <span className="min-w-10 text-center text-lg font-bold tabular-nums">{line.quantity}</span>
                      <button
                        type="button"
                        aria-label={`Increase ${line.itemName}`}
                        className={stepper}
                        disabled={busy || !whole}
                        onClick={() => update.mutate({ lineId: line.id, quantity: line.quantity + 1 })}
                      >
                        +
                      </button>
                    </div>
                    <button type="button" disabled={busy} onClick={() => remove.mutate(line.id)} className="h-12 px-2 text-base font-semibold text-danger underline disabled:opacity-40">
                      Remove
                    </button>
                  </div>
                </li>
              );
            })}
            {pending.map((row) => (
              <li key={row.key} aria-label={`Adding ${row.label}`} className="flex items-center justify-between gap-3 py-4 text-ink-soft">
                <p className="text-base font-semibold">
                  {row.label} x {row.quantity}
                </p>
                <p className="text-sm">Adding...</p>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="flex flex-col gap-4 border-t border-line px-5 py-4">
        <div role="group" aria-label="Order type" className="grid grid-cols-2 gap-2">
          {ORDER_TYPES.map((type) => (
            <button
              key={type}
              type="button"
              aria-pressed={cart.orderType === type}
              disabled={busy}
              onClick={() => orderType.mutate(type)}
              className={`h-12 rounded-control text-base font-semibold ${cart.orderType === type ? 'bg-brand text-on-brand' : 'border border-line hover:border-brand'}`}
            >
              {type}
            </button>
          ))}
        </div>

        {cart.promoCode ? (
          <div className="flex items-center justify-between gap-3 rounded-control bg-brand-tint px-4 py-2">
            <p className="text-base">
              <span className="font-mono font-semibold">{cart.promoCode}</span>
              {cart.promoDiscountAmount > 0 ? '' : ' saved, no extra discount right now'}
            </p>
            <button type="button" disabled={busy} onClick={() => promo.mutate(null)} className="h-12 text-base font-semibold underline">
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
              className="h-12 min-w-0 flex-1 rounded-control border border-ink-soft/40 bg-surface px-3 text-base uppercase"
            />
            <button type="submit" disabled={busy || !code.trim()} className="h-12 rounded-control border border-line px-5 text-base font-semibold hover:border-brand disabled:opacity-40">
              Apply
            </button>
          </form>
        )}

        <label className={`flex min-h-12 items-center justify-between gap-3 text-base font-semibold ${isSupervisor ? '' : 'opacity-60'}`}>
          <span>
            Senior / PWD discount
            {!isSupervisor && <span className="block text-sm font-normal text-ink-soft">A manager applies this</span>}
          </span>
          <input
            type="checkbox"
            role="switch"
            checked={cart.seniorPwdDiscountApplied}
            disabled={busy || !isSupervisor}
            onChange={(e) => senior.mutate(e.target.checked)}
            className="size-7 accent-brand"
          />
        </label>

        <dl className="flex flex-col gap-1 text-base">
          <Row label="Subtotal" value={cart.subtotal} />
          {cart.itemPromoDiscountAmount > 0 && <Row label="Item promotions" value={-cart.itemPromoDiscountAmount} />}
          {cart.promoDiscountAmount > 0 && <Row label={`Promo code ${cart.promoCode ?? ''}`.trim()} value={-cart.promoDiscountAmount} />}
          {cart.discountAmount > 0 && <Row label="Senior / PWD" value={-cart.discountAmount} />}
          <div className="mt-1 flex items-baseline justify-between border-t border-line pt-2 text-2xl font-bold">
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
          className="h-16 rounded-control bg-brand text-xl font-bold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-50"
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
