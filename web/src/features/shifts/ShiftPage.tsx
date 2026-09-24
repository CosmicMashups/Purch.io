import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ConfirmModal } from '../../components/ConfirmModal';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { toast } from '../../components/feedback/toastStore';
import { FormField, PrimaryButton, controlClass } from '../../components/forms/FormField';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime } from '../../lib/dates';
import { userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import { formatPeso } from '../dashboard/format';
import { DeviceRequired } from '../pos/components/DeviceRequired';
import { useCloseShift, useCurrentShift, useOpenShift } from './queries';
import { cashOutcome, describeOutcome, parseCashAmount } from './reconciliation';
import type { Shift } from './types';

export function ShiftPage() {
  const { claims } = useSession();
  if (!claims?.deviceId) return <DeviceRequired />;
  return <ShiftScreen />;
}

function ShiftScreen() {
  const shift = useCurrentShift();
  const [closed, setClosed] = useState<Shift | null>(null);

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <PageHeader title="Shift and cash drawer" backTo={{ to: '/sell', label: 'Sell' }} />
      {closed ? (
        <ClosedSummary shift={closed} />
      ) : shift.isPending ? (
        <Skeleton className="h-64 w-full" />
      ) : shift.isError ? (
        <ErrorState title="The shift could not be loaded" message={userMessage(shift.error)} onRetry={() => void shift.refetch()} />
      ) : shift.data === null ? (
        <OpenForm />
      ) : (
        <CloseForm shift={shift.data} onClosed={setClosed} />
      )}
    </div>
  );
}

function OpenForm() {
  const open = useOpenShift();
  const [text, setText] = useState('');
  const [error, setError] = useState<string | undefined>();

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const parsed = parseCashAmount(text);
    if (!parsed.ok) return setError(parsed.message);
    setError(undefined);
    open.mutate(parsed.value, { onSuccess: () => toast.success('Shift opened') });
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-5 rounded-panel border border-line bg-surface p-6">
      <p className="text-base text-ink-soft">No shift is open on this device. Count the cash in the drawer, then open the shift.</p>
      <FormField label="Opening cash count (PHP)" error={error}>
        <input inputMode="decimal" value={text} onChange={(e) => setText(e.target.value)} className={controlClass} />
      </FormField>
      <div>
        <PrimaryButton type="submit" busy={open.isPending}>
          {open.isPending ? 'Opening...' : 'Open shift'}
        </PrimaryButton>
      </div>
    </form>
  );
}

function CloseForm({ shift, onClosed }: { shift: Shift; onClosed: (shift: Shift) => void }) {
  const close = useCloseShift();
  const [count, setCount] = useState('');
  const [notes, setNotes] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState<string | undefined>();
  const [confirming, setConfirming] = useState(false);

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const parsed = parseCashAmount(count);
    if (!parsed.ok) return setError(parsed.message);
    setError(undefined);
    setConfirming(true);
  }

  function confirm() {
    const parsed = parseCashAmount(count);
    if (!parsed.ok) return;
    setConfirming(false);
    // A missing or wrong manager PIN comes back from the server as a plain message and the form stays open.
    close.mutate(
      { closingCashAmount: parsed.value, handoverNotes: notes.trim() || null, approverPin: pin.trim() || null },
      { onSuccess: onClosed },
    );
  }

  return (
    <>
      <section className="rounded-panel border border-line bg-surface p-6">
        <h2 className="text-xl font-bold">Shift open</h2>
        <dl className="mt-3 grid gap-2 text-base sm:grid-cols-2">
          <div>
            <dt className="text-sm text-ink-soft">Opened by</dt>
            <dd className="font-semibold">{shift.openedByUserName}</dd>
          </div>
          <div>
            <dt className="text-sm text-ink-soft">Opened at</dt>
            <dd className="font-semibold">{formatDateTime(shift.openedAt)}</dd>
          </div>
          <div>
            <dt className="text-sm text-ink-soft">Opening cash</dt>
            <dd className="font-semibold tabular-nums">{formatPeso(shift.openingCashAmount)}</dd>
          </div>
        </dl>
      </section>

      <form onSubmit={submit} noValidate className="flex flex-col gap-5 rounded-panel border border-line bg-surface p-6">
        <h2 className="text-xl font-bold">Close the shift</h2>
        <FormField label="Closing cash count (PHP)" hint="Count the drawer. The system compares it with what it expects." error={error}>
          <input inputMode="decimal" value={count} onChange={(e) => setCount(e.target.value)} className={controlClass} />
        </FormField>
        <FormField label="Handover notes (optional)">
          <input value={notes} onChange={(e) => setNotes(e.target.value)} className={controlClass} />
        </FormField>
        <FormField label="Manager or admin PIN" hint="Only needed if the cash count does not match">
          <input type="password" inputMode="numeric" autoComplete="off" value={pin} onChange={(e) => setPin(e.target.value)} className={controlClass} />
        </FormField>
        <div>
          <PrimaryButton type="submit" busy={close.isPending}>
            {close.isPending ? 'Closing...' : 'Close shift'}
          </PrimaryButton>
        </div>
      </form>

      <ConfirmModal
        open={confirming}
        title="Close this shift?"
        description="The count is final once the shift is closed. A new shift has to be opened to keep selling on this device's drawer."
        confirmLabel="Close shift"
        onConfirm={confirm}
        onCancel={() => setConfirming(false)}
      />
    </>
  );
}

function ClosedSummary({ shift }: { shift: Shift }) {
  const outcome = cashOutcome(shift);
  return (
    <section aria-label="Shift summary" className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-6">
      <h2 className="text-2xl font-bold tracking-tight">Shift closed</h2>
      <p role="status" className={`rounded-control px-4 py-3 text-lg font-bold ${outcome.kind === 'matched' ? 'bg-brand-tint text-brand-strong' : 'bg-amber-100 text-amber-900'}`}>
        {describeOutcome(outcome)}
      </p>
      <dl className="grid gap-3 text-base sm:grid-cols-3">
        <Figure label="Opening cash" value={shift.openingCashAmount} />
        <Figure label="Expected cash" value={shift.expectedCashAmount} />
        <Figure label="Counted cash" value={shift.closingCashAmount} />
      </dl>
      {shift.approvedByUserName && <p className="text-base">Approved by {shift.approvedByUserName}</p>}
      {shift.handoverNotes && <p className="text-base text-ink-soft">Notes: {shift.handoverNotes}</p>}
      <div>
        <Link to="/sell" className="inline-grid h-12 place-items-center rounded-control bg-brand px-6 text-base font-semibold text-on-brand hover:bg-brand-strong">
          Done
        </Link>
      </div>
    </section>
  );
}

function Figure({ label, value }: { label: string; value: number | null }) {
  return (
    <div>
      <dt className="text-sm text-ink-soft">{label}</dt>
      <dd className="font-semibold tabular-nums">{value === null ? 'Not recorded' : formatPeso(value)}</dd>
    </div>
  );
}
