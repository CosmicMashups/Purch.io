import { useCallback, useMemo } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from '../../components/feedback/toastStore';
import { userMessage } from '../../lib/apiError';
import { pendingRows, type AddPreview, type createAddQueue, type PendingRow } from './addQueue';
import type { AddLineRequest, Transaction } from './types';

type AddQueue = ReturnType<typeof createAddQueue>;

export interface CartAdds {
  /** Records the tap at once and sends it in order. Never waits, never throws. */
  add: (label: string, request: AddLineRequest, preview?: AddPreview) => void;
  /** Adds still on their way: item and quantity, with the device's preview price when it has one. */
  pending: PendingRow[];
}

/**
 * Connects an add queue to one server cart. Each answer replaces the cached cart, and any older read of the
 * cart still in flight is cancelled first so it can never land on top of a newer answer.
 */
export function useCartAdds(
  queue: AddQueue,
  send: (batchId: string, requests: AddLineRequest[]) => Promise<Transaction>,
  cartKey: readonly unknown[],
  options: { retry?: boolean } = {},
): CartAdds {
  const qc = useQueryClient();
  const waiting = queue.useQueue((s) => s.waiting);
  const inFlight = queue.useQueue((s) => s.inFlight);

  const add = useCallback(
    (label: string, request: AddLineRequest, preview?: AddPreview) =>
      queue.add(
        {
          send,
          retry: options.retry,
          onCart: (cart) => qc.setQueryData(cartKey, cart),
          beforeSend: () => qc.cancelQueries({ queryKey: cartKey }),
          onError: (labels, error) => toast.error(`${labels.join(', ')} could not be added. ${userMessage(error)}`),
        },
        label,
        request,
        preview,
      ),
    [qc, queue, send, cartKey, options.retry],
  );

  const pending = useMemo(() => pendingRows(inFlight, waiting), [inFlight, waiting]);
  return { add, pending };
}
