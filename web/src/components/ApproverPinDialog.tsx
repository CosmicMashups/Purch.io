import { useEffect, useId, useRef, useState } from 'react';
import { controlClass } from './forms/FormField';

interface ApproverPinDialogProps {
  open: boolean;
  /** What is being approved, e.g. "Clear the cart" or "Remove Ramen". */
  title: string;
  /** The server's own reason this needs a PIN, or the first-ask prompt — see approverPin.ts. */
  message: string;
  /** True once a submitted PIN has been refused, so the same message reads as a correction rather than an opening instruction. */
  isError?: boolean;
  busy?: boolean;
  onSubmit: (pin: string) => void;
  onCancel: () => void;
}

/**
 * The manager/admin PIN check behind a void, refund or a kitchen-order edit. Modeled on ConfirmModal's own
 * chrome (focus starts away from the destructive default, Escape backs out) with one input in between:
 * nothing here is decorative, since this is the one screen standing between a cashier and an approved
 * change to the sale.
 */
export function ApproverPinDialog({ open, title, message, isError = false, busy = false, onSubmit, onCancel }: ApproverPinDialogProps) {
  const titleId = useId();
  const messageId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [pin, setPin] = useState('');

  useEffect(() => {
    if (!open) return;
    inputRef.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onCancel();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onCancel]);

  if (!open) return null;

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (pin.trim() && !busy) onSubmit(pin.trim());
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4">
      <form
        onSubmit={submit}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={messageId}
        className="w-full max-w-sm rounded-panel bg-surface p-6 shadow-xl"
      >
        <h2 id={titleId} className="text-xl font-bold">
          {title}
        </h2>
        <p id={messageId} role={isError ? 'alert' : undefined} className={`mt-2 text-base ${isError ? 'font-medium text-danger' : 'text-ink-soft'}`}>
          {message}
        </p>

        <label htmlFor={`${titleId}-pin`} className="mt-4 block text-base font-semibold">
          Manager or admin PIN
        </label>
        <input
          ref={inputRef}
          id={`${titleId}-pin`}
          type="password"
          inputMode="numeric"
          autoComplete="off"
          aria-invalid={isError}
          value={pin}
          onChange={(e) => setPin(e.target.value)}
          disabled={busy}
          className={`${controlClass} mt-1 tracking-[0.3em]`}
        />

        <div className="mt-6 flex justify-end gap-3">
          <button type="button" onClick={onCancel} disabled={busy} className="h-12 rounded-control border border-line px-6 text-base font-semibold hover:border-brand disabled:opacity-60">
            Cancel
          </button>
          <button
            type="submit"
            disabled={busy || !pin.trim()}
            className="h-12 rounded-control bg-brand px-6 text-base font-semibold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-60"
          >
            {busy ? 'Checking...' : 'Approve'}
          </button>
        </div>
      </form>
    </div>
  );
}
