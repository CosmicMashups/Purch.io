import { Link, Navigate, useNavigate } from 'react-router-dom';
import { Skeleton } from '../../components/Skeleton';
import { useKioskStore } from './kioskStore';
import { useKioskCart, useSubmitKioskOrder } from './queries';
import { ORDER_TYPES, type OrderType } from './tickets';

const SUBTITLE: Record<OrderType, string> = { 'Dine In': 'Enjoy your food inside the store', 'Take Out': 'Pack it to go' };

/** Choosing here is the last step: it sets the order type and sends the order to the counter. */
export function KioskOrderTypePage() {
  const navigate = useNavigate();
  const cart = useKioskCart();
  const submit = useSubmitKioskOrder();
  const setSubmitted = useKioskStore((s) => s.setSubmitted);

  if (cart.isSuccess && cart.data.lines.length === 0 && !submit.isPending) return <Navigate to="/kiosk/menu" replace />;

  function choose(orderType: OrderType) {
    submit.mutate(orderType, {
      onSuccess: (order) => {
        setSubmitted(order);
        navigate('/kiosk/done', { replace: true });
      },
    });
  }

  return (
    <div className="flex flex-1 flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <Link to="/kiosk/cart" aria-disabled={submit.isPending} className="inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
          Back to order
        </Link>
      </header>
      <h1 className="text-3xl font-extrabold tracking-tight">For here or to go?</h1>

      {cart.isPending ? (
        <Skeleton className="h-40 w-full" />
      ) : submit.isPending ? (
        <p role="status" className="py-16 text-center text-xl font-semibold">
          Sending your order…
        </p>
      ) : (
        <div className="flex flex-1 flex-col justify-center gap-5">
          {ORDER_TYPES.map((type) => (
            <button key={type} type="button" onClick={() => choose(type)} className="flex min-h-32 flex-col items-start justify-center gap-1 rounded-panel border-2 border-line bg-surface px-6 py-6 text-left active:translate-y-px hover:border-brand">
              <span className="text-3xl font-bold">{type}</span>
              <span className="text-lg text-ink-soft">{SUBTITLE[type]}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
