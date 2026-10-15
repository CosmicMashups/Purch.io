import { useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { userMessage } from '../../lib/apiError';
import { useApproverPinAction } from '../shared/useApproverPinAction';
import { posKeys } from './queries';
import type { Transaction } from './types';

interface Call {
  send: (approverPin?: string) => Promise<Transaction | void>;
  onLanded?: (cart: Transaction | void) => void;
  describeError: (error: unknown) => string;
}

/**
 * Runs a cart action that may come back needing a manager/admin PIN (void, or an edit on a kitchen order
 * the kitchen hasn't started yet — see ApproverAuthorizationService on the server). Cart-specific part of
 * the shared useApproverPinAction: it lands the returned cart in the cache.
 */
export function useApproverGatedAction() {
  const qc = useQueryClient();
  const call = useRef<Call | null>(null);

  const action = useApproverPinAction<Transaction | void>({
    send: (pin) => call.current!.send(pin),
    onDone: (cart) => {
      // An action that returns the cart (void, remove) lands it in the cache; one that returns nothing does not.
      if (cart) qc.setQueryData(posKeys.cart, cart);
      call.current?.onLanded?.(cart);
    },
    describeError: (error) => call.current!.describeError(error),
  });

  /** `title` names the action for the dialog; `askFirst` asks for the PIN before the first send. */
  function run<T extends Transaction | void>(
    title: string,
    send: (approverPin?: string) => Promise<T>,
    onLanded?: (cart: T) => void,
    describeError: (error: unknown) => string = userMessage,
    askFirst?: string,
  ): Promise<void> {
    call.current = { send, onLanded: onLanded as Call['onLanded'], describeError };
    return action.run(title, askFirst);
  }

  return { run, dialogProps: action.dialogProps };
}
