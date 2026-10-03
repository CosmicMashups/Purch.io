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

    async function tick() {
      try {
        const feed = await customerDisplayFeed.poll(version.current);
        if (feed && !stopped) {
          version.current = feed.version;
          setState(feed.state ?? IDLE_STATE);
        }
      } catch {
        // Keep showing the last order and try again on the next tick.
      }
      if (!stopped) timer = window.setTimeout(() => void tick(), POLL_MS);
    }

    void tick();
    return () => {
      stopped = true;
      window.clearTimeout(timer);
    };
  }, [enabled]);

  return state;
}
