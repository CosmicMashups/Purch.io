import { useEffect } from 'react';
import { publishCustomerDisplay, type CustomerDisplayState } from './channel';

/** Keeps the customer display in step with whatever this screen is showing. */
export function usePublishCustomerDisplay(state: CustomerDisplayState): void {
  const key = JSON.stringify(state);
  useEffect(() => {
    publishCustomerDisplay(JSON.parse(key) as CustomerDisplayState);
  }, [key]);
}
