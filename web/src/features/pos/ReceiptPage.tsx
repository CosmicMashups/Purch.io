import { useEffect, useRef, useState } from 'react';
import { readBrandCache } from '../../theme/brandCache';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { RefundDialog } from '../../components/RefundDialog';
import { toast } from '../../components/feedback/toastStore';
import { formatPeso } from '../dashboard/format';
import { paymentSummary } from '../kiosk/tickets';
import { useHardwareConfig } from '../../hardware/config';
import { IDLE_STATE, stateForReceipt, vatOf } from '../../hardware/display/channel';
import { usePublishCustomerDisplay } from '../../hardware/display/usePublishCustomerDisplay';
import { posApi } from './api';
import { usePosStore } from './posStore';
import { PaymentMethod, TransactionStatus, type TransactionLine } from './types';

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
  const location = useLocation();
  const receipt = usePosStore((s) => s.receipt);
  const clearReceipt = usePosStore((s) => s.clearReceipt);
  const showReceipt = usePosStore((s) => s.showReceipt);
  const paperWidth = useHardwareConfig((s) => s.paperWidth);
  const autoPrint = useHardwareConfig((s) => s.autoPrintReceipt);
  const [refunding, setRefunding] = useState(false);
  const [refundBusy, setRefundBusy] = useState(false);
  usePublishCustomerDisplay(receipt ? stateForReceipt(receipt) : IDLE_STATE);
  const [brand] = useState(() => readBrandCache() ?? {});
  const [shownAt] = useState(() => new Date());
  const printedFor = useRef<string | null>(null);
  const justSold = (location.state as { justSold?: boolean } | null)?.justSold === true;
  // Opened from the back-office Orders list: Done goes back there instead of starting a new sale.
  const backTo = (location.state as { backTo?: { to: string; label: string } } | null)?.backTo;

  // Straight after a sale is paid, once the receipt printer is set up, the print window opens by itself. Looking a sale up
  // later never does, and a re-render never prints twice.
  useEffect(() => {
    if (!receipt || !justSold || !autoPrint || receipt.status !== TransactionStatus.Completed || printedFor.current === receipt.id) return;
    const id = receipt.id;
    const timer = window.setTimeout(() => {
      printedFor.current = id;
      window.print();
    }, 300);
    return () => window.clearTimeout(timer);
  }, [receipt, justSold, autoPrint]);

  if (!receipt) return <Navigate to="/sell" replace />;

  const payment = receipt.payments[0];
  const refunded = receipt.status === TransactionStatus.Refunded;
  const receiptId = receipt.id;
  const vat = vatOf(receipt);
  const vatExempt = receipt.vatExemptAmount > 0;
  const printedAt = new Date(receipt.completedAt ?? receipt.createdAt ?? shownAt);

  function newSale() {
    clearReceipt();
    navigate(backTo?.to ?? '/sell', { replace: true });
  }

  async function submitRefund(reason: string, approverPin: string) {
    setRefundBusy(true);
    try {
      const result = await posApi.refund(receiptId, { reason, approverPin });
      showReceipt(result);
      setRefunding(false);
      toast.info('Sale refunded');
    } finally {
      setRefundBusy(false);
    }
  }

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <div className="flex flex-col gap-1 print:hidden">
        <h1 className="text-3xl font-bold tracking-tight text-brand-strong">{backTo ? 'Receipt' : 'Sale complete'}</h1>
        <p className="text-base text-ink-soft">The payment is recorded. Hand over the receipt.</p>
      </div>

      <article aria-label="Receipt" className={`receipt-paper receipt-${paperWidth} rounded-panel border border-line bg-surface p-6 font-mono text-sm leading-snug print:rounded-none print:border-0 print:p-0 print:text-black`}>
        <header className="flex flex-col items-center gap-0.5 pb-3 text-center">
          <p className="text-base font-bold uppercase">{brand.registeredBusinessName || brand.businessName || 'Business name not set'}</p>
          {brand.registeredAddress && <p>{brand.registeredAddress}</p>}
          <p>{brand.tin ? `VAT REG TIN: ${brand.tin}` : 'VAT REG TIN: not set'}</p>
          <p className="mt-2 text-base font-bold tracking-wide">OFFICIAL RECEIPT</p>
          <p className="font-bold">OR No. {receipt.receiptNumber === null ? 'pending' : String(receipt.receiptNumber).padStart(8, '0')}</p>
          {refunded && <p className="font-bold text-danger print:text-black">*** REFUNDED ***</p>}
        </header>

        <div className="border-y border-dashed border-ink-soft py-2">
          <Pair label="Date" value={printedAt.toLocaleDateString('en-PH', { year: 'numeric', month: '2-digit', day: '2-digit' })} />
          <Pair label="Time" value={printedAt.toLocaleTimeString('en-PH', { hour: '2-digit', minute: '2-digit', second: '2-digit' })} />
          {receipt.orderType && <Pair label="Order type" value={receipt.orderType} />}
          {receipt.kioskPrepNumber !== null && <Pair label="Order no." value={String(receipt.kioskPrepNumber)} />}
          {receipt.kioskPaymentPreference && <Pair label="Customer chose" value={paymentSummary(receipt.kioskPaymentPreference, receipt.kioskDiscountHint)} />}
        </div>

        <table className="mt-2 w-full text-left">
          <thead>
            <tr className="border-b border-dashed border-ink-soft">
              <th className="py-1 pr-1 font-bold">Qty</th>
              <th className="px-1 py-1 font-bold">Description</th>
              <th className="px-1 py-1 text-right font-bold">Price</th>
              <th className="py-1 pl-1 text-right font-bold">Amount</th>
            </tr>
          </thead>
          <tbody>
            {receipt.lines.map((line) => {
              const extra = details(line);
              return (
                <tr key={line.id} className="align-top">
                  <td className="py-1 pr-1 tabular-nums">{line.quantity}</td>
                  <td className="px-1 py-1">
                    {line.itemName}
                    {extra.length > 0 && <span className="block text-xs">{extra.join(', ')}</span>}
                    {line.appliedPromoLabel && <span className="block text-xs">Promo: {line.appliedPromoLabel}</span>}
                  </td>
                  <td className="px-1 py-1 text-right tabular-nums">{plain(line.unitPrice)}</td>
                  <td className="py-1 pl-1 text-right tabular-nums">{plain(line.lineTotal)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>

        <dl className="mt-2 flex flex-col gap-0.5 border-t border-dashed border-ink-soft pt-2">
          <Row label="Total Sales (VAT Inclusive)" value={receipt.subtotal} />
          {receipt.itemPromoDiscountAmount > 0 && <Row label="Less: Item promotions" value={-receipt.itemPromoDiscountAmount} />}
          {receipt.promoDiscountAmount > 0 && <Row label={`Less: Promo ${receipt.promoCode ?? ''}`.trim()} value={-receipt.promoDiscountAmount} />}
          {vatExempt && <Row label="Less: VAT (12%)" value={-receipt.vatExemptAmount} />}
          {receipt.discountAmount > 0 && <Row label="Less: SC/PWD Discount (20%)" value={-receipt.discountAmount} />}
          <div className="mt-1 flex items-baseline justify-between border-y border-ink-soft py-1 text-base font-bold">
            <dt>TOTAL AMOUNT DUE</dt>
            <dd className="tabular-nums">{formatPeso(receipt.totalAmount)}</dd>
          </div>
          {payment && (
            <>
              <Row label={`Tendered (${METHOD_LABEL[payment.method] ?? 'Payment'})`} value={payment.amountTendered ?? payment.amount} />
              {payment.changeGiven !== null && payment.changeGiven > 0 && <Row label="Change" value={payment.changeGiven} />}
            </>
          )}
        </dl>

        <dl className="mt-3 flex flex-col gap-0.5 border-t border-dashed border-ink-soft pt-2">
          <Row label="VATable Sales" value={vatExempt ? 0 : receipt.totalAmount - vat} />
          <Row label="VAT-Exempt Sales" value={vatExempt ? receipt.totalAmount + receipt.discountAmount : 0} />
          <Row label="Zero-Rated Sales" value={0} />
          <Row label="VAT Amount (12%)" value={vat} />
        </dl>

        {receipt.seniorPwdDiscountApplied && (
          <div className="mt-3 flex flex-col gap-2 border-t border-dashed border-ink-soft pt-2">
            <Blank label="SC/PWD Name" />
            <Blank label="SC/PWD ID No." />
            <Blank label="Signature" />
          </div>
        )}

        <div className="mt-3 flex flex-col gap-2 border-t border-dashed border-ink-soft pt-2">
          <Blank label="Sold to" />
          <Blank label="TIN" />
          <Blank label="Address" />
        </div>

        <footer className="mt-4 border-t border-dashed border-ink-soft pt-3 text-center">
          <p className="font-bold">THIS SERVES AS YOUR OFFICIAL RECEIPT</p>
          <p className="mt-1">Thank you. Please come again.</p>
        </footer>
      </article>

      <div className="flex flex-wrap gap-3 print:hidden">
        <button type="button" onClick={newSale} className="h-16 rounded-control bg-brand px-8 text-xl font-bold text-on-brand hover:bg-brand-strong active:translate-y-px">
          {backTo ? `Back to ${backTo.label}` : 'New sale'}
        </button>
        <button type="button" onClick={() => window.print()} className="h-16 rounded-control border border-line bg-surface px-8 text-xl font-bold hover:border-brand">
          Print
        </button>
        {receipt.status === TransactionStatus.Completed && (
          <button type="button" onClick={() => navigate('/sell/exchange')} className="h-16 rounded-control border border-line bg-surface px-8 text-xl font-bold hover:border-brand">
            Exchange
          </button>
        )}
        {!refunded && (
          <button type="button" onClick={() => setRefunding(true)} className="h-16 rounded-control border border-danger px-8 text-xl font-bold text-danger hover:bg-danger/10">
            Refund
          </button>
        )}
      </div>

      {refunding && <RefundDialog total={formatPeso(receipt.totalAmount)} busy={refundBusy} onSubmit={submitRefund} onCancel={() => setRefunding(false)} />}
    </div>
  );
}

function plain(value: number): string {
  return value.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-baseline justify-between gap-2">
      <dt>{label}</dt>
      <dd className="tabular-nums">{value < 0 ? `-${formatPeso(-value)}` : formatPeso(value)}</dd>
    </div>
  );
}

function Pair({ label, value }: { label: string; value: string }) {
  return (
    <p className="flex justify-between gap-2">
      <span>{label}</span>
      <span>{value}</span>
    </p>
  );
}

/** A line the customer or cashier fills in by hand on the printed copy. */
function Blank({ label }: { label: string }) {
  return (
    <p className="flex items-end gap-2">
      <span className="shrink-0">{label}:</span>
      <span className="h-4 flex-1 border-b border-ink-soft" />
    </p>
  );
}
