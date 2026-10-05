import { ArrowCounterClockwise } from '@phosphor-icons/react';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Modal } from '../../components/Modal';
import { formatPeso } from '../dashboard/format';
import { useKioskStore } from './kioskStore';
import { toLocalTransaction, useLocalKioskCartStore } from './localCart';
import { useKioskPromoRules } from './queries';

/** The strip every kiosk screen keeps pinned to its bottom edge. */
export const barClass = 'flex shrink-0 items-stretch gap-3 border-t border-line bg-surface px-5 py-4 shadow-[0_-8px_24px_-12px_rgb(15_23_42/0.18)]';

export const secondaryButton =
  'inline-flex h-16 min-w-40 items-center justify-center gap-2 rounded-control border-2 border-line bg-surface px-6 text-xl font-bold text-ink transition-transform duration-100 hover:border-brand active:translate-y-px disabled:opacity-40';

export const primaryButton =
  'inline-flex h-16 min-w-48 items-center justify-center gap-2 rounded-control bg-brand px-8 text-xl font-bold text-on-brand transition-transform duration-100 active:translate-y-px disabled:opacity-40';

/** Clears the order and goes back to the welcome screen. Asks first when there is something to lose. */
export function StartOverButton({ className = secondaryButton }: { className?: string }) {
  const navigate = useNavigate();
  const hasLines = useLocalKioskCartStore((s) => s.lines.length > 0);
  const clearCart = useLocalKioskCartStore((s) => s.clear);
  const resetCheckout = useKioskStore((s) => s.resetCheckout);
  const [asking, setAsking] = useState(false);

  function startOver() {
    clearCart();
    resetCheckout();
    navigate('/kiosk', { replace: true });
  }

  return (
    <>
      <button type="button" className={className} onClick={() => (hasLines ? setAsking(true) : startOver())}>
        <ArrowCounterClockwise size={26} weight="bold" aria-hidden="true" />
        Start Over
      </button>
      <Modal
        open={asking}
        title="Start over?"
        onClose={() => setAsking(false)}
        footer={
          <div className="flex gap-3">
            <button type="button" className={`${secondaryButton} flex-1`} onClick={() => setAsking(false)}>
              Keep my order
            </button>
            <button type="button" className={`${primaryButton} flex-1`} onClick={startOver}>
              Clear and start over
            </button>
          </div>
        }
      >
        <p className="text-lg">Your order will be cleared and the kiosk returns to the first screen.</p>
      </Modal>
    </>
  );
}

/**
 * The menu's bottom strip: the running total in a block of its own, then Start Over beside Complete Order. The total
 * is never inside the button, so the button only ever says what pressing it does.
 */
export function KioskActionBar() {
  const lines = useLocalKioskCartStore((s) => s.lines);
  const { data: rules } = useKioskPromoRules();
  const total = toLocalTransaction(lines, rules).totalAmount;
  const count = lines.reduce((sum, line) => sum + line.quantity, 0);

  return (
    <div className={`${barClass} flex-wrap`}>
      <div className="flex min-w-40 flex-1 flex-col justify-center">
        <span className="text-base font-semibold text-ink-soft">{count === 0 ? 'Your order is empty' : `Your total (${count} ${count === 1 ? 'item' : 'items'})`}</span>
        <span className="text-3xl font-extrabold tabular-nums">{formatPeso(total)}</span>
      </div>
      <StartOverButton />
      {count > 0 ? (
        <Link to="/kiosk/cart" className={primaryButton}>
          Complete Order
        </Link>
      ) : (
        <button type="button" className={primaryButton} disabled>
          Complete Order
        </button>
      )}
    </div>
  );
}
