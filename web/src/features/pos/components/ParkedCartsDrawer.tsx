/*
 * Design note (Task 10, U1 POS). Layout: a drawer that slides in from the right edge over the till, not a
 * centred modal, because the cart already lives on the right and a parked cart is "another cart" in that same
 * place. Rows are at least 48px tall with full-height Resume / Discard buttons (tablet thumbs, PRODUCT.md), one
 * cart per row: name, then "when, by whom, how many" in one muted line, total right-aligned in tabular figures.
 * The way in is a "Parked" button in the cashier bar carrying a count badge, so a waiting cart is never
 * forgotten. Colour keeps its operational meaning: teal for Resume (the primary action), red only for Discard.
 * Motion is a single 200ms slide, dropped under prefers-reduced-motion. Skills run: impeccable (shape),
 * design-taste-frontend (pre-flight), ui-ux-pro-max (react + html-tailwind, dials 6/4/3, not persisted).
 */
import { ClockCounterClockwise, X } from '@phosphor-icons/react';
import { useEffect, useId, useRef, useState } from 'react';
import { ApproverPinDialog } from '../../../components/ApproverPinDialog';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState } from '../../../components/ErrorState';
import { Skeleton } from '../../../components/Skeleton';
import { relativeAge } from '../../../components/charts/scale';
import { toast } from '../../../components/feedback/toastStore';
import { useOnlineStatus } from '../../../hooks/useOnlineStatus';
import { userMessage } from '../../../lib/apiError';
import { formatPeso } from '../../dashboard/format';
import { posApi } from '../api';
import { posKeys, useParkedCarts, useResumeCart } from '../queries';
import { useApproverGatedAction } from '../useApproverGatedAction';
import type { ChangedLine, ParkedCartSummary } from '../types';
import { useQueryClient } from '@tanstack/react-query';

/** "Table 4", or "Cart abcd1234" when it was parked without a label. */
const parkedName = (cart: ParkedCartSummary): string => cart.label?.trim() || `Cart ${cart.id.slice(0, 8)}`;

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

/** One sentence for the toast after a resume: what the server repriced or dropped on the way back. */
function resumeSummary(name: string, changed: ChangedLine[] | null | undefined): string {
  const count = (reason: ChangedLine['reason']) => (changed ?? []).filter((c) => c.reason === reason).length;
  const parts = [
    count('PriceChanged') > 0 && plural(count('PriceChanged'), 'price changed', 'prices changed'),
    count('Unavailable') > 0 && `${plural(count('Unavailable'), 'unavailable item', 'unavailable items')} removed`,
    count('NotRepriced') > 0 && `${plural(count('NotRepriced'), 'line', 'lines')} kept ${count('NotRepriced') === 1 ? 'its' : 'their'} old price`,
  ].filter(Boolean);
  return parts.length === 0 ? `Resumed ${name}` : `Resumed ${name}. ${parts.join(', ')}.`;
}

interface ParkedCartsDrawerProps {
  open: boolean;
  onClose: () => void;
  /** Whether the register's own cart has items: resuming then parks it, and the copy says so. */
  cartHasItems: boolean;
}

export function ParkedCartsDrawer({ open, onClose, cartHasItems }: ParkedCartsDrawerProps) {
  const online = useOnlineStatus();
  const parked = useParkedCarts(open && online);
  const resume = useResumeCart();
  const gated = useApproverGatedAction();
  const qc = useQueryClient();
  const [confirm, setConfirm] = useState<ParkedCartSummary | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const titleId = useId();
  const closeRef = useRef<HTMLButtonElement>(null);
  const nestedOpen = confirm !== null || gated.dialogProps !== null;
  // Escape belongs to whichever dialog is on top: read through refs so the listener is registered once per open.
  const nested = useRef(nestedOpen);
  const close = useRef(onClose);
  useEffect(() => {
    nested.current = nestedOpen;
    close.current = onClose;
  });

  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    closeRef.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !nested.current) close.current();
    };
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
      previous?.focus?.();
    };
  }, [open]);

  if (!open) return null;

  function onResume(cart: ParkedCartSummary) {
    setBusyId(cart.id);
    resume.mutate(cart.id, {
      onSuccess: (resumed) => {
        const changed = resumed.changedLines ?? [];
        const message = resumeSummary(parkedName(cart), changed);
        if (changed.length > 0) toast.info(message);
        else toast.success(message);
        onClose();
      },
      onError: (error) => toast.error(userMessage(error)),
      onSettled: () => setBusyId(null),
    });
  }

  async function onDiscard(cart: ParkedCartSummary) {
    setConfirm(null);
    setBusyId(cart.id);
    try {
      await gated.run(`Discard ${parkedName(cart)}`, (approverPin) => posApi.discardParked(cart.id, approverPin), () => {
        toast.success(`Discarded ${parkedName(cart)}`);
        void qc.invalidateQueries({ queryKey: posKeys.parked });
      });
    } finally {
      setBusyId(null);
    }
  }

  const disabled = !online || resume.isPending || busyId !== null;

  return (
    <div className="fixed inset-0 z-40 flex justify-end bg-black/40" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="flex h-full w-full max-w-lg flex-col border-l border-line bg-surface shadow-xl motion-safe:animate-[drawer-in_200ms_ease-out]"
      >
        <header className="flex items-center justify-between gap-3 border-b border-line px-5 py-3">
          <div className="min-w-0">
            <h2 id={titleId} className="text-xl font-bold">
              Parked carts
            </h2>
            <p className="text-sm text-ink-soft">
              {cartHasItems ? 'Resuming a cart parks the cart you have open now.' : 'Any register in this branch can resume these.'}
            </p>
          </div>
          <button ref={closeRef} type="button" onClick={onClose} aria-label="Close parked carts" className="grid size-12 shrink-0 place-items-center rounded-control text-ink-soft hover:bg-canvas">
            <X size={22} aria-hidden="true" />
          </button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-3" aria-busy={parked.isPending && online}>
          {!online ? (
            <ErrorState title="Selling needs a connection" message="Parked carts are kept on the server, so they can be resumed once the connection returns." />
          ) : parked.isPending ? (
            <div className="flex flex-col gap-3" aria-hidden="true">
              {Array.from({ length: 3 }, (_, i) => (
                <Skeleton key={i} className="h-16 w-full" />
              ))}
            </div>
          ) : parked.isError ? (
            <ErrorState title="Parked carts could not be loaded" message={userMessage(parked.error)} onRetry={() => void parked.refetch()} />
          ) : parked.data.length === 0 ? (
            <EmptyState title="No parked carts" description="Park a cart from the cart panel to serve the next customer, then resume it here." />
          ) : (
            <ul className="divide-y divide-line" aria-label="Parked carts list">
              {parked.data.map((cart) => {
                const name = parkedName(cart);
                return (
                  <li key={cart.id} className="flex min-h-16 items-center gap-3 py-3">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-baseline justify-between gap-3">
                        <p className="line-clamp-2 min-w-0 break-words text-base font-semibold">{name}</p>
                        <p className="shrink-0 text-base font-bold tabular-nums">{formatPeso(cart.totalAmount)}</p>
                      </div>
                      <p className="text-sm text-ink-soft">
                        <time dateTime={cart.parkedAt}>{relativeAge(cart.parkedAt)}</time>
                        {cart.parkedByName ? `, by ${cart.parkedByName}` : ''}, {cart.lineCount === 0 ? 'no items' : plural(cart.lineCount, 'item', 'items')}
                      </p>
                    </div>
                    <div className="flex shrink-0 gap-2">
                      <button
                        type="button"
                        aria-label={`Discard ${name}`}
                        disabled={disabled}
                        onClick={() => setConfirm(cart)}
                        className="h-12 rounded-control px-3 text-sm font-semibold text-danger hover:bg-canvas disabled:opacity-40"
                      >
                        Discard
                      </button>
                      <button
                        type="button"
                        aria-label={`Resume ${name}`}
                        disabled={disabled}
                        onClick={() => onResume(cart)}
                        className="inline-flex h-12 items-center gap-2 rounded-control bg-brand px-4 text-sm font-bold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-50"
                      >
                        <ClockCounterClockwise size={18} aria-hidden="true" />
                        {busyId === cart.id && resume.isPending ? 'Resuming...' : 'Resume'}
                      </button>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </div>

      <ConfirmModal
        open={confirm !== null}
        destructive
        title={confirm ? `Discard ${parkedName(confirm)}?` : ''}
        description={
          confirm && confirm.totalAmount > 0
            ? `Its ${plural(confirm.lineCount, 'item', 'items')} (${formatPeso(confirm.totalAmount)}) are thrown away. A manager or admin PIN may be needed.`
            : 'This empty parked cart is removed.'
        }
        confirmLabel="Discard"
        onConfirm={() => confirm && void onDiscard(confirm)}
        onCancel={() => setConfirm(null)}
      />
      {gated.dialogProps && <ApproverPinDialog open {...gated.dialogProps} />}
    </div>
  );
}
