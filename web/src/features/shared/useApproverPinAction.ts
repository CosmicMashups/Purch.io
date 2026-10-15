import { useState } from 'react';
import { toast } from '../../components/feedback/toastStore';
import { userMessage } from '../../lib/apiError';
import { needsApproverPin, approverPinPrompt } from '../pos/approverPin';

interface Options<T> {
  /** Performs the action; `pin` is absent on the first try and set on retries from the dialog. */
  send: (pin?: string) => Promise<T>;
  onDone?: (result: T) => void;
  /** Turns a non-PIN failure into the toast text (defaults to userMessage). */
  describeError?: (error: unknown) => string;
}

/**
 * Cart-agnostic approver-PIN flow: try without a PIN, open the dialog only when the server demands one,
 * keep it open (isError) on a wrong PIN, and close on success. Spread `dialogProps` into ApproverPinDialog.
 */
export function useApproverPinAction<T>({ send, onDone, describeError = userMessage }: Options<T>) {
  const [pending, setPending] = useState<{ title: string; message: string; isError: boolean } | null>(null);
  const [busy, setBusy] = useState(false);

  /** `askFirst` opens the dialog before any send, for actions the server always gates. */
  async function run(title: string, askFirst?: string): Promise<void> {
    if (askFirst) {
      setPending({ title, message: askFirst, isError: false });
      return;
    }
    try {
      onDone?.(await send());
      setPending(null);
    } catch (error) {
      if (needsApproverPin(error)) setPending({ title, message: approverPinPrompt(error), isError: false });
      else toast.error(describeError(error));
    }
  }

  async function submitPin(pin: string) {
    if (!pending) return;
    setBusy(true);
    try {
      onDone?.(await send(pin));
      setPending(null);
    } catch (error) {
      if (needsApproverPin(error)) {
        setPending({ ...pending, message: approverPinPrompt(error), isError: true });
      } else {
        toast.error(describeError(error));
        setPending(null);
      }
    } finally {
      setBusy(false);
    }
  }

  return {
    run,
    dialogProps: pending && { ...pending, busy, onSubmit: submitPin, onCancel: () => setPending(null) },
  };
}
