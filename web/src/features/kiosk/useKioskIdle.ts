import { useCallback, useEffect, useRef, useState } from 'react';
import { useLocalKioskCartStore } from './localCart';

/** Untouched for this long and the kiosk asks whether the customer is still there. */
export const IDLE_WARN_MS = 45_000;
/** Untouched for this long and the order is cleared for the next customer (the warning gets the last 15 seconds). */
export const IDLE_CLEAR_MS = 60_000;

export type IdleStage = 'active' | 'warn';

/** Where an idle stretch stands: nothing yet, asking, or time to clear. An empty cart has nothing to ask about. */
export function idleStage(idleMs: number, hasLines: boolean): IdleStage | 'expired' {
  if (!hasLines) return idleMs >= IDLE_WARN_MS ? 'expired' : 'active';
  if (idleMs >= IDLE_CLEAR_MS) return 'expired';
  return idleMs >= IDLE_WARN_MS ? 'warn' : 'active';
}

/**
 * Watches for a customer who walked away. Any touch or key press counts as being there, except while the "Need more
 * time?" question is up: then only answering it does, so a stray tap behind the dialog cannot keep a dead order alive.
 */
export function useKioskIdle({ enabled, hasLines, onExpire }: { enabled: boolean; hasLines: boolean; onExpire: () => void }) {
  const [stage, setStage] = useState<IdleStage>('active');
  const [secondsLeft, setSecondsLeft] = useState(Math.round((IDLE_CLEAR_MS - IDLE_WARN_MS) / 1000));
  const stageRef = useRef<IdleStage>('active');
  const expire = useRef(onExpire);
  useEffect(() => {
    expire.current = onExpire;
  });

  const keepGoing = useCallback(() => {
    useLocalKioskCartStore.setState({ lastActivityAt: Date.now() });
    stageRef.current = 'active';
    setStage('active');
  }, []);

  useEffect(() => {
    if (!enabled) return;
    const touch = () => {
      if (stageRef.current === 'active') useLocalKioskCartStore.setState({ lastActivityAt: Date.now() });
    };
    window.addEventListener('pointerdown', touch);
    window.addEventListener('keydown', touch);
    return () => {
      window.removeEventListener('pointerdown', touch);
      window.removeEventListener('keydown', touch);
    };
  }, [enabled]);

  useEffect(() => {
    if (!enabled) return;
    // Arriving on a screen counts as being there, so a stale timestamp never expires a customer who just got here.
    useLocalKioskCartStore.setState({ lastActivityAt: Date.now() });
    const id = window.setInterval(() => {
      const idleMs = Date.now() - useLocalKioskCartStore.getState().lastActivityAt;
      const next = idleStage(idleMs, hasLines);
      if (next === 'expired') {
        stageRef.current = 'active';
        setStage('active');
        expire.current();
        return;
      }
      stageRef.current = next;
      setStage(next);
      if (next === 'warn') setSecondsLeft(Math.max(0, Math.ceil((IDLE_CLEAR_MS - idleMs) / 1000)));
    }, 1000);
    return () => {
      window.clearInterval(id);
      stageRef.current = 'active';
    };
  }, [enabled, hasLines]);

  // A screen that never times out never shows the question, whatever the last screen left behind.
  return { stage: enabled ? stage : ('active' as IdleStage), secondsLeft, keepGoing };
}
