import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ConfirmModal } from '../../components/ConfirmModal';
import { Modal } from '../../components/Modal';
import { toast } from '../../components/feedback/toastStore';
import { EditorCard } from '../../components/forms/EditorCard';
import { FormField, PrimaryButton, controlClass } from '../../components/forms/FormField';
import { ListCard, Pill, QueryList } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { AsyncPanel } from '../dashboard/components/AsyncPanel';
import { formatPeso } from '../dashboard/format';
import { useAnonymizeCustomer, useCreateCredit, useCreditLedgers, useCreditReminders, useRecordCreditPayment, useUpdateCreditLimit } from './queries';
import { availableCredit, canAnonymize, customerSchema, limitProblem, paymentProblem, type CustomerForm } from './rules';
import type { CreditLedger } from './types';

const linkButton = 'h-12 text-base font-semibold text-brand-strong underline disabled:opacity-40 disabled:no-underline';

export function CustomersPage() {
  const ledgers = useCreditLedgers();
  const reminders = useCreditReminders(7);
  const anonymize = useAnonymizeCustomer();
  const [paying, setPaying] = useState<CreditLedger | null>(null);
  const [limiting, setLimiting] = useState<CreditLedger | null>(null);
  const [erasing, setErasing] = useState<CreditLedger | null>(null);

  function confirmErase() {
    if (!erasing) return;
    const target = erasing;
    setErasing(null);
    anonymize.mutate(target.id, { onSuccess: () => toast.success('The customer\'s details were erased') });
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Customers" subtitle="Customer credit (utang) accounts" backTo={{ to: '/business', label: 'Business' }} />

      <AsyncPanel
        title="Payments due soon"
        subtitle="Balances due within 7 days, or already overdue"
        query={reminders}
        isEmpty={(d) => d.length === 0}
        emptyMessage="Nothing is due in the next 7 days."
      >
        {(d) => (
          <ul className="flex flex-col divide-y divide-line">
            {d.map((r) => (
              <li key={r.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
                <div>
                  <p className="text-base font-semibold">{r.customerFullName}</p>
                  <p className="text-sm text-ink-soft">
                    {r.customerPhoneNumber}, due {r.dueDate}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-base font-semibold tabular-nums">{formatPeso(r.balance)}</span>
                  {r.isOverdue && <Pill tone="danger">Overdue</Pill>}
                </div>
              </li>
            ))}
          </ul>
        )}
      </AsyncPanel>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
        <QueryList
          query={ledgers}
          errorTitle="Customers could not be loaded"
          emptyMessage="No customer accounts yet."
          renderRow={(l) => (
            <ListCard key={l.id}>
              <div className="min-w-0">
                <p className="text-base font-semibold">{l.customerFullName}</p>
                <p className="text-sm text-ink-soft">{l.customerPhoneNumber}</p>
                <p className="text-base tabular-nums">
                  Owes {formatPeso(l.balance)} of {formatPeso(l.creditLimit)} ({formatPeso(availableCredit(l))} available)
                </p>
                {l.dueDate && <p className="text-sm text-ink-soft">Due {l.dueDate}</p>}
                <div className="mt-2 flex flex-wrap gap-x-5">
                  <button type="button" className={linkButton} disabled={!l.isActive || l.balance <= 0} onClick={() => setPaying(l)}>
                    Record payment
                  </button>
                  <button type="button" className={linkButton} disabled={!l.isActive} onClick={() => setLimiting(l)}>
                    Change limit
                  </button>
                  <button type="button" className={linkButton} disabled={!canAnonymize(l)} title={l.balance !== 0 ? 'The balance must be zero first' : undefined} onClick={() => setErasing(l)}>
                    Erase details
                  </button>
                </div>
              </div>
              {!l.isActive && <Pill>Erased</Pill>}
            </ListCard>
          )}
        />
        <NewCustomer />
      </div>

      {paying && <PaymentDialog ledger={paying} onClose={() => setPaying(null)} />}
      {limiting && <LimitDialog ledger={limiting} onClose={() => setLimiting(null)} />}
      <ConfirmModal
        open={erasing !== null}
        destructive
        title="Erase this customer's details?"
        description={erasing ? `${erasing.customerFullName}'s name, phone number and address are removed for good. The sales history stays. This cannot be undone.` : undefined}
        confirmLabel="Erase details"
        onConfirm={confirmErase}
        onCancel={() => setErasing(null)}
      />
    </div>
  );
}

function NewCustomer() {
  const create = useCreateCredit();
  const { register, handleSubmit, reset, formState: { errors } } = useForm<CustomerForm>({
    resolver: zodResolver(customerSchema),
    defaultValues: { customerFullName: '', customerPhoneNumber: '', customerAddress: '', dueDate: '' },
  });

  const submit = handleSubmit((v) =>
    create.mutate(
      {
        customerFullName: v.customerFullName,
        customerPhoneNumber: v.customerPhoneNumber,
        customerAddress: v.customerAddress.trim() || null,
        creditLimit: v.creditLimit,
        dueDate: v.dueDate || null,
      },
      { onSuccess: () => { toast.success('Customer added'); reset(); } },
    ),
  );

  return (
    <EditorCard title="customer" editing={false} busy={create.isPending} onSubmit={submit} onCancel={() => reset()}>
      <FormField label="Full name" error={errors.customerFullName?.message}>
        <input {...register('customerFullName')} className={controlClass} />
      </FormField>
      <FormField label="Phone number" error={errors.customerPhoneNumber?.message}>
        <input inputMode="tel" {...register('customerPhoneNumber')} className={controlClass} />
      </FormField>
      <FormField label="Address (optional)" hint="Only what you need. We keep as little personal information as possible.">
        <input {...register('customerAddress')} className={controlClass} />
      </FormField>
      <FormField label="Credit limit (PHP)" error={errors.creditLimit?.message}>
        <input type="number" inputMode="decimal" step="0.01" {...register('creditLimit', { valueAsNumber: true })} className={controlClass} />
      </FormField>
      <FormField label="Due date (optional)" error={errors.dueDate?.message}>
        <input type="date" {...register('dueDate')} className={controlClass} />
      </FormField>
    </EditorCard>
  );
}

function PaymentDialog({ ledger, onClose }: { ledger: CreditLedger; onClose: () => void }) {
  const record = useRecordCreditPayment();
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | undefined>();

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const problem = paymentProblem(amount, ledger.balance);
    if (problem) return setError(problem);
    setError(undefined);
    record.mutate({ id: ledger.id, amount: Number(amount), note: note.trim() || null }, { onSuccess: () => { toast.success('Payment recorded'); onClose(); } });
  }

  return (
    <Modal open title={`Payment from ${ledger.customerFullName}`} onClose={onClose} footer={<PrimaryButton type="submit" form="credit-payment-form" busy={record.isPending}>{record.isPending ? 'Saving...' : 'Record payment'}</PrimaryButton>}>
      <form id="credit-payment-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
        <p className="text-base">Owes {formatPeso(ledger.balance)}</p>
        <FormField label="Amount paid (PHP)" error={error}>
          <input inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} className={controlClass} />
        </FormField>
        <FormField label="Note (optional)">
          <input value={note} onChange={(e) => setNote(e.target.value)} className={controlClass} />
        </FormField>
      </form>
    </Modal>
  );
}

function LimitDialog({ ledger, onClose }: { ledger: CreditLedger; onClose: () => void }) {
  const update = useUpdateCreditLimit();
  const [limit, setLimit] = useState(String(ledger.creditLimit));
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | undefined>();

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const problem = limitProblem(limit);
    if (problem) return setError(problem);
    setError(undefined);
    update.mutate({ id: ledger.id, creditLimit: Number(limit), reason: reason.trim() || null }, { onSuccess: () => { toast.success('Credit limit changed'); onClose(); } });
  }

  return (
    <Modal open title={`Credit limit for ${ledger.customerFullName}`} onClose={onClose} footer={<PrimaryButton type="submit" form="credit-limit-form" busy={update.isPending}>{update.isPending ? 'Saving...' : 'Save limit'}</PrimaryButton>}>
      <form id="credit-limit-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
        <FormField label="New credit limit (PHP)" error={error}>
          <input inputMode="decimal" value={limit} onChange={(e) => setLimit(e.target.value)} className={controlClass} />
        </FormField>
        <FormField label="Reason (optional)" hint="Kept in the audit log">
          <input value={reason} onChange={(e) => setReason(e.target.value)} className={controlClass} />
        </FormField>
      </form>
    </Modal>
  );
}
