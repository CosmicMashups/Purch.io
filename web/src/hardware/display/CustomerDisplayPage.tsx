import { useEffect, useState } from 'react';
import { formatPeso } from '../../features/dashboard/format';
import { FullscreenButton } from '../fullscreen';
import { IDLE_STATE, customerDisplaySupported, subscribeCustomerDisplay, type CustomerDisplayState } from './channel';

/**
 * A second window for the customer, dragged to the customer-facing monitor. It shows only what the till
 * sends, on the same browser, and never talks to the server or shows staff details.
 */
export function CustomerDisplayPage() {
  const [state, setState] = useState<CustomerDisplayState>(IDLE_STATE);
  const supported = customerDisplaySupported();

  useEffect(() => subscribeCustomerDisplay(setState), []);

  if (!supported) {
    return (
      <main className="grid min-h-dvh place-items-center bg-canvas p-8 text-center text-ink">
        <p className="max-w-md text-xl">This browser cannot share the order between windows. Use a current version of Chrome, Edge or Firefox.</p>
      </main>
    );
  }

  return (
    <main className="flex min-h-dvh flex-col gap-6 bg-canvas p-8 text-ink">
      <div className="flex justify-end">
        <FullscreenButton />
      </div>

      {state.mode === 'idle' && (
        <div className="grid flex-1 place-items-center text-center">
          <h1 className="text-6xl font-extrabold tracking-tight">Welcome!</h1>
        </div>
      )}

      {(state.mode === 'cart' || state.mode === 'payment') && (
        <>
          <h1 className="text-3xl font-bold">{state.mode === 'payment' ? 'Amount to pay' : 'Your order'}</h1>
          <ul className="flex flex-1 flex-col gap-3">
            {state.lines.map((line, i) => (
              <li key={i} className="flex items-baseline justify-between gap-4 text-3xl">
                <span>
                  {line.name}
                  {line.quantity !== 1 && <span className="text-ink-soft"> × {line.quantity}</span>}
                </span>
                <span className="tabular-nums">{formatPeso(line.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <Totals state={state} />
        </>
      )}

      {state.mode === 'completed' && (
        <div className="grid flex-1 place-items-center text-center">
          <div className="flex flex-col gap-6">
            <h1 className="text-6xl font-extrabold tracking-tight">Thank you!</h1>
            {state.change !== null && state.change > 0 && (
              <p className="text-4xl">
                Your change: <span className="font-bold tabular-nums">{formatPeso(state.change)}</span>
              </p>
            )}
          </div>
        </div>
      )}
    </main>
  );
}

function Totals({ state }: { state: CustomerDisplayState }) {
  return (
    <div className="flex flex-col gap-2 border-t border-line pt-4 text-2xl">
      {state.savings.map((s) => (
        <p key={s.label} className="flex justify-between text-ok">
          <span>{s.label}</span>
          <span className="tabular-nums">−{formatPeso(s.amount)}</span>
        </p>
      ))}
      <p className="flex items-baseline justify-between text-6xl font-extrabold">
        <span>Total</span>
        <span className="tabular-nums">{formatPeso(state.total)}</span>
      </p>
    </div>
  );
}
