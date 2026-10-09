import { useEffect, useRef, useState } from 'react';
import { apiClient } from '../../lib/apiClient';
import { IDLE_STATE, type CustomerDisplayState } from './channel';

interface Feed {
  version: number;
  updatedAt: string | null;
  state: CustomerDisplayState | null;
}

/**
 * The customer display on its own screen: the Register pushes what to show, and the display asks for it. The server holds
 * the latest state per Register (it keeps nothing in memory between requests), and a poll that finds nothing new is a 304.
 */
export const customerDisplayFeed = {
  publish: (state: CustomerDisplayState) => apiClient.put('/customer-display/state', state).then(() => undefined),

  /** Null means nothing newer than the version the display already has. */
  poll: (knownVersion: number | null) =>
    apiClient
      .get<Feed>('/customer-display/state', {
        headers: knownVersion === null ? undefined : { 'If-None-Match': `"v${knownVersion}"` },
        validateStatus: (status) => status === 200 || status === 304,
      })
      .then((response) => (response.status === 304 ? null : response.data)),
};

/** Best effort: a till must never stall because its customer screen could not be told something. */
export function publishToCustomerDisplay(state: CustomerDisplayState): void {
  void customerDisplayFeed.publish(state).catch(() => undefined);
}

export const POLL_MS = 1000;
/** Poll interval once nothing has changed for a while (or the page is hidden); a change drops back to POLL_MS. */
export const IDLE_POLL_MS = 3000;
const QUIET_POLLS_BEFORE_SLOWING = 30;

/**
 * What a paired customer display shows. Polls once a second while the page is open, keeps the last state through a dropped
 * connection (so a hiccup never blanks the customer's order), and starts on the welcome screen until the Register says more.
 */
export function useServerCustomerDisplay(enabled: boolean): CustomerDisplayState {
  const [state, setState] = useState<CustomerDisplayState>(IDLE_STATE);
  const version = useRef<number | null>(null);

  useEffect(() => {
    if (!enabled) return;
    let stopped = false;
    let timer: number | undefined;

    let quietPolls = 0;

    async function tick() {
      // A hidden tab is not being looked at, so it skips the request and just checks again later.
      if (!document.hidden) {
        try {
          const feed = await customerDisplayFeed.poll(version.current);
          if (feed && !stopped) {
            version.current = feed.version;
            quietPolls = 0;
            setState(feed.state ?? IDLE_STATE);
          } else {
            quietPolls += 1;
          }
        } catch {
          // Keep showing the last order and try again on the next tick.
        }
      }
      // Fast while an order is in progress, then slower once the till has been quiet for a while.
      const delay = document.hidden ? IDLE_POLL_MS : quietPolls >= QUIET_POLLS_BEFORE_SLOWING ? IDLE_POLL_MS : POLL_MS;
      if (!stopped) timer = window.setTimeout(() => void tick(), delay);
    }

    void tick();
    return () => {
      stopped = true;
      window.clearTimeout(timer);
    };
  }, [enabled]);

  return state;
}
