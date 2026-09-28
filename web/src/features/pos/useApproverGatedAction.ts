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
  retry: (pin: string) => Promise<Transaction>;
  /** Runs once the action actually lands, whether on the first try or after a PIN — e.g. a success toast. */
  onLanded?: (cart: Transaction) => void;
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

  function land(cart: Transaction, onLanded?: (cart: Transaction) => void) {
    qc.setQueryData(posKeys.cart, cart);
    onLanded?.(cart);
    setPending(null);
  }

  /** `title` names the action for the dialog, e.g. "Clear the cart" or "Remove Ramen". `onLanded` runs once
   * the action actually succeeds, on the first try or after a PIN — a success toast, typically. */
  async function run(title: string, send: (approverPin?: string) => Promise<Transaction>, onLanded?: (cart: Transaction) => void): Promise<void> {
    try {
      land(await send(), onLanded);
    } catch (error) {
      if (needsApproverPin(error)) {
        setPending({ title, message: approverPinPrompt(error), isError: false, retry: send, onLanded });
      } else {
        toast.error(userMessage(error));
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
        toast.error(userMessage(error));
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
