import { CreditCard, DeviceMobile, IdentificationCard, Money } from '@phosphor-icons/react';
import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { Modal } from '../../components/Modal';
import { formatPeso } from '../dashboard/format';
import { barClass, primaryButton, secondaryButton } from './KioskActionBar';
import { ChoiceTile } from './ChoiceTile';
import { OptionCard } from './OptionCard';
import { useKioskStore } from './kioskStore';
import { toLocalTransaction, useLocalKioskCartStore } from './localCart';
import { useKioskPromoRules } from './queries';
import { DISCOUNT_HINTS, DISCOUNT_LABEL, KIOSK_PAYMENTS, PAYMENT_LABEL, paymentSummary, type DiscountHint, type KioskPayment } from './tickets';

const SUBTITLE: Record<KioskPayment, string> = {
  cash: 'Pay with cash at the counter',
  card: 'Tap, insert or swipe at the counter',
  ewallet: 'Scan the counter QR with your phone',
  discount: 'Show your ID at the counter',
};

const ICON: Record<KioskPayment, React.ReactNode> = {
  cash: <Money size={52} weight="duotone" aria-hidden="true" />,
  card: <CreditCard size={52} weight="duotone" aria-hidden="true" />,
  ewallet: <DeviceMobile size={52} weight="duotone" aria-hidden="true" />,
  discount: <IdentificationCard size={52} weight="duotone" aria-hidden="true" />,
};

/**
 * How the customer expects to pay. Nothing is charged here: the choice travels with the order so the cashier is ready
 * and the receipt records it. With Discounts asks which discount, because the cashier applies it at the counter.
 */
export function KioskPaymentPage() {
  const navigate = useNavigate();
  const lines = useLocalKioskCartStore((s) => s.lines);
  const checkout = useKioskStore((s) => s.checkout);
  const setPayment = useKioskStore((s) => s.setPayment);
  const { data: rules } = useKioskPromoRules();
  const [choosingDiscount, setChoosingDiscount] = useState(false);

  if (lines.length === 0) return <Navigate to="/kiosk/menu" replace />;
  if (!checkout.orderType) return <Navigate to="/kiosk/order-type" replace />;

  const total = toLocalTransaction(lines, rules).totalAmount;
  const discounted = checkout.payment === 'discount';

  function choose(payment: KioskPayment) {
    if (payment === 'discount') setChoosingDiscount(true);
    else setPayment(payment);
  }

  return (
    <div className="flex h-full flex-col">
      <main className="kiosk-scroll flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto px-6 pb-6">
        <div className="mt-2">
          <h1 className="text-4xl font-extrabold tracking-tight">How will you pay?</h1>
          <p className="text-xl text-ink-soft">You pay at the counter. This just lets the cashier get ready.</p>
        </div>
        <div className="mx-auto grid w-full max-w-5xl content-start gap-4 landscape:grid-cols-2">
          {KIOSK_PAYMENTS.map((payment) => (
            <ChoiceTile
              key={payment}
              icon={ICON[payment]}
              title={PAYMENT_LABEL[payment]}
              subtitle={payment === 'discount' && checkout.payment === 'discount' && checkout.discountHint ? paymentSummary(payment, checkout.discountHint) : SUBTITLE[payment]}
              selected={checkout.payment === payment}
              onSelect={() => choose(payment)}
            />
          ))}
        </div>
      </main>

      <div className={`${barClass} flex-wrap`}>
        <div className="flex min-w-40 flex-1 flex-col justify-center">
          <span className="text-base font-semibold text-ink-soft">{discounted ? 'Estimated total (before discount)' : 'Total'}</span>
          <span className="text-3xl font-extrabold tabular-nums">{formatPeso(total)}</span>
        </div>
        <Link to="/kiosk/order-type" className={secondaryButton}>
          Back
        </Link>
        <button type="button" className={primaryButton} disabled={!checkout.payment} onClick={() => navigate('/kiosk/processing')}>
          Place order
        </button>
      </div>

      <DiscountSheet
        open={choosingDiscount}
        current={checkout.discountHint}
        onClose={() => setChoosingDiscount(false)}
        onPick={(hint) => {
          setPayment('discount', hint);
          setChoosingDiscount(false);
        }}
      />
    </div>
  );
}

function DiscountSheet({ open, current, onClose, onPick }: { open: boolean; current: DiscountHint | null; onClose: () => void; onPick: (hint: DiscountHint) => void }) {
  return (
    <Modal open={open} title="Which discount?" onClose={onClose}>
      <div className="flex flex-col gap-4">
        <p className="text-lg text-ink-soft">The cashier applies your discount at the counter. Please have your valid ID ready. The total shown here is before the discount.</p>
        <div className="flex flex-col gap-3" role="radiogroup" aria-label="Discount type">
          {DISCOUNT_HINTS.map((hint) => (
            <OptionCard key={hint} kind="radio" title={DISCOUNT_LABEL[hint]} selected={current === hint} onSelect={() => onPick(hint)} />
          ))}
        </div>
      </div>
    </Modal>
  );
}
