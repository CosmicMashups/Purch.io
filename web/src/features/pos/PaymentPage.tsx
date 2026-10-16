import { ArrowsSplit } from '@phosphor-icons/react';
import { useRef, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { FormField, PrimaryButton, controlClass } from '../../components/forms/FormField';
import { PageHeader } from '../../components/PageHeader';
import { ApiError, userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import { useBranches } from '../branches/queries';
import { useCreditEnabled } from '../tenant/queries';
import { useCreditLedgers } from '../credit/queries';
import { formatPeso } from '../dashboard/format';
import { stateForPayment, vatOf } from '../../hardware/display/channel';
import { usePublishCustomerDisplay } from '../../hardware/display/usePublishCustomerDisplay';
import { tenderCoversTotal } from './catalogView';
import { CashKeypad } from './components/CashKeypad';
import { GcashQr } from './components/GcashQr';
import { SplitTenderPanel } from './components/SplitTenderPanel';
import { usePosStore } from './posStore';
import { useCart, usePay } from './queries';
import { PaymentMethod, type PaymentPart, type RecordPaymentRequest } from './types';

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
];

/** Roles holding the SplitPay permission (backend Permissions.SplitPay). The API still enforces it. */
const SPLIT_ROLES: readonly string[] = ['Admin', 'Manager', 'Cashier'];

// The API's 404 problem details carry a typed `resource`; the message wording is not part of the contract.
const notFoundOf = (error: unknown, resource: string) => error instanceof ApiError && error.kind === 'notFound' && error.resource === resource;

/** 404 for the open cart on pay: an earlier attempt most likely completed the sale and its answer was lost. */
const isLostSale = (error: unknown) => notFoundOf(error, 'Open cart');

/** 404 for the Utang customer account: the cart is still open, the cashier picks another customer. */
const isMissingLedger = (error: unknown) => notFoundOf(error, 'Customer credit account');

/** A refused payment, shown inline. The expected-total mismatch gets one plain instruction with the new total, a
 * missing customer account says what to do; every other refusal (other 409s and 404s included) is the server's words. */
function payErrorText(error: unknown): string {
  if (isMissingLedger(error)) return 'That customer account could not be found. Choose another customer.';
  if (error instanceof ApiError && error.kind === 'conflict') {
    // The code says what happened; the new total is the number in the server's sentence.
    const moved = error.code === 'total_changed' ? /(\d+(?:\.\d+)?)/.exec(error.message) : null;
    if (moved) return `The cart changed, review the total. It is now ${formatPeso(Number(moved[1]))}.`;
  }
  return userMessage(error);
}

export function PaymentPage() {
  const navigate = useNavigate();
  const { claims, role } = useSession();
  const cart = useCart(true);
  const pay = usePay();
  const showReceipt = usePosStore((s) => s.showReceipt);
  const branches = useBranches();
  const [choice, setChoice] = useState<Choice>('cash');
  const [tendered, setTendered] = useState('');
  const [ledgerId, setLedgerId] = useState('');
  const [splitting, setSplitting] = useState(false);
  // The last split refusal, cleared as soon as the cashier changes the parts.
  const [splitError, setSplitError] = useState<string | null>(null);
  const [singleError, setSingleError] = useState<string | null>(null);
  // An Utang account the server no longer has: the split panel drops the part that used it.
  const [rejectedLedgerId, setRejectedLedgerId] = useState<string | null>(null);
  // One request per attempt: a double tap lands before React re-renders the button as busy.
  const inFlight = useRef(false);
  // One key per visit to this screen: a retry or double-tap resends it, so the server pays the sale once.
  const paymentKey = useRef(crypto.randomUUID());
  const canSplit = role !== null && SPLIT_ROLES.includes(role);
  const creditOn = useCreditEnabled();
  const ledgers = useCreditLedgers(choice === 'utang' && creditOn);
  usePublishCustomerDisplay(stateForPayment(cart.data));

  if (!claims?.deviceId) return <Navigate to="/sell" replace />;
  if (cart.isPending) return <Skeleton className="h-96 w-full max-w-3xl" />;
  if (cart.isError) return <ErrorState title="The cart could not be loaded" message={userMessage(cart.error)} onRetry={() => void cart.refetch()} />;
  // The sale may already be recorded: never offer to charge again, send the cashier to look it up.
  if (pay.isError && isLostSale(pay.error)) return <LostSale />;
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
      // Exactly the total on this screen, which is the server's last answer: the server refuses the sale if its own total has moved.
      expectedTotal: total,
    };
    submit(body);
  }

  function completeSplit(parts: PaymentPart[]) {
    // With parts the server ignores the request-level method, but it is still required: send the first part's.
    submit({ method: parts[0].method, amountTendered: null, customerCreditLedgerId: null, expectedTotal: total, parts });
  }

  function openSplit(open: boolean) {
    pay.reset();
    setSplitError(null);
    setSplitting(open);
  }

  function submit(body: RecordPaymentRequest) {
    if (inFlight.current) return;
    inFlight.current = true;
    setSplitError(null);
    setSingleError(null);
    // A refusal is reported by the shared mutation error toast; the sale stays open to retry.
    pay.mutate({ ...body, paymentKey: paymentKey.current }, {
      onSuccess: (completed) => {
        showReceipt(completed);
        navigate('/sell/receipt', { replace: true, state: { justSold: true } });
      },
      onError: (error) => {
        const text = payErrorText(error);
        if (body.parts) setSplitError(text);
        else setSingleError(text);
        if (isMissingLedger(error)) {
          setLedgerId('');
          setRejectedLedgerId(body.parts?.find((p) => p.method === PaymentMethod.UtangCredit)?.customerCreditLedgerId ?? null);
        }
      },
      onSettled: () => {
        inFlight.current = false;
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
        <p className="mt-1 text-base text-ink-soft">VAT (12%): {formatPeso(vatOf(cart.data))}</p>
      </div>

      {splitting ? (
        <SplitTenderPanel
          total={total}
          creditOn={creditOn}
          busy={pay.isPending}
          branch={branch}
          error={splitError}
          rejectedLedgerId={rejectedLedgerId}
          onEdit={() => {
            setSplitError(null);
          }}
          onComplete={completeSplit}
          onCancel={() => openSplit(false)}
        />
      ) : (
        <>
          <div role="radiogroup" aria-label="Payment method" className="grid gap-3 sm:grid-cols-2">
            {CHOICES.filter((c) => c.id !== 'utang' || creditOn).map((c) => (
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
          {canSplit && (
            <button
              type="button"
              onClick={() => openSplit(true)}
              className="flex h-12 items-center gap-2 self-start rounded-control border border-line bg-surface px-5 text-base font-semibold text-ink hover:border-brand"
            >
              <ArrowsSplit size={22} aria-hidden />
              Split payment
            </button>
          )}
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
                <GcashQr branch={branch} />
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

          {singleError && (
            <p role="alert" className="rounded-control border border-danger/40 bg-danger/10 px-4 py-3 text-base font-medium text-danger">
              {singleError}
            </p>
          )}
          <div>
            <PrimaryButton type="button" busy={pay.isPending} disabled={!ready} onClick={confirm}>
              {pay.isPending ? 'Recording payment...' : `Confirm ${formatPeso(total)}`}
            </PrimaryButton>
          </div>
        </>
      )}
    </div>
  );
}

function LostSale() {
  return (
    <div className="flex max-w-2xl flex-col gap-5">
      <PageHeader title="Payment" backTo={{ to: '/sell', label: 'the cart' }} />
      <div role="alert" className="rounded-panel border border-warn bg-warn/10 p-6">
        <p className="text-xl font-bold">This sale may already be recorded. Check Recent sales before charging again.</p>
        <p className="mt-2 text-base text-ink-soft">The register could not find the open cart. That usually means the last payment went through but its answer was lost.</p>
      </div>
      <div className="flex flex-wrap gap-3">
        <Link to="/sell/find-sale" className="flex h-14 items-center rounded-control bg-brand px-6 text-lg font-bold text-on-brand hover:bg-brand-strong">
          Open Recent sales
        </Link>
        <Link to="/sell" className="flex h-14 items-center rounded-control border border-line bg-surface px-6 text-lg font-semibold hover:border-brand">
          Back to the register
        </Link>
      </div>
    </div>
  );
}
