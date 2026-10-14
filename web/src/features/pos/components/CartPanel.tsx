import { Pause, PlusCircle } from '@phosphor-icons/react';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApproverPinDialog } from '../../../components/ApproverPinDialog';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { formatPeso } from '../../dashboard/format';
import { vatOf } from '../../../hardware/display/channel';
import { userMessage } from '../../../lib/apiError';
import { posApi } from '../api';
import { posKeys, useApplyPromoCode, useApplySeniorPwd, useParkCart, useSetOrderType } from '../queries';
import { useTenantSettings } from '../../tenant/queries';
import { discountBase, discountMarker, hasManualDiscount, SENIOR_EXCLUSIVE } from '../rules';
import { DiscountDialog } from './DiscountDialog';
import { LineActionsMenu, LineNoteDialog } from './LineActionsMenu';
import { OpenItemDialog, type OpenItemInput } from './OpenItemDialog';
import { PriceOverrideDialog } from './PriceOverrideDialog';
import { ParkCartDialog } from './ParkCartDialog';
import { useApproverGatedAction } from '../useApproverGatedAction';
import type { PendingRow } from '../addQueue';
import type { ManualDiscountRequest, Transaction, TransactionLine } from '../types';

type LineDialog =
  | { kind: 'discount'; line: TransactionLine | null }
  | { kind: 'override'; line: TransactionLine }
  | { kind: 'note'; line: TransactionLine }
  | { kind: 'openItem' };

const ORDER_TYPES = ['Dine In', 'Take Out'] as const;

interface CartPanelProps {
  cart: Transaction;
  /** Admin or Manager. The API restricts Senior/PWD and voiding to them; this only hides what would be refused. */
  isSupervisor: boolean;
  onCheckout: () => void;
  /** Adds still on their way to the server. Shown with the device's preview price when it has one, and the cart cannot be changed or charged until they land. */
  pending?: PendingRow[];
  /** After a park lands, e.g. to close the phone-width cart overlay. */
  onParked?: () => void;
}

const stepper = 'grid size-9 place-items-center rounded-control border border-line text-lg font-semibold hover:border-brand disabled:opacity-40';

function lineDetails(line: TransactionLine): string[] {
  return [
    ...Object.values(line.itemVariantAttributes),
    ...line.comboSelections.map((c) => `${c.slotLabel}: ${c.selectedItemName}`),
    ...line.modifierSelections.map((m) => m.modifierName),
  ];
}

export function CartPanel({ cart, isSupervisor, onCheckout, pending = [], onParked }: CartPanelProps) {
  const promo = useApplyPromoCode();
  const senior = useApplySeniorPwd();
  const orderType = useSetOrderType();
  const gated = useApproverGatedAction();
  const [code, setCode] = useState('');
  const [confirmVoid, setConfirmVoid] = useState(false);
  const [gatedActionBusy, setGatedActionBusy] = useState(false);
  const [parkOpen, setParkOpen] = useState(false);
  const park = useParkCart();
  const openItemEnabled = useTenantSettings().data?.openItemEnabled === true;
  const [dialog, setDialog] = useState<LineDialog | null>(null);
  const [confirmSenior, setConfirmSenior] = useState(false);
  const seniorApplied = cart.seniorPwdDiscountApplied;
  const qc = useQueryClient();
  const pinOpen = gated.dialogProps !== null;

  const updating = pending.length > 0;
  const busy = updating || gatedActionBusy || park.isPending || promo.isPending || senior.isPending || orderType.isPending;
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

  function onPark(label: string | undefined) {
    park.mutate(label, {
      onSuccess: () => {
        setParkOpen(false);
        onParked?.();
        toast.success(label ? `Cart parked as "${label}"` : 'Cart parked');
      },
      onError: (error) => toast.error(userMessage(error)),
    });
  }

  /** Dialog actions go through the gated runner too: on a claimed kiosk order the server may ask for a PIN. The dialog closes once it lands. */
  function submitDialog(title: string, send: (approverPin?: string) => Promise<Transaction>, done: string) {
    void runGated(title, send, () => {
      setDialog(null);
      toast.success(done);
    });
  }

  /**
   * Discounts and price overrides are gated by role and permission on the server (DiscountApply, PriceOverride),
   * never by an approver PIN, so they skip the PIN runner: no PIN dialog, no retry loop.
   */
  async function submitSupervisor(send: () => Promise<Transaction>, done: string) {
    setGatedActionBusy(true);
    try {
      qc.setQueryData(posKeys.cart, await send());
      setDialog(null);
      toast.success(done);
    } catch (error) {
      toast.error(userMessage(error));
    } finally {
      setGatedActionBusy(false);
    }
  }

  function applyDiscount(line: TransactionLine | null, body: ManualDiscountRequest) {
    const removing = body.value === 0;
    void submitSupervisor(() => (line ? posApi.setLineDiscount(line.id, body) : posApi.setCartDiscount(body)), removing ? 'Discount removed' : 'Discount applied');
  }

  function addOpenItem(item: OpenItemInput) {
    submitDialog(`Add ${item.name}`, (approverPin) => posApi.addOpenItem({ ...item, approverPin }), `${item.name} added`);
  }

  function onSeniorToggle(apply: boolean) {
    if (apply && hasManualDiscount(cart)) return setConfirmSenior(true);
    senior.mutate(apply);
  }

  function onVoid() {
    setConfirmVoid(false);
    void runGated('Clear the cart', (approverPin) => posApi.voidCart(approverPin), () => toast.info('Cart cleared'));
  }

  return (
    <section aria-label="Cart" className="flex h-full min-h-0 flex-col rounded-panel border border-line bg-surface">
      <header className="flex items-center justify-between border-b border-line px-4 py-2">
        <h2 className="text-base font-bold">Cart</h2>
        <div className="flex items-center gap-1">
          {openItemEnabled && (
            <button type="button" disabled={busy} onClick={() => setDialog({ kind: 'openItem' })} className="inline-flex h-12 items-center gap-1 rounded-control px-2 text-sm font-semibold text-brand-strong hover:bg-brand-tint disabled:opacity-40">
              <PlusCircle size={20} aria-hidden="true" />
              Open item
            </button>
          )}
          {!empty && (
            <button type="button" disabled={busy} onClick={() => setConfirmVoid(true)} className="h-9 px-2 text-sm font-semibold text-danger underline disabled:opacity-40">
              Clear cart
            </button>
          )}
        </div>
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
                      <p className="text-sm font-semibold">
                        {line.isOpenItem ? (line.openItemName ?? line.itemName) : line.itemName}
                        {line.isOpenItem && <span className="ml-2 rounded-full border border-line px-2 py-0.5 text-xs font-semibold text-ink-soft">Open item</span>}
                      </p>
                      {line.note && <p className="text-xs italic text-ink-soft">{line.note}</p>}
                      {details.length > 0 && <p className="text-xs text-ink-soft">{details.join(', ')}</p>}
                      {line.appliedPromoLabel && <p className="text-xs font-semibold text-brand-strong">Promo: {line.appliedPromoLabel}</p>}
                      {line.priceOverrideFrom != null && line.priceOverrideFrom !== line.unitPrice && (
                        <p className="text-xs font-semibold text-warn">
                          {formatPeso(line.unitPrice)} each <s className="font-normal text-ink-soft">was {formatPeso(line.priceOverrideFrom)}</s>
                        </p>
                      )}
                      {discountMarker(line) && <p className="text-xs font-semibold text-warn">Discount {discountMarker(line)}</p>}
                    </div>
                    <div className="flex shrink-0 items-start gap-1">
                      <p className="pt-3 text-sm font-semibold tabular-nums">{formatPeso(line.lineTotal)}</p>
                      <LineActionsMenu
                        line={line}
                        isSupervisor={isSupervisor}
                        seniorApplied={seniorApplied}
                        disabled={busy}
                        onDiscount={() => setDialog({ kind: 'discount', line })}
                        onOverride={() => setDialog({ kind: 'override', line })}
                        onNote={() => setDialog({ kind: 'note', line })}
                      />
                    </div>
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
            onChange={(e) => onSeniorToggle(e.target.checked)}
            className="size-6 accent-brand"
          />
        </label>

        {isSupervisor && !empty && (
          seniorApplied ? (
            <p className="text-xs text-ink-soft">{SENIOR_EXCLUSIVE}</p>
          ) : (
            <button type="button" disabled={busy} onClick={() => setDialog({ kind: 'discount', line: null })} className="h-12 rounded-control border border-dashed border-warn/50 text-sm font-semibold text-warn hover:bg-warn/10 disabled:opacity-40">
              {(cart.manualDiscountAmount ?? 0) > 0 ? 'Change cart discount' : 'Add discount'}
            </button>
          )
        )}

        <dl className="flex flex-col text-sm">
          <Row label="Subtotal" value={cart.subtotal} />
          {cart.itemPromoDiscountAmount > 0 && <Row label="Item promotions" value={-cart.itemPromoDiscountAmount} />}
          {cart.promoDiscountAmount > 0 && <Row label={`Promo code ${cart.promoCode ?? ''}`.trim()} value={-cart.promoDiscountAmount} />}
          {lineDiscountTotal(cart) > 0 && <Row label="Item discounts" value={-lineDiscountTotal(cart)} />}
          {(cart.manualDiscountAmount ?? 0) > 0 && (
            <Row
              label={`Cart discount${cart.manualDiscountKind === 'Percent' && cart.manualDiscountValue ? ` ${cart.manualDiscountValue}%` : ''}${cart.manualDiscountReason ? ` (${cart.manualDiscountReason})` : ''}`}
              value={-(cart.manualDiscountAmount ?? 0)}
            />
          )}
          {cart.vatExemptAmount > 0 && <Row label="VAT exemption (12%)" value={-cart.vatExemptAmount} />}
          {/* discountAmount totals every discount on the server, manual ones included: only label it Senior/PWD when that is on (the two never combine). */}
          {cart.seniorPwdDiscountApplied && cart.discountAmount > 0 && <Row label="Senior / PWD (20%)" value={-cart.discountAmount} />}
          <Row label="VAT (12%)" value={vatOf(cart)} />
          <div className="mt-1 flex items-baseline justify-between border-t border-line pt-1 text-lg font-bold">
            <dt>Total</dt>
            <dd className="tabular-nums" data-testid="cart-total">
              {formatPeso(cart.totalAmount)}
            </dd>
          </div>
        </dl>

        <div className="grid grid-cols-[auto_minmax(0,1fr)] gap-2">
          <button
            type="button"
            disabled={empty || busy}
            onClick={() => setParkOpen(true)}
            title="Set this cart aside and serve the next customer"
            className="inline-flex h-12 items-center gap-2 rounded-control border border-line px-4 text-base font-semibold hover:border-brand active:translate-y-px disabled:opacity-40"
          >
            <Pause size={18} aria-hidden="true" />
            Park cart
          </button>
          <button
            type="button"
            disabled={empty || busy}
            onClick={onCheckout}
            className="h-12 truncate rounded-control bg-brand px-3 text-base font-bold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-50"
          >
            {updating ? 'Updating...' : `Charge ${formatPeso(cart.totalAmount)}`}
          </button>
        </div>
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

      <ConfirmModal
        open={confirmSenior}
        title="Apply Senior/PWD discount?"
        description="Senior/PWD can't be combined with a manual discount. Applying it removes the manual discounts on this cart."
        confirmLabel="Apply Senior/PWD"
        onConfirm={() => {
          setConfirmSenior(false);
          senior.mutate(true);
        }}
        onCancel={() => setConfirmSenior(false)}
      />

      {dialog?.kind === 'discount' && (
        <DiscountDialog
          subject={dialog.line ? dialog.line.itemName : 'the whole cart'}
          total={discountBase(cart, dialog.line)}
          initial={initialDiscount(dialog.line ?? cart)}
          busy={gatedActionBusy}
          onApply={(body) => applyDiscount(dialog.line, body)}
          onRemove={initialDiscount(dialog.line ?? cart) ? () => applyDiscount(dialog.line, { kind: 'Percent', value: 0, reason: 'Removed' }) : undefined}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.kind === 'override' && (
        <PriceOverrideDialog
          itemName={dialog.line.itemName}
          currentUnitPrice={dialog.line.unitPrice}
          originalUnitPrice={dialog.line.priceOverrideFrom}
          busy={gatedActionBusy}
          onApply={(body) => void submitSupervisor(() => posApi.overrideLinePrice(dialog.line.id, body), 'Price changed')}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.kind === 'note' && (
        <LineNoteDialog
          itemName={dialog.line.itemName}
          initial={dialog.line.note}
          busy={gatedActionBusy || pinOpen}
          suspended={pinOpen}
          onSave={(note) => submitDialog(`Note for ${dialog.line.itemName}`, (approverPin) => posApi.setLineNote(dialog.line.id, note, approverPin), note ? 'Note saved' : 'Note removed')}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.kind === 'openItem' && <OpenItemDialog busy={gatedActionBusy || pinOpen} suspended={pinOpen} onAdd={addOpenItem} onClose={() => setDialog(null)} />}

      {gated.dialogProps && <ApproverPinDialog open {...gated.dialogProps} />}
      {parkOpen && <ParkCartDialog open busy={park.isPending} onPark={onPark} onClose={() => setParkOpen(false)} />}
    </section>
  );
}

/** The discount already on a line or the cart, to start the dialog from; undefined when there is none. */
function initialDiscount(target: { manualDiscountAmount?: number; manualDiscountKind?: Transaction['manualDiscountKind']; manualDiscountValue?: number | null; manualDiscountReason?: string | null }) {
  if (!target.manualDiscountKind || !target.manualDiscountValue || (target.manualDiscountAmount ?? 0) <= 0) return undefined;
  return { kind: target.manualDiscountKind, value: target.manualDiscountValue, reason: target.manualDiscountReason ?? '' };
}

const lineDiscountTotal = (cart: Transaction) => cart.lines.reduce((sum, l) => sum + (l.manualDiscountAmount ?? 0), 0);

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-baseline justify-between">
      <dt className="text-ink-soft">{label}</dt>
      <dd className="tabular-nums">{formatPeso(value)}</dd>
    </div>
  );
}
