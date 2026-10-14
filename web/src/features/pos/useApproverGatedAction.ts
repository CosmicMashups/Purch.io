import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from '../../components/feedback/toastStore';
import { userMessage } from '../../lib/apiError';
import { needsApproverPin, approverPinPrompt } from './approverPin';
import { posKeys } from './queries';
import type { Transaction } from './types';

interface PendingApproval {
  title: string;
  message: string;
  isError: boolean;
  /** Retries the same action, this time with the PIN the cashier just typed. */
  retry: (pin: string) => Promise<Transaction | void>;
  describeError: (error: unknown) => string;
  /** Runs once the action actually lands, whether on the first try or after a PIN — e.g. a success toast. */
  onLanded?: (cart: Transaction | void) => void;
}

/**
 * Runs a cart action that may come back needing a manager/admin PIN (void, or an edit on a kitchen order
 * the kitchen hasn't started yet — see ApproverAuthorizationService on the server). The action itself never
 * carries a PIN up front; only a refusal opens the dialog, so the common case (an ordinary cart, or an
 * Admin/Manager who needs none) never sees it at all.
 */
export function useApproverGatedAction() {
  const qc = useQueryClient();
  const [pending, setPending] = useState<PendingApproval | null>(null);
  const [busy, setBusy] = useState(false);

  /** An action that returns the cart (void, remove) lands it in the cache; one that returns nothing (discarding a parked cart) does not. */
  function land(cart: Transaction | void, onLanded?: (cart: Transaction | void) => void) {
    if (cart) qc.setQueryData(posKeys.cart, cart);
    onLanded?.(cart);
    setPending(null);
  }

  /** `title` names the action for the dialog, e.g. "Clear the cart" or "Remove Ramen". `onLanded` runs once
   * the action actually succeeds, on the first try or after a PIN — a success toast, typically. */
  /** `describeError` turns a non-PIN failure into the toast text (defaults to userMessage); the error itself is never re-labelled. */
  async function run<T extends Transaction | void>(
    title: string,
    send: (approverPin?: string) => Promise<T>,
    onLanded?: (cart: T) => void,
    describeError: (error: unknown) => string = userMessage,
  ): Promise<void> {
    try {
      land(await send(), onLanded as PendingApproval['onLanded']);
    } catch (error) {
      if (needsApproverPin(error)) {
        setPending({ title, message: approverPinPrompt(error), isError: false, retry: send, onLanded: onLanded as PendingApproval['onLanded'], describeError });
      } else {
        toast.error(describeError(error));
      }
    }
  }

  async function submitPin(pin: string) {
    if (!pending) return;
    setBusy(true);
    try {
      land(await pending.retry(pin), pending.onLanded);
    } catch (error) {
      if (needsApproverPin(error)) {
        setPending({ ...pending, message: approverPinPrompt(error), isError: true });
      } else {
        toast.error(pending.describeError(error));
        setPending(null);
      }
    } finally {
      setBusy(false);
    }
  }

  return {
    run,
    dialogProps: pending && { title: pending.title, message: pending.message, isError: pending.isError, busy, onSubmit: submitPin, onCancel: () => setPending(null) },
  };
}
