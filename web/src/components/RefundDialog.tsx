import { useId, useRef, useState } from 'react';
import { ApiError, userMessage } from '../lib/apiError';
import { controlClass } from './forms/FormField';

interface RefundDialogProps {
  /** The total being refunded, shown so a cashier can't confirm the wrong sale. */
  total: string;
  busy: boolean;
  /** Rejects with the ApiError so the dialog can tell an approver-PIN refusal from anything else. */
  onSubmit: (reason: string, approverPin: string) => Promise<void>;
  onCancel: () => void;
}

/**
 * Refund reason and manager/admin PIN, collected together: unlike voiding an open cart, a refund is never
 * free, so there is no point asking for the PIN only after a first refusal. Both fields stay editable if
 * the server refuses either one, and the dialog only closes once the refund has actually gone through.
 */
export function RefundDialog({ total, busy, onSubmit, onCancel }: RefundDialogProps) {
  const titleId = useId();
  const reasonRef = useRef<HTMLTextAreaElement>(null);
  const [reason, setReason] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState<string | null>(null);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!reason.trim() || !pin.trim() || busy) return;
    setError(null);
    try {
      await onSubmit(reason.trim(), pin.trim());
    } catch (submitError) {
      // A refused reason, a wrong/missing PIN, or a lockout all reopen this same dialog with the
      // server's own message; anything else is the caller's problem to show (it closes the dialog first).
      if (submitError instanceof ApiError) {
        setError(userMessage(submitError));
      } else {
        throw submitError;
      }
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4">
      <form onSubmit={submit} role="dialog" aria-modal="true" aria-labelledby={titleId} className="w-full max-w-sm rounded-panel bg-surface p-6 shadow-xl">
        <h2 id={titleId} className="text-xl font-bold">
          Refund {total}
        </h2>
        <p className="mt-2 text-base text-ink-soft">This marks the whole sale Refunded and needs a manager or admin to approve it.</p>

        <label htmlFor={`${titleId}-reason`} className="mt-4 block text-base font-semibold">
          Reason
        </label>
        <textarea
          ref={reasonRef}
          id={`${titleId}-reason`}
          rows={2}
          autoFocus
          disabled={busy}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          className={`${controlClass} mt-1 h-auto py-2`}
        />

        <label htmlFor={`${titleId}-pin`} className="mt-4 block text-base font-semibold">
          Manager or admin PIN
        </label>
        <input
          id={`${titleId}-pin`}
          type="password"
          inputMode="numeric"
          autoComplete="off"
          disabled={busy}
          value={pin}
          onChange={(e) => setPin(e.target.value)}
          className={`${controlClass} mt-1 tracking-[0.3em]`}
        />

        {error && (
          <p role="alert" className="mt-2 text-sm font-medium text-danger">
            {error}
          </p>
        )}

        <div className="mt-6 flex justify-end gap-3">
          <button type="button" onClick={onCancel} disabled={busy} className="h-12 rounded-control border border-line px-6 text-base font-semibold hover:border-brand disabled:opacity-60">
            Cancel
          </button>
          <button
            type="submit"
            disabled={busy || !reason.trim() || !pin.trim()}
            className="h-12 rounded-control bg-danger px-6 text-base font-semibold text-white hover:bg-red-700 active:translate-y-px disabled:opacity-60"
          >
            {busy ? 'Refunding...' : 'Refund'}
          </button>
        </div>
      </form>
    </div>
  );
}
