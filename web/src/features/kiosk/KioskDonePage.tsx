import { useEffect } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useKioskStore } from './kioskStore';

/** How long the confirmation stays before the kiosk resets itself for the next customer. */
export const DONE_RESET_MS = 30_000;

export function KioskDonePage() {
  const navigate = useNavigate();
  const order = useKioskStore((s) => s.submitted);
  const setSubmitted = useKioskStore((s) => s.setSubmitted);

  function finish() {
    setSubmitted(null);
    navigate('/kiosk', { replace: true });
  }

  useEffect(() => {
    if (!order) return;
    const id = window.setTimeout(() => {
      useKioskStore.getState().setSubmitted(null);
      navigate('/kiosk', { replace: true });
    }, DONE_RESET_MS);
    return () => window.clearTimeout(id);
  }, [order, navigate]);

  if (!order) return <Navigate to="/kiosk" replace />;

  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-6 p-8 text-center">
      <h1 className="text-4xl font-extrabold tracking-tight">Order sent!</h1>
      <p className="text-xl text-ink-soft">Please take your receipt and go to the counter to pay.</p>
      <div className="w-full rounded-panel border border-line bg-surface p-8">
        <p className="text-lg font-semibold text-ink-soft">Your order number is</p>
        <p className="my-3 text-8xl font-black tabular-nums tracking-wide text-brand-strong" aria-label={`Order number ${order.kioskPrepNumber ?? 'unknown'}`}>
          {order.kioskPrepNumber ?? '—'}
        </p>
        {order.orderType && <p className="text-lg font-semibold">{order.orderType}</p>}
      </div>
      <button type="button" onClick={finish} className="h-20 w-full rounded-control bg-brand text-2xl font-bold text-on-brand">
        Start a new order
      </button>
      <p className="text-base text-ink-soft">This screen resets by itself in a few seconds.</p>
    </main>
  );
}
