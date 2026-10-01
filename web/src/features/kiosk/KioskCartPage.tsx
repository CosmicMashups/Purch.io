import { Link } from 'react-router-dom';
import { formatPeso } from '../dashboard/format';
import { toLocalTransaction, useLocalKioskCartStore } from './localCart';
import { lineDetails } from './tickets';

const stepper = 'grid size-14 place-items-center rounded-control border border-line text-2xl font-semibold disabled:opacity-40';

export function KioskCartPage() {
  const lines = useLocalKioskCartStore((s) => s.lines);
  const updateQuantity = useLocalKioskCartStore((s) => s.updateQuantity);
  const removeLine = useLocalKioskCartStore((s) => s.removeLine);
  const cart = toLocalTransaction(lines);

  return (
    <div className="flex flex-1 flex-col gap-4 p-4">
      <header className="flex items-center justify-between">
        <Link to="/kiosk/menu" className="inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
          Back to menu
        </Link>
        <h1 className="text-xl font-bold">Your order</h1>
        <span className="w-24" aria-hidden="true" />
      </header>

      {cart.lines.length === 0 && (
        <div className="flex flex-1 flex-col items-center justify-center gap-4 text-center">
          <p className="text-2xl font-bold">Your order is empty</p>
          <Link to="/kiosk/menu" className="grid h-16 place-items-center rounded-control bg-brand px-8 text-xl font-bold text-on-brand">
            Choose items
          </Link>
        </div>
      )}

      {cart.lines.length > 0 && (
        <>
          <ul className="flex flex-col gap-3">
            {cart.lines.map((line) => {
              const details = lineDetails(line);
              return (
                <li key={line.id} className="flex flex-col gap-3 rounded-panel border border-line bg-surface p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-lg font-semibold">{line.itemName}</p>
                      {details.length > 0 && <p className="text-base text-ink-soft">{details.join(' · ')}</p>}
                    </div>
                    <p className="text-lg font-bold tabular-nums">{formatPeso(line.lineTotal)}</p>
                  </div>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <button type="button" aria-label={`Fewer ${line.itemName}`} disabled={line.quantity <= 1} onClick={() => updateQuantity(line.id, line.quantity - 1)} className={stepper}>
                        −
                      </button>
                      <span className="min-w-8 text-center text-xl font-bold tabular-nums" aria-live="polite">
                        {line.quantity}
                      </span>
                      <button type="button" aria-label={`More ${line.itemName}`} onClick={() => updateQuantity(line.id, line.quantity + 1)} className={stepper}>
                        +
                      </button>
                    </div>
                    <button type="button" onClick={() => removeLine(line.id)} className="h-14 px-3 text-base font-semibold text-danger underline">
                      Remove
                    </button>
                  </div>
                </li>
              );
            })}
          </ul>

          <div className="sticky bottom-0 mt-auto flex flex-col gap-3 bg-canvas pb-4 pt-2">
            <p className="flex items-baseline justify-between text-2xl font-extrabold">
              <span>Total</span>
              <span className="tabular-nums">{formatPeso(cart.totalAmount)}</span>
            </p>
            <Link to="/kiosk/order-type" className="grid h-20 place-items-center rounded-control bg-brand text-2xl font-bold text-on-brand">
              Continue
            </Link>
          </div>
        </>
      )}
    </div>
  );
}
