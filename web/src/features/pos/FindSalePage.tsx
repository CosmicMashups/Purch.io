import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { FormField, PrimaryButton, controlClass } from '../../components/forms/FormField';
import { ErrorState } from '../../components/ErrorState';
import { userMessage } from '../../lib/apiError';
import { formatPeso } from '../dashboard/format';
import { posApi } from './api';
import { usePosStore } from './posStore';
import type { Transaction } from './types';

/**
 * Looking up an older sale by its receipt number — for a refund or exchange the cashier can't reach from
 * an open cart or the receipt they just printed (an earlier day, a different terminal). Receipt numbers
 * are only unique per device, so a search can rarely come back with more than one sale; the cashier picks
 * from the short list in that case instead of the lookup guessing.
 */
export function FindSalePage() {
  const navigate = useNavigate();
  const showReceipt = usePosStore((s) => s.showReceipt);
  const [value, setValue] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [matches, setMatches] = useState<Transaction[] | null>(null);

  async function search(event: React.FormEvent) {
    event.preventDefault();
    const receiptNumber = Number(value.trim());
    if (!Number.isInteger(receiptNumber) || receiptNumber <= 0) {
      setError('Enter the receipt number as it appears on the printed receipt.');
      return;
    }

    setBusy(true);
    setError(null);
    setMatches(null);
    try {
      const found = await posApi.findByReceiptNumber(receiptNumber);
      if (found.length === 0) {
        setError(`No completed sale found with receipt number ${receiptNumber}.`);
      } else if (found.length === 1) {
        open(found[0]);
      } else {
        setMatches(found);
      }
    } catch (searchError) {
      setError(userMessage(searchError));
    } finally {
      setBusy(false);
    }
  }

  function open(sale: Transaction) {
    showReceipt(sale);
    navigate('/sell/receipt');
  }

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <div className="flex flex-col gap-1">
        <Link to="/sell" className="text-base font-semibold text-brand-strong underline">
          Back to Sell
        </Link>
        <h1 className="text-3xl font-bold tracking-tight">Find a sale</h1>
        <p className="text-base text-ink-soft">Look up a completed sale by its receipt number to refund or exchange it.</p>
      </div>

      <form onSubmit={search} className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-6">
        <FormField label="Receipt number">
          <input
            type="text"
            inputMode="numeric"
            autoFocus
            disabled={busy}
            value={value}
            onChange={(e) => setValue(e.target.value)}
            className={controlClass}
            placeholder="e.g. 1047"
          />
        </FormField>
        <PrimaryButton type="submit" busy={busy} disabled={busy || !value.trim()}>
          {busy ? 'Searching...' : 'Find sale'}
        </PrimaryButton>
      </form>

      {error && <ErrorState title="Sale not found" message={error} />}

      {matches && matches.length > 1 && (
        <div className="flex flex-col gap-3">
          <p className="text-base font-semibold">
            {matches.length} sales share receipt number {value.trim()} (different terminals number independently). Pick the one you mean.
          </p>
          <ul className="flex flex-col gap-2">
            {matches.map((sale) => (
              <li key={sale.id}>
                <button
                  type="button"
                  onClick={() => open(sale)}
                  className="flex w-full items-center justify-between rounded-control border border-line bg-surface px-4 py-3 text-left hover:border-brand"
                >
                  <span className="text-base font-semibold">
                    {sale.lines[0]?.itemName ?? 'Sale'}
                    {sale.lines.length > 1 ? ` + ${sale.lines.length - 1} more` : ''}
                  </span>
                  <span className="text-base font-bold tabular-nums">{formatPeso(sale.totalAmount)}</span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
