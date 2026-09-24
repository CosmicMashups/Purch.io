import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { FormField, PrimaryButton, controlClass } from '../../components/forms/FormField';
import { PageHeader } from '../../components/PageHeader';
import { userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import { useBranches } from '../branches/queries';
import { useCreditLedgers } from '../credit/queries';
import { formatPeso } from '../dashboard/format';
import { stateForPayment } from '../../hardware/display/channel';
import { usePublishCustomerDisplay } from '../../hardware/display/usePublishCustomerDisplay';
import { tenderCoversTotal } from './catalogView';
import { CashKeypad } from './components/CashKeypad';
import { usePosStore } from './posStore';
import { useCart, usePay } from './queries';
import { PaymentMethod, type RecordPaymentRequest } from './types';

type Choice = 'cash' | 'bank' | 'gcash' | 'utang';

const CHOICES: { id: Choice; label: string; method: PaymentMethod }[] = [
  { id: 'cash', label: 'Cash', method: PaymentMethod.Cash },
  { id: 'bank', label: 'Bank transfer', method: PaymentMethod.BankTransfer },
  { id: 'gcash', label: 'GCash (manual QR)', method: PaymentMethod.ManualGcashQr },
  { id: 'utang', label: 'Utang / Credit', method: PaymentMethod.UtangCredit },
];

const UNAVAILABLE = [
  { label: 'QR Ph', why: 'Needs a live payment connection' },
  { label: 'Bill payment / e-load', why: 'Not connected yet' },
  { label: 'Split payment', why: 'Not built yet' },
];

export function PaymentPage() {
  const navigate = useNavigate();
  const { claims } = useSession();
  const cart = useCart(true);
  const pay = usePay();
  const showReceipt = usePosStore((s) => s.showReceipt);
  const branches = useBranches();
  const [choice, setChoice] = useState<Choice>('cash');
  const [tendered, setTendered] = useState('');
  const [ledgerId, setLedgerId] = useState('');
  const ledgers = useCreditLedgers(choice === 'utang');
  usePublishCustomerDisplay(stateForPayment(cart.data));

  if (!claims?.deviceId) return <Navigate to="/sell" replace />;
  if (cart.isPending) return <Skeleton className="h-96 w-full max-w-3xl" />;
  if (cart.isError) return <ErrorState title="The cart could not be loaded" message={userMessage(cart.error)} onRetry={() => void cart.refetch()} />;
  // A completed sale empties the cart on the server; that must not bounce us off before the receipt shows.
  if (cart.data.lines.length === 0 && !pay.isSuccess) return <Navigate to="/sell" replace />;

  const total = cart.data.totalAmount;
  const branch = branches.data?.find((b) => b.id === claims.branchId);
  const tenderNumber = tendered === '' ? NaN : Number(tendered);

  const ready =
    choice === 'cash' ? tenderCoversTotal(tenderNumber, total) : choice === 'utang' ? ledgerId !== '' : true;

  function confirm() {
    const method = CHOICES.find((c) => c.id === choice)!.method;
    const body: RecordPaymentRequest = {
      method,
      amountTendered: choice === 'cash' ? tenderNumber : null,
      customerCreditLedgerId: choice === 'utang' ? ledgerId : null,
    };
    // A refusal is reported by the shared mutation error toast; the sale stays open to retry.
    pay.mutate(body, {
      onSuccess: (completed) => {
        showReceipt(completed);
        navigate('/sell/receipt', { replace: true });
      },
    });
  }

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader title="Payment" backTo={{ to: '/sell', label: 'the cart' }} />

      <div className="rounded-panel border border-brand bg-brand-tint p-6">
        <p className="text-base font-medium text-ink-soft">Total to collect</p>
        <p className="text-5xl font-bold tabular-nums tracking-tight" data-testid="amount-due">
          {formatPeso(total)}
        </p>
      </div>

      <div role="radiogroup" aria-label="Payment method" className="grid gap-3 sm:grid-cols-2">
        {CHOICES.map((c) => (
          <button
            key={c.id}
            type="button"
            role="radio"
            aria-checked={choice === c.id}
            onClick={() => setChoice(c.id)}
            className={`min-h-16 rounded-control px-5 text-left text-lg font-semibold ${choice === c.id ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`}
          >
            {c.label}
          </button>
        ))}
      </div>
      <ul className="flex flex-wrap gap-x-6 gap-y-1 text-sm text-ink-soft">
        {UNAVAILABLE.map((u) => (
          <li key={u.label}>
            {u.label}: {u.why}
          </li>
        ))}
      </ul>

      <section className="rounded-panel border border-line bg-surface p-6">
        {choice === 'cash' && <CashKeypad total={total} value={tendered} onChange={setTendered} />}

        {choice === 'bank' && <p className="text-base">Confirm only after you have checked that the transfer of {formatPeso(total)} has arrived.</p>}

        {choice === 'gcash' && (
          <div className="flex flex-col gap-3">
            <p className="text-base">Show this QR to the customer. Confirm only after you see the payment of {formatPeso(total)} land.</p>
            {branch?.manualGcashQrImageUrl ? (
              <img src={branch.manualGcashQrImageUrl} alt="GCash QR code for this branch" className="size-56 rounded-control border border-line object-contain" />
            ) : (
              <p className="rounded-control border border-dashed border-ink-soft/40 p-4 text-base text-ink-soft">No QR is set for this branch. An admin can add one in the branch settings.</p>
            )}
            {(branch?.manualGcashAccountName || branch?.manualGcashAccountNumber) && (
              <p className="text-base font-semibold">
                {[branch.manualGcashAccountName, branch.manualGcashAccountNumber].filter(Boolean).join(', ')}
              </p>
            )}
          </div>
        )}

        {choice === 'utang' && (
          <div className="flex flex-col gap-3">
            {ledgers.isError ? (
              <ErrorState title="Customer accounts could not be loaded" message={userMessage(ledgers.error)} onRetry={() => void ledgers.refetch()} />
            ) : ledgers.isPending ? (
              <Skeleton className="h-14 w-full" />
            ) : ledgers.data.filter((l) => l.isActive).length === 0 ? (
              <p className="text-base text-ink-soft">No customer accounts yet. A manager can add one.</p>
            ) : (
              <FormField label="Customer account" hint="The server checks the credit limit when you confirm">
                <select value={ledgerId} onChange={(e) => setLedgerId(e.target.value)} className={controlClass}>
                  <option value="">Choose a customer</option>
                  {ledgers.data
                    .filter((l) => l.isActive)
                    .map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.customerFullName} ({formatPeso(Math.max(0, l.creditLimit - l.balance))} available)
                      </option>
                    ))}
                </select>
              </FormField>
            )}
          </div>
        )}
      </section>

      <div>
        <PrimaryButton type="button" busy={pay.isPending} disabled={!ready} onClick={confirm}>
          {pay.isPending ? 'Recording payment...' : `Confirm ${formatPeso(total)}`}
        </PrimaryButton>
      </div>
    </div>
  );
}
