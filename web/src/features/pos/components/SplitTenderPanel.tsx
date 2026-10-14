/*
 * Design note (Task 12, U1 POS). Split payment is the exception, not the default: the single-method screen stays
 * as it was (Cash keypad, one tap) and "Split payment" is a secondary button that swaps the method area for this
 * panel. One primary action only, "Complete sale", enabled the moment Remaining hits exactly ₱0.00. The figures
 * strip (Total / Paid / Remaining) uses large tabular numerals; Remaining turns green at zero, never red before
 * that (a part still to collect is normal, not an error). Change due appears only when a cash part was over-tendered.
 * The add-part form sits under the list: method chips (48px+), an amount field prefilled with what is left, then the
 * method's own input (the shared CashKeypad for cash, a reference for bank/GCash, the customer picker for Utang).
 * Colour keeps its operational meaning: red only for a blocking error, amber for an Utang amount past the available
 * credit (the server decides), green for "nothing left". Motion: colour transitions only, none under reduced motion.
 * Skills run: impeccable (shape, Operate mode), design-taste-frontend (pre-flight), ui-ux-pro-max (react +
 * html-tailwind, dials 6/4/3, not persisted).
 */
import { CheckCircle, Trash, Warning } from '@phosphor-icons/react';
import { useEffect, useRef, useState } from 'react';
import type { Branch } from '../../branches/types';
import { ErrorState } from '../../../components/ErrorState';
import { Skeleton } from '../../../components/Skeleton';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { userMessage } from '../../../lib/apiError';
import { useCreditLedgers } from '../../credit/queries';
import { formatPeso } from '../../dashboard/format';
import { MAX_REFERENCE_LENGTH, MAX_SPLIT_PARTS, parseAmount, remaining, splitChange, validateParts } from '../rules';
import { PaymentMethod, type PaymentPart } from '../types';
import { CashKeypad } from './CashKeypad';
import { GcashQr } from './GcashQr';

type Method = typeof PaymentMethod.Cash | typeof PaymentMethod.BankTransfer | typeof PaymentMethod.ManualGcashQr | typeof PaymentMethod.UtangCredit;

const METHODS: { method: Method; label: string }[] = [
  { method: PaymentMethod.Cash, label: 'Cash' },
  { method: PaymentMethod.BankTransfer, label: 'Bank transfer' },
  { method: PaymentMethod.ManualGcashQr, label: 'GCash' },
  { method: PaymentMethod.UtangCredit, label: 'Utang / Credit' },
];

const labelOf = (m: PaymentMethod) => METHODS.find((x) => x.method === m)?.label ?? 'Payment';
const cents = (n: number) => Math.round(n * 100);
const amountText = (n: number) => (n > 0 ? String(n) : '');

export interface SplitTenderPanelProps {
  total: number;
  creditOn: boolean;
  busy: boolean;
  /** The server's refusal of the last attempt, shown inline. */
  error: string | null;
  /** The branch whose manual GCash QR is shown while a GCash part is being taken. */
  branch?: Branch;
  /** Parts or the draft changed: a refusal shown for the last attempt no longer applies. */
  onEdit?: () => void;
  /** A customer account the server refused as missing: any part using it is dropped so another can be chosen. */
  rejectedLedgerId?: string | null;
  onComplete: (parts: PaymentPart[]) => void;
  onCancel: () => void;
}

export function SplitTenderPanel({ total, creditOn, busy, error, branch, onEdit, rejectedLedgerId, onComplete, onCancel }: SplitTenderPanelProps) {
  const [parts, setParts] = useState<PaymentPart[]>([]);
  const [method, setMethod] = useState<Method>(PaymentMethod.Cash);
  const [amount, setAmount] = useState(() => amountText(total));
  const [tendered, setTendered] = useState('');
  const [reference, setReference] = useState('');
  const [ledgerId, setLedgerId] = useState('');
  const [seenRejected, setSeenRejected] = useState(rejectedLedgerId);
  if (seenRejected !== rejectedLedgerId) {
    setSeenRejected(rejectedLedgerId);
    if (rejectedLedgerId && parts.some((p) => p.customerCreditLedgerId === rejectedLedgerId)) {
      const kept = parts.filter((p) => p.customerCreditLedgerId !== rejectedLedgerId);
      setParts(kept);
      setLedgerId('');
      setAmount(amountText(remaining(total, kept)));
    }
  }
  const completeRef = useRef<HTMLButtonElement>(null);
  const focusComplete = useRef(false);
  // Add unmounts once nothing remains; focus moves to the next thing to press instead of the page body.
  useEffect(() => {
    if (focusComplete.current) {
      focusComplete.current = false;
      completeRef.current?.focus();
    }
  }, [parts]);
  const edited = () => {
    if (error) onEdit?.();
  };
  const ledgers = useCreditLedgers(creditOn);
  // The server's total can move (a refused sale reloads the cart): offer what is now left.
  const [seenTotal, setSeenTotal] = useState(total);
  if (seenTotal !== total) {
    setSeenTotal(total);
    setAmount(amountText(remaining(total, parts)));
  }

  const left = remaining(total, parts);
  const partsCents = parts.reduce((sum, p) => sum + cents(p.amount), 0);
  // After a refused sale the total can drop below what was already added: never show that as settled.
  const over = Math.max(0, partsCents - cents(total)) / 100;
  const paid = Math.min(partsCents, cents(total)) / 100;
  const change = splitChange(parts);
  const hasUtang = parts.some((p) => p.method === PaymentMethod.UtangCredit);
  const full = parts.length >= MAX_SPLIT_PARTS;
  const ready = over === 0 && cents(left) === 0 && validateParts(total, parts) === null;
  const activeLedgers = ledgers.data?.filter((l) => l.isActive) ?? [];

  // The draft part, checked as it is typed.
  const amountValue = parseAmount(amount);
  const tenderValue = tendered === '' ? amountValue : parseAmount(tendered);
  const ledger = activeLedgers.find((l) => l.id === ledgerId);
  const available = ledger ? Math.max(0, ledger.creditLimit - ledger.balance) : 0;
  let draftError: string | null = null;
  if (amount !== '' && (!(amountValue > 0) || Math.abs(amountValue * 100 - cents(amountValue)) > 1e-6)) draftError = 'Enter an amount more than 0, with at most 2 decimals';
  else if (amount !== '' && cents(amountValue) > cents(left))
    draftError =
      method === PaymentMethod.Cash
        ? `The cash part can be at most ${formatPeso(left)}. Put extra cash in "Cash received" and it becomes change.`
        : `Only cash can be more than what's left. Enter at most ${formatPeso(left)}.`;
  else if (reference.trim().length > MAX_REFERENCE_LENGTH) draftError = `Keep the reference to ${MAX_REFERENCE_LENGTH} characters`;
  const canAdd =
    !full &&
    cents(left) > 0 &&
    amount !== '' &&
    draftError === null &&
    (method !== PaymentMethod.Cash || (Number.isFinite(tenderValue) && cents(tenderValue) >= cents(amountValue))) &&
    (method !== PaymentMethod.UtangCredit || (ledgerId !== '' && !hasUtang));

  function resetDraft(nextLeft: number, nextMethod: Method = method) {
    edited();
    setMethod(nextMethod);
    setAmount(amountText(nextLeft));
    setTendered('');
    setReference('');
    setLedgerId('');
  }

  function pick(next: Method) {
    resetDraft(left, next);
  }

  function add() {
    if (!canAdd) return;
    const part: PaymentPart = {
      method,
      amount: amountValue,
      amountTendered: method === PaymentMethod.Cash ? tenderValue : null,
      customerCreditLedgerId: method === PaymentMethod.UtangCredit ? ledgerId : null,
      reference: method === PaymentMethod.BankTransfer || method === PaymentMethod.ManualGcashQr ? reference.trim() || null : null,
    };
    const next = [...parts, part];
    focusComplete.current = remaining(total, next) === 0;
    setParts(next);
    // After a non-cash part, cash is the usual way to settle what is left.
    resetDraft(remaining(total, next), method === PaymentMethod.UtangCredit ? PaymentMethod.Cash : method);
  }

  function removeAt(index: number) {
    const next = parts.filter((_, i) => i !== index);
    setParts(next);
    resetDraft(remaining(total, next));
  }

  return (
    <section aria-label="Split payment" className="flex flex-col gap-5 rounded-panel border border-line bg-surface p-5 sm:p-6">
      <dl className="grid grid-cols-3 gap-3">
        <Figure label="Total" value={formatPeso(total)} />
        <Figure label="Paid" value={formatPeso(paid)} />
        <div className={`rounded-control px-4 py-3 motion-safe:transition-colors ${over > 0 ? 'bg-warn/10 text-ink' : cents(left) === 0 ? 'bg-ok/10 text-ok' : 'bg-brand-tint text-ink'}`}>
          <dt className="text-sm font-semibold">Remaining</dt>
          <dd data-testid="split-remaining" aria-live="polite" className="text-3xl font-bold tabular-nums tracking-tight sm:text-4xl">
            {formatPeso(left)}
          </dd>
        </div>
      </dl>

      {parts.length > 0 && (
        <ol aria-label="Payments added" className="flex flex-col divide-y divide-line rounded-control border border-line">
          {parts.map((p, i) => {
            const partChange = p.method === PaymentMethod.Cash && p.amountTendered != null ? (cents(p.amountTendered) - cents(p.amount)) / 100 : 0;
            const who = p.customerCreditLedgerId ? activeLedgers.find((l) => l.id === p.customerCreditLedgerId)?.customerFullName : null;
            const detail =
              p.method === PaymentMethod.Cash
                ? partChange > 0 ? `Received ${formatPeso(p.amountTendered ?? p.amount)}, change ${formatPeso(partChange)}` : null
                : p.reference ? `Ref ${p.reference}` : who ?? null;
            return (
              <li key={i} className="flex min-h-16 items-center gap-3 px-4 py-2">
                <div className="min-w-0 flex-1">
                  <p className="text-lg font-semibold">{labelOf(p.method)}</p>
                  {detail && <p className="truncate text-sm text-ink-soft">{detail}</p>}
                </div>
                <p className="text-xl font-bold tabular-nums">{formatPeso(p.amount)}</p>
                <button
                  type="button"
                  onClick={() => removeAt(i)}
                  disabled={busy}
                  aria-label={`Remove ${labelOf(p.method)} ${formatPeso(p.amount)}`}
                  className="grid size-12 place-items-center rounded-control border border-line text-ink-soft hover:border-danger hover:text-danger disabled:opacity-60"
                >
                  <Trash size={22} aria-hidden />
                </button>
              </li>
            );
          })}
        </ol>
      )}

      {over > 0 && (
        <p role="alert" className="flex items-start gap-2 rounded-control border border-warn bg-warn/10 px-4 py-3 text-base font-semibold text-ink">
          <Warning size={22} className="mt-0.5 shrink-0 text-warn" aria-hidden />
          Payments exceed the new total by {formatPeso(over)}. Remove a payment.
        </p>
      )}

      {change > 0 && (
        <p className="flex items-baseline justify-between rounded-control bg-brand-tint px-4 py-3 text-lg font-bold text-brand-strong">
          <span>Change to give</span>
          <span data-testid="split-change" className="text-2xl tabular-nums">{formatPeso(change)}</span>
        </p>
      )}

      {cents(left) > 0 && (
        <div className="flex flex-col gap-4 border-t border-line pt-5">
          <h2 className="text-lg font-bold">{parts.length === 0 ? 'First payment' : 'Next payment'}</h2>
          <div role="radiogroup" aria-label="Method for this part" className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {METHODS.filter((m) => m.method !== PaymentMethod.UtangCredit || creditOn).map((m) => {
              const blocked = m.method === PaymentMethod.UtangCredit && hasUtang;
              return (
                <button
                  key={m.method}
                  type="button"
                  role="radio"
                  aria-checked={method === m.method}
                  disabled={blocked || full}
                  onClick={() => pick(m.method)}
                  className={`min-h-14 rounded-control px-3 text-base font-semibold disabled:opacity-50 ${method === m.method ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`}
                >
                  {m.label}
                </button>
              );
            })}
          </div>
          {hasUtang && <p className="text-sm text-ink-soft">Only one Utang part per sale.</p>}
          {full && <p className="text-sm text-ink-soft">A sale can be split into at most {MAX_SPLIT_PARTS} payments.</p>}

          <FormField label="Amount for this part" error={draftError ?? undefined}>
            <input
              inputMode="decimal"
              autoComplete="off"
              value={amount}
              onChange={(e) => {
                edited();
                setAmount(e.target.value.replace(/[^0-9.]/g, ''));
              }}
              className="h-14 w-full rounded-control border border-ink-soft/40 bg-surface px-4 text-right text-2xl font-bold tabular-nums focus:border-brand"
            />
          </FormField>

          {method === PaymentMethod.Cash && amountValue > 0 && (
            <CashKeypad
              total={amountValue}
              value={tendered}
              onChange={(v) => {
                edited();
                setTendered(v);
              }}
            />
          )}

          {(method === PaymentMethod.BankTransfer || method === PaymentMethod.ManualGcashQr) && (
            <FormField label="Reference (optional)" hint="The transaction or reference number, to match it later">
              <input autoComplete="off" value={reference} maxLength={MAX_REFERENCE_LENGTH + 20} onChange={(e) => {
                  edited();
                  setReference(e.target.value);
                }} className={controlClass} />
            </FormField>
          )}

          {method === PaymentMethod.ManualGcashQr && (
            <div className="flex flex-col gap-3">
              <p className="text-base">Show this QR to the customer. Add the part only after you see {amountValue > 0 ? formatPeso(amountValue) : 'the payment'} land.</p>
              <GcashQr branch={branch} />
            </div>
          )}

          {method === PaymentMethod.UtangCredit &&
            (ledgers.isError ? (
              <ErrorState title="Customer accounts could not be loaded" message={userMessage(ledgers.error)} onRetry={() => void ledgers.refetch()} />
            ) : ledgers.isPending ? (
              <Skeleton className="h-14 w-full" />
            ) : activeLedgers.length === 0 ? (
              <p className="text-base text-ink-soft">No customer accounts yet. A manager can add one.</p>
            ) : (
              <>
                <FormField label="Customer account" hint="The server checks the credit limit when you complete the sale">
                  <select
                    value={ledgerId}
                    onChange={(e) => {
                      edited();
                      setLedgerId(e.target.value);
                    }} className={controlClass}>
                    <option value="">Choose a customer</option>
                    {activeLedgers.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.customerFullName} ({formatPeso(Math.max(0, l.creditLimit - l.balance))} available)
                      </option>
                    ))}
                  </select>
                </FormField>
                {ledger && amountValue > available && (
                  <p className="flex items-start gap-2 rounded-control bg-warn/10 px-4 py-3 text-base font-medium text-ink">
                    <Warning size={22} className="mt-0.5 shrink-0 text-warn" aria-hidden />
                    {formatPeso(amountValue)} is more than the {formatPeso(available)} available. The sale may be refused for the credit limit.
                  </p>
                )}
              </>
            ))}

          <button
            type="button"
            onClick={add}
            disabled={!canAdd || busy}
            className="h-14 rounded-control border-2 border-brand bg-surface px-6 text-lg font-bold text-brand-strong hover:bg-brand-tint active:translate-y-px disabled:border-line disabled:text-ink-soft disabled:opacity-70"
          >
            Add payment
          </button>
        </div>
      )}

      {error && (
        <p role="alert" className="flex items-start gap-2 rounded-control border border-danger/40 bg-danger/10 px-4 py-3 text-base font-medium text-danger">
          <Warning size={22} className="mt-0.5 shrink-0" aria-hidden />
          {error}
        </p>
      )}

      <div className="flex flex-col-reverse gap-3 sm:flex-row sm:items-center">
        <button type="button" onClick={onCancel} disabled={busy} className="h-14 rounded-control px-4 text-base font-semibold text-ink-soft underline-offset-4 hover:underline disabled:opacity-60">
          Back to single payment
        </button>
        <button
          ref={completeRef}
          type="button"
          onClick={() => ready && onComplete(parts)}
          disabled={!ready || busy}
          className="flex h-16 flex-1 items-center justify-center gap-2 rounded-control bg-brand px-6 text-xl font-bold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-50"
        >
          {ready && !busy && <CheckCircle size={26} weight="bold" aria-hidden />}
          {busy ? 'Recording payment...' : `Complete sale ${formatPeso(total)}`}
        </button>
      </div>
    </section>
  );
}

function Figure({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-control border border-line px-4 py-3">
      <dt className="text-sm font-semibold text-ink-soft">{label}</dt>
      <dd className="text-xl font-bold tabular-nums sm:text-2xl">{value}</dd>
    </div>
  );
}
