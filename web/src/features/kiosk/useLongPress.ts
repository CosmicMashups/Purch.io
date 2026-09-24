import { useCallback, useEffect, useRef } from 'react';

/**
 * A hold gesture for a control customers should not stumble on, such as resetting a public kiosk.
 * Spread the returned handlers onto the element.
 */
export function useLongPress(onLongPress: () => void, ms = 3000) {
  const timer = useRef<number | null>(null);
  const cancel = useCallback(() => {
    if (timer.current !== null) window.clearTimeout(timer.current);
    timer.current = null;
  }, []);
  useEffect(() => cancel, [cancel]);
  const start = useCallback(() => {
    cancel();
    timer.current = window.setTimeout(onLongPress, ms);
  }, [cancel, onLongPress, ms]);
  return { onPointerDown: start, onPointerUp: cancel, onPointerLeave: cancel, onPointerCancel: cancel };
}
