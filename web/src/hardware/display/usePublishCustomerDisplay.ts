import { useEffect } from 'react';
import { useSession } from '../../features/auth/useSession';
import { publishCustomerDisplay, type CustomerDisplayState } from './channel';
import { publishToCustomerDisplay } from './serverFeed';

/** Waits for a burst of cart changes to settle before telling the server, so tapping five items sends one update. */
const SERVER_DEBOUNCE_MS = 250;

/**
 * Keeps the customer display in step with whatever this screen is showing: a second window on this browser straight away, and,
 * on a Register someone has unlocked, the customer display paired to it through the server.
 */
export function usePublishCustomerDisplay(state: CustomerDisplayState): void {
  const { claims } = useSession();
  const onRegister = !!claims?.deviceId;
  const key = JSON.stringify(state);

  useEffect(() => {
    publishCustomerDisplay(JSON.parse(key) as CustomerDisplayState);
  }, [key]);

  useEffect(() => {
    if (!onRegister) return;
    const timer = window.setTimeout(() => publishToCustomerDisplay(JSON.parse(key) as CustomerDisplayState), SERVER_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [key, onRegister]);
}
