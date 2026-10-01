import { Link, Navigate, useNavigate } from 'react-router-dom';
import { toast } from '../../components/feedback/toastStore';
import { userMessage } from '../../lib/apiError';
import type { AddLineRequest } from '../pos/types';
import { useLocalKioskCartStore } from './localCart';
import { useKioskStore } from './kioskStore';
import { usePlaceKioskOrder } from './queries';
import { ORDER_TYPES, type OrderType } from './tickets';

const SUBTITLE: Record<OrderType, string> = { 'Dine In': 'Enjoy your food inside the store', 'Take Out': 'Pack it to go' };

function toRequestLines(lines: ReturnType<typeof useLocalKioskCartStore.getState>['lines']): AddLineRequest[] {
  return lines.map((line) => ({
    itemId: line.itemId,
    itemVariantId: line.itemVariantId,
    quantity: line.quantity,
    ...(line.comboSelections.length > 0 ? { comboSelections: line.comboSelections.map((c) => ({ slotId: c.slotId, selectedItemId: c.selectedItemId })) } : {}),
    ...(line.modifierSelections.length > 0 ? { selectedModifierIds: line.modifierSelections.map((m) => m.itemModifierId) } : {}),
  }));
}

/** Choosing here is the last step: it places the whole order, built entirely on this device, in one call. */
export function KioskOrderTypePage() {
  const navigate = useNavigate();
  const lines = useLocalKioskCartStore((s) => s.lines);
  const clearCart = useLocalKioskCartStore((s) => s.clear);
  const placeOrder = usePlaceKioskOrder();
  const setSubmitted = useKioskStore((s) => s.setSubmitted);

  // Not while an order was just placed: clearing the cart on success would otherwise race this guard
  // and bounce back to the menu before the navigate to the confirmation screen lands.
  if (lines.length === 0 && !placeOrder.isPending && !placeOrder.isSuccess) return <Navigate to="/kiosk/menu" replace />;

  function choose(orderType: OrderType) {
    const orderId = crypto.randomUUID();
    placeOrder.mutate(
      { orderId, orderType, lines: toRequestLines(lines) },
      {
        onSuccess: (order) => {
          clearCart();
          setSubmitted(order);
          navigate('/kiosk/done', { replace: true });
        },
        onError: (error) => {
          toast.error(userMessage(error) || 'Something in your order is no longer available. Please review your order and try again.');
        },
      },
    );
  }

  return (
    <div className="flex flex-1 flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <Link to="/kiosk/cart" aria-disabled={placeOrder.isPending} className="inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
          Back to order
        </Link>
      </header>
      <h1 className="text-3xl font-extrabold tracking-tight">For here or to go?</h1>

      {placeOrder.isPending ? (
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
