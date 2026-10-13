/** Spreads a fixed interval so devices that start together (after an outage, a deploy or a shift change)
 * drift apart instead of hitting the API in lockstep. */
export function withJitter(ms: number, ratio = 0.15): number {
  return Math.max(1_000, Math.round(ms * (1 + (Math.random() * 2 - 1) * ratio)));
}

interface PollOptions {
  /** Interval while there is something to watch. */
  activeMs: number;
  /** Interval once the list has been empty for `quietAfterMs`. */
  idleMs: number;
  quietAfterMs: number;
}

/**
 * A refetchInterval for a "what is pending?" list. Polls fast while there are rows, then backs off once the
 * list has been empty for a while; the first non-empty answer snaps it back. Background tabs are already
 * paused by react-query (refetchIntervalInBackground defaults to false).
 */
export function adaptiveListInterval({ activeMs, idleMs, quietAfterMs }: PollOptions) {
  const lastBusyAt = new WeakMap<object, number>();
  return (query: { state: { data: unknown } } & object): number => {
    const now = Date.now();
    const data = query.state.data;
    const busy = !Array.isArray(data) || data.length > 0;
    if (busy || !lastBusyAt.has(query)) lastBusyAt.set(query, now);
    const quietFor = now - (lastBusyAt.get(query) ?? now);
    return withJitter(quietFor >= quietAfterMs ? idleMs : activeMs);
  };
}
