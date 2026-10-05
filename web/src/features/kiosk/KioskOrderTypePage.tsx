import { ForkKnife, ShoppingBag } from '@phosphor-icons/react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { barClass, secondaryButton } from './KioskActionBar';
import { ChoiceTile } from './ChoiceTile';
import { useKioskStore } from './kioskStore';
import { useLocalKioskCartStore } from './localCart';
import { ORDER_TYPES, type OrderType } from './tickets';

const SUBTITLE: Record<OrderType, string> = { 'Dine In': 'Enjoy your food inside the store', 'Take Out': 'Pack it to go' };
const ICON: Record<OrderType, React.ReactNode> = {
  'Dine In': <ForkKnife size={52} weight="duotone" aria-hidden="true" />,
  'Take Out': <ShoppingBag size={52} weight="duotone" aria-hidden="true" />,
};

/** For here or to go. It only records the choice: the order is sent after the customer says how they will pay. */
export function KioskOrderTypePage() {
  const navigate = useNavigate();
  const hasLines = useLocalKioskCartStore((s) => s.lines.length > 0);
  const chosen = useKioskStore((s) => s.checkout.orderType);
  const setOrderType = useKioskStore((s) => s.setOrderType);

  if (!hasLines) return <Navigate to="/kiosk/menu" replace />;

  function choose(orderType: OrderType) {
    setOrderType(orderType);
    navigate('/kiosk/payment');
  }

  return (
    <div className="flex h-full flex-col">
      <main className="kiosk-scroll flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto px-6 pb-6">
        <h1 className="mt-2 text-4xl font-extrabold tracking-tight">For here or to go?</h1>
        <div className="mx-auto grid w-full max-w-5xl content-start gap-5 landscape:grid-cols-2">
          {ORDER_TYPES.map((type) => (
            <ChoiceTile key={type} icon={ICON[type]} title={type} subtitle={SUBTITLE[type]} selected={chosen === type} onSelect={() => choose(type)} />
          ))}
        </div>
      </main>
      <div className={barClass}>
        <Link to="/kiosk/cart" className={secondaryButton}>
          Back to order
        </Link>
      </div>
    </div>
  );
}
