import { WarningCircle } from '@phosphor-icons/react';
import { useEffect, useRef, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ApiError, userMessage } from '../../lib/apiError';
import { barClass, primaryButton, secondaryButton } from './KioskActionBar';
import { useKioskStore } from './kioskStore';
import { useLocalKioskCartStore } from './localCart';
import { buildPlaceOrderRequest, orderFingerprint } from './orderRequest';
import { usePlaceKioskOrder } from './queries';

/** The animation always plays this long, so a fast answer does not flash by unread. */
export const MIN_PROCESSING_MS = 1_200;
/** After this long the screen says it is still working. */
export const SLOW_AFTER_MS = 8_000;
/** After this long the attempt is dropped and the customer can try again. */
export const GIVE_UP_AFTER_MS = 20_000;
/** From the third failed try the screen also points to a member of staff. */
const ASK_STAFF_AFTER = 3;

interface Failure {
  message: string;
  /** Sending the same order again could work (the connection dropped). False when the order itself has to change. */
  retryable: boolean;
}

function describe(error: unknown, timedOut: boolean): Failure {
  if (timedOut) return { message: 'Sending is taking longer than expected.', retryable: true };
  if (error instanceof ApiError && (error.kind === 'validation' || error.kind === 'conflict' || error.kind === 'notFound')) {
    return { message: userMessage(error) || 'Something in your order is no longer available.', retryable: false };
  }
  return { message: error instanceof ApiError ? userMessage(error) : 'We could not send your order.', retryable: true };
}

/**
 * Where the order is really sent to the counter. It shows a working state for as long as the server needs, never a fake
 * timer, and a failure keeps the cart so the customer can try again: the retry carries the same order id, so an order the
 * server did receive is never sent twice.
 */
export function KioskProcessingPage() {
  const navigate = useNavigate();
  const lines = useLocalKioskCartStore((s) => s.lines);
  const clearCart = useLocalKioskCartStore((s) => s.clear);
  const checkout = useKioskStore((s) => s.checkout);
  const orderIdFor = useKioskStore((s) => s.orderIdFor);
  const setSubmitted = useKioskStore((s) => s.setSubmitted);
  const placeOrder = usePlaceKioskOrder();

  const [failure, setFailure] = useState<Failure | null>(null);
  const [slow, setSlow] = useState(false);
  const [failures, setFailures] = useState(0);
  const started = useRef(false);
  const finished = useRef(false);
  const timers = useRef<number[]>([]);
  const sending = failure === null;

  function send() {
    const startedAt = performance.now();
    const controller = new AbortController();
    let timedOut = false;
    setFailure(null);
    setSlow(false);
    timers.current.push(
      window.setTimeout(() => setSlow(true), SLOW_AFTER_MS),
      window.setTimeout(() => {
        timedOut = true;
        controller.abort();
      }, GIVE_UP_AFTER_MS),
    );

    const orderId = orderIdFor(orderFingerprint(lines, checkout));
    placeOrder.mutate(
      { body: buildPlaceOrderRequest(orderId, lines, checkout), signal: controller.signal },
      {
        onSuccess: (order) => {
          finished.current = true;
          timers.current.push(
            window.setTimeout(() => {
              clearCart();
              setSubmitted(order);
              navigate('/kiosk/done', { replace: true });
            }, Math.max(0, MIN_PROCESSING_MS - (performance.now() - startedAt))),
          );
        },
        onError: (error) => {
          setFailures((count) => count + 1);
          setFailure(describe(error, timedOut));
        },
      },
    );
  }

  // Nothing to send (an empty cart, or the choices were never made): this screen only redirects.
  const nothingToSend = lines.length === 0 || !checkout.orderType || !checkout.payment;

  useEffect(() => {
    if (started.current || nothingToSend) return;
    started.current = true;
    send();
    // Sends once when the screen opens; retries are the buttons below.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(
    () => () => {
      timers.current.forEach((id) => window.clearTimeout(id));
    },
    [],
  );

  // While the order is in flight, going back or refreshing would leave the customer unsure whether it was sent.
  useEffect(() => {
    if (!sending) return;
    const hold = () => window.history.pushState(null, '', window.location.href);
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    hold();
    window.addEventListener('popstate', hold);
    window.addEventListener('beforeunload', warn);
    return () => {
      window.removeEventListener('popstate', hold);
      window.removeEventListener('beforeunload', warn);
    };
  }, [sending]);

  if (!finished.current && !started.current && nothingToSend) {
    return <Navigate to={lines.length === 0 ? '/kiosk/menu' : '/kiosk/payment'} replace />;
  }

  if (failure) {
    return (
      <div className="flex h-full flex-col">
        <main className="grid min-h-0 flex-1 place-items-center overflow-y-auto p-8 text-center">
          <div className="flex max-w-2xl flex-col items-center gap-5">
            <WarningCircle size={96} weight="duotone" className="text-warn" aria-hidden="true" />
            <h1 className="text-4xl font-extrabold tracking-tight">We could not send your order</h1>
            <p role="alert" className="text-2xl text-ink-soft">
              {failure.message}
            </p>
            {failure.retryable ? <p className="text-xl text-ink-soft">Your order is safe. Please try again.</p> : <p className="text-xl text-ink-soft">Please review your order and try again.</p>}
            {failures >= ASK_STAFF_AFTER && <p className="text-xl font-semibold">Still not working? Please ask a member of staff for help.</p>}
          </div>
        </main>
        <div className={`${barClass} justify-end`}>
          <button type="button" className={secondaryButton} onClick={() => navigate('/kiosk/payment', { replace: true })}>
            Back to payment
          </button>
          {failure.retryable ? (
            <button type="button" className={primaryButton} onClick={send}>
              Try again
            </button>
          ) : (
            <button type="button" className={primaryButton} onClick={() => navigate('/kiosk/cart', { replace: true })}>
              Review my order
            </button>
          )}
        </div>
      </div>
    );
  }

  return (
    <main className="grid h-full place-items-center p-8 text-center" aria-busy="true">
      <div className="flex flex-col items-center gap-8">
        <div className="flex h-20 items-end gap-2.5" aria-hidden="true">
          {[0, 1, 2].map((bar) => (
            <span key={bar} className="h-full w-5 origin-bottom rounded-full bg-brand motion-safe:animate-[kiosk-bar_1s_ease-in-out_infinite]" style={{ animationDelay: `${bar * 140}ms` }} />
          ))}
        </div>
        <div className="flex flex-col gap-2">
          <h1 role="status" className="text-4xl font-extrabold tracking-tight">
            {slow ? 'Still sending your order' : 'Sending your order'}
          </h1>
          <p className="text-2xl text-ink-soft">{slow ? 'Thank you for waiting. Please do not leave the kiosk.' : 'This only takes a moment.'}</p>
        </div>
      </div>
    </main>
  );
}
