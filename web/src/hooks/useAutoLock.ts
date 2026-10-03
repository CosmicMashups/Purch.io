import { useEffect, useRef } from 'react';

/** How long a till can sit untouched before it locks itself. */
export const AUTO_LOCK_MS = 5 * 60 * 1000;

const ACTIVITY = ['pointerdown', 'keydown', 'touchstart'] as const;

/**
 * Calls `onLock` once the screen has been left alone for `idleMs`. Taps and key presses (including a barcode scanner typing)
 * keep it awake. Scrolling alone does not, so nothing listens to scroll events.
 */
export function useAutoLock(enabled: boolean, onLock: () => void, idleMs = AUTO_LOCK_MS): void {
  const lock = useRef(onLock);
  lock.current = onLock;

  useEffect(() => {
    if (!enabled) return;
    let timer = window.setTimeout(() => lock.current(), idleMs);
    const wake = () => {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => lock.current(), idleMs);
    };
    for (const event of ACTIVITY) window.addEventListener(event, wake, { passive: true });
    return () => {
      window.clearTimeout(timer);
      for (const event of ACTIVITY) window.removeEventListener(event, wake);
    };
  }, [enabled, idleMs]);
}
