import { CheckCircle, ForkKnife, Printer, ShoppingBag, Wallet } from '@phosphor-icons/react';
import { useEffect, useRef } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { kioskSlipReady, useHardwareConfig } from '../../hardware/config';
import { primaryButton, secondaryButton } from './KioskActionBar';
import { useSlipPrinter } from './useSlipPrinter';
import { useKioskStore } from './kioskStore';
import { slipFromOrder } from './slipData';
import { paymentSummary } from './tickets';
import { useBusinessName } from './useBusinessName';

/** How long the confirmation stays before the kiosk resets itself for the next customer. */
export const DONE_RESET_MS = 30_000;

const RING_RADIUS = 26;
const RING_LENGTH = 2 * Math.PI * RING_RADIUS;

/** The hand-off: the order number to carry to the counter, how they said they would pay, and a ring that shows the screen resetting for the next customer. */
export function KioskDonePage() {
  const navigate = useNavigate();
  const order = useKioskStore((s) => s.submitted);
  const setSubmitted = useKioskStore((s) => s.setSubmitted);
  const checkout = useKioskStore((s) => s.checkout);
  const slipOn = useHardwareConfig((s) => kioskSlipReady(s));
  const businessName = useBusinessName();
  const printer = useSlipPrinter();
  const printed = useRef(false);

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

  // The order is already sent by now, so a printer that fails only costs the slip, never the order.
  const { print } = printer;
  useEffect(() => {
    if (!order || !slipOn || printed.current) return;
    printed.current = true;
    print(slipFromOrder(order, businessName, { orderType: checkout.orderType, payment: checkout.payment, discountHint: checkout.discountHint }));
  }, [order, slipOn, print, businessName, checkout]);

  if (!order) return <Navigate to="/kiosk" replace />;

  const type = order.orderType ?? checkout.orderType;
  const payment = paymentSummary(order.kioskPaymentPreference ?? checkout.payment, order.kioskDiscountHint ?? checkout.discountHint);

  return (
    <main className="kiosk-scroll flex h-full flex-col items-center justify-center gap-8 overflow-y-auto p-8 text-center">
      <div className="flex flex-col items-center gap-2">
        <CheckCircle size={72} weight="fill" className="text-ok" aria-hidden="true" />
        <h1 className="text-5xl font-extrabold tracking-tight">Order sent!</h1>
      </div>

      <section className="flex w-full max-w-3xl flex-col items-center gap-3 rounded-panel border-2 border-brand bg-brand-tint px-8 py-8" aria-label="Your order number">
        <p className="text-2xl font-semibold text-ink-soft">Your order number</p>
        <p className="text-8xl font-black tabular-nums leading-none tracking-wide text-brand-strong" aria-label={`Order number ${order.kioskPrepNumber ?? 'unknown'}`}>
          {order.kioskPrepNumber ?? '-'}
        </p>
        {(type || payment) && (
          <ul className="mt-2 flex flex-wrap items-center justify-center gap-3">
            {type && (
              <li className="inline-flex items-center gap-2 rounded-full bg-surface px-5 py-2 text-xl font-bold">
                {type === 'Dine In' ? <ForkKnife size={26} weight="bold" aria-hidden="true" /> : <ShoppingBag size={26} weight="bold" aria-hidden="true" />}
                {type}
              </li>
            )}
            {payment && (
              <li className="inline-flex items-center gap-2 rounded-full bg-surface px-5 py-2 text-xl font-bold">
                <Wallet size={26} weight="bold" aria-hidden="true" />
                {payment}
              </li>
            )}
          </ul>
        )}
      </section>

      <div className="flex flex-col items-center gap-1">
        <p className="text-4xl font-extrabold tracking-tight">Pay at the counter and enjoy!</p>
        <p className="text-2xl text-ink-soft">{slipOn ? 'Take your order slip to the counter.' : 'Show your order number at the counter.'}</p>
      </div>

      <div className="flex flex-col items-center gap-4">
        <div className="flex flex-wrap justify-center gap-3">
          {slipOn && (
            <button
              type="button"
              className={secondaryButton}
              onClick={() => print(slipFromOrder(order, businessName, { orderType: checkout.orderType, payment: checkout.payment, discountHint: checkout.discountHint }))}
            >
              <Printer size={26} weight="bold" aria-hidden="true" />
              Print again
            </button>
          )}
          <button type="button" onClick={finish} className={`${primaryButton} min-w-80`}>
            Start a new order
          </button>
        </div>
        <p className="flex items-center gap-3 text-lg text-ink-soft">
          <svg viewBox="0 0 64 64" className="size-10 -rotate-90" aria-hidden="true">
            <circle cx="32" cy="32" r={RING_RADIUS} fill="none" stroke="var(--line)" strokeWidth="6" />
            <circle
              cx="32"
              cy="32"
              r={RING_RADIUS}
              fill="none"
              stroke="var(--brand)"
              strokeWidth="6"
              strokeLinecap="round"
              strokeDasharray={RING_LENGTH}
              style={{ ['--ring-length' as string]: RING_LENGTH, animation: `kiosk-countdown ${DONE_RESET_MS}ms linear forwards` }}
            />
          </svg>
          This screen resets by itself for the next customer.
        </p>
      </div>
      {printer.portal}
    </main>
  );
}
