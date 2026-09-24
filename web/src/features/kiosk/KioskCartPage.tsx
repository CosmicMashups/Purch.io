import { Link } from 'react-router-dom';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { userMessage } from '../../lib/apiError';
import { formatPeso } from '../dashboard/format';
import { lineDetails } from './tickets';
import { useKioskAdds, useKioskCart, useKioskRemoveLine, useKioskUpdateLine } from './queries';

const stepper = 'grid size-14 place-items-center rounded-control border border-line text-2xl font-semibold disabled:opacity-40';

export function KioskCartPage() {
  const cart = useKioskCart();
  const update = useKioskUpdateLine();
  const remove = useKioskRemoveLine();
  const adds = useKioskAdds();
  const updating = adds.pending.length > 0;
  const busy = update.isPending || remove.isPending || updating;

  return (
    <div className="flex flex-1 flex-col gap-4 p-4">
      <header className="flex items-center justify-between">
        <Link to="/kiosk/menu" className="inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
          Back to menu
        </Link>
        <h1 className="text-xl font-bold">Your order</h1>
        <span className="w-24" aria-hidden="true" />
      </header>

      {cart.isPending && <Skeleton className="h-64 w-full" />}
      {cart.isError && <ErrorState title="Your order could not be loaded" message={userMessage(cart.error)} onRetry={() => void cart.refetch()} />}

      {updating && (
        <p role="status" className="rounded-control border border-line bg-surface px-4 py-3 text-lg font-semibold">
          Adding {adds.pending.map((row) => `${row.label} x ${row.quantity}`).join(", ")}...
        </p>
      )}

      {cart.isSuccess && cart.data.lines.length === 0 && !updating && (
        <div className="flex flex-1 flex-col items-center justify-center gap-4 text-center">
          <p className="text-2xl font-bold">Your order is empty</p>
          <Link to="/kiosk/menu" className="grid h-16 place-items-center rounded-control bg-brand px-8 text-xl font-bold text-on-brand">
            Choose items
          </Link>
        </div>
      )}

      {cart.isSuccess && cart.data.lines.length > 0 && (
        <>
          <ul className="flex flex-col gap-3">
            {cart.data.lines.map((line) => {
              const details = lineDetails(line);
              return (
                <li key={line.id} className="flex flex-col gap-3 rounded-panel border border-line bg-surface p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-lg font-semibold">{line.itemName}</p>
                      {details.length > 0 && <p className="text-base text-ink-soft">{details.join(' · ')}</p>}
                      {line.appliedPromoLabel && <p className="text-sm font-semibold text-ok">{line.appliedPromoLabel}</p>}
                    </div>
                    <p className="text-lg font-bold tabular-nums">{formatPeso(line.lineTotal)}</p>
                  </div>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <button type="button" aria-label={`Fewer ${line.itemName}`} disabled={busy || line.quantity <= 1} onClick={() => update.mutate({ lineId: line.id, quantity: line.quantity - 1 })} className={stepper}>
                        −
                      </button>
                      <span className="min-w-8 text-center text-xl font-bold tabular-nums" aria-live="polite">
                        {line.quantity}
                      </span>
                      <button type="button" aria-label={`More ${line.itemName}`} disabled={busy} onClick={() => update.mutate({ lineId: line.id, quantity: line.quantity + 1 })} className={stepper}>
                        +
                      </button>
                    </div>
                    <button type="button" disabled={busy} onClick={() => remove.mutate(line.id)} className="h-14 px-3 text-base font-semibold text-danger underline disabled:opacity-40">
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
              <span className="tabular-nums">{formatPeso(cart.data.totalAmount)}</span>
            </p>
            <Link to="/kiosk/order-type" aria-disabled={updating} onClick={(e) => updating && e.preventDefault()} className={`grid h-20 place-items-center rounded-control bg-brand text-2xl font-bold text-on-brand ${updating ? "opacity-50" : ""}`}>
              Continue
            </Link>
          </div>
        </>
      )}
    </div>
  );
}
