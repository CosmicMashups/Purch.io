import { Navigate, useNavigate } from 'react-router-dom';
import { formatPeso } from '../dashboard/format';
import { useHardwareConfig } from '../../hardware/config';
import { IDLE_STATE, stateForReceipt } from '../../hardware/display/channel';
import { usePublishCustomerDisplay } from '../../hardware/display/usePublishCustomerDisplay';
import { usePosStore } from './posStore';
import { PaymentMethod, type TransactionLine } from './types';

const METHOD_LABEL: Record<number, string> = {
  [PaymentMethod.Cash]: 'Cash',
  [PaymentMethod.QrPh]: 'QR Ph',
  [PaymentMethod.BankTransfer]: 'Bank transfer',
  [PaymentMethod.ManualGcashQr]: 'GCash',
  [PaymentMethod.BillPaymentELoad]: 'Bill payment / e-load',
  [PaymentMethod.UtangCredit]: 'Utang / Credit',
  [PaymentMethod.Split]: 'Split',
};

function details(line: TransactionLine): string[] {
  return [
    ...Object.values(line.itemVariantAttributes),
    ...line.comboSelections.map((c) => `${c.slotLabel}: ${c.selectedItemName}`),
    ...line.modifierSelections.map((m) => m.modifierName),
  ];
}

export function ReceiptPage() {
  const navigate = useNavigate();
  const receipt = usePosStore((s) => s.receipt);
  const clearReceipt = usePosStore((s) => s.clearReceipt);
  const paperWidth = useHardwareConfig((s) => s.paperWidth);
  usePublishCustomerDisplay(receipt ? stateForReceipt(receipt) : IDLE_STATE);

  if (!receipt) return <Navigate to="/sell" replace />;

  const payment = receipt.payments[0];

  function newSale() {
    clearReceipt();
    navigate('/sell', { replace: true });
  }

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <div className="flex flex-col gap-1 print:hidden">
        <h1 className="text-3xl font-bold tracking-tight text-brand-strong">Sale complete</h1>
        <p className="text-base text-ink-soft">The payment is recorded. Hand over the receipt.</p>
      </div>

      <article aria-label="Receipt" className={`receipt-paper receipt-${paperWidth} rounded-panel border border-line bg-surface p-6 print:border-0 print:p-0`}>
        <header className="border-b border-dashed border-line pb-4">
          <p className="text-lg font-bold">Receipt No. {receipt.receiptNumber ?? 'pending'}</p>
          {receipt.orderType && <p className="text-base text-ink-soft">{receipt.orderType}</p>}
          {receipt.kioskPrepNumber !== null && <p className="text-base text-ink-soft">Order number {receipt.kioskPrepNumber}</p>}
        </header>

        <ul className="divide-y divide-line">
          {receipt.lines.map((line) => {
            const extra = details(line);
            return (
              <li key={line.id} className="py-3">
                <div className="flex items-baseline justify-between gap-3">
                  <p className="text-base font-semibold">
                    {line.itemName} x {line.quantity}
                  </p>
                  <p className="text-base tabular-nums">{formatPeso(line.lineTotal)}</p>
                </div>
                {extra.length > 0 && <p className="text-sm text-ink-soft">{extra.join(', ')}</p>}
                {line.appliedPromoLabel && <p className="text-sm text-ink-soft">Promo: {line.appliedPromoLabel}</p>}
              </li>
            );
          })}
        </ul>

        <dl className="mt-2 flex flex-col gap-1 border-t border-dashed border-line pt-4 text-base">
          <Row label="Subtotal" value={receipt.subtotal} />
          {receipt.itemPromoDiscountAmount > 0 && <Row label="Item promotions" value={-receipt.itemPromoDiscountAmount} />}
          {receipt.promoDiscountAmount > 0 && <Row label={`Promo code ${receipt.promoCode ?? ''}`.trim()} value={-receipt.promoDiscountAmount} />}
          {receipt.discountAmount > 0 && <Row label="Senior / PWD discount" value={-receipt.discountAmount} />}
          <div className="mt-1 flex items-baseline justify-between text-2xl font-bold">
            <dt>Total</dt>
            <dd className="tabular-nums">{formatPeso(receipt.totalAmount)}</dd>
          </div>
          {payment && (
            <>
              <Row label={METHOD_LABEL[payment.method] ?? 'Payment'} value={payment.amountTendered ?? payment.amount} />
              {payment.changeGiven !== null && payment.changeGiven > 0 && <Row label="Change" value={payment.changeGiven} />}
            </>
          )}
        </dl>
      </article>

      <p className="text-sm text-ink-soft print:hidden">This is a sale summary. The BIR official receipt is not available from the web yet.</p>

      <div className="flex flex-wrap gap-3 print:hidden">
        <button type="button" onClick={newSale} className="h-16 rounded-control bg-brand px-8 text-xl font-bold text-on-brand hover:bg-brand-strong active:translate-y-px">
          New sale
        </button>
        <button type="button" onClick={() => window.print()} className="h-16 rounded-control border border-line bg-surface px-8 text-xl font-bold hover:border-brand">
          Print
        </button>
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-baseline justify-between">
      <dt className="text-ink-soft">{label}</dt>
      <dd className="tabular-nums">{formatPeso(value)}</dd>
    </div>
  );
}
