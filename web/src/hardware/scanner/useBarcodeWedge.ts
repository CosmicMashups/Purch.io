import { useEffect, useRef } from 'react';
import { WedgeDetector, isEditableTarget } from './wedge';

/**
 * Listens for a keyboard-mode scanner anywhere on the page, so the cashier does not have to click the
 * search box first. Keys typed into a field are left alone; that field handles its own Enter.
 */
export function useBarcodeWedge(onScan: (code: string) => void, enabled: boolean): void {
  const handler = useRef(onScan);
  useEffect(() => {
    handler.current = onScan;
  }, [onScan]);

  useEffect(() => {
    if (!enabled) return;
    const detector = new WedgeDetector();
    const onKey = (event: KeyboardEvent) => {
      if (event.ctrlKey || event.metaKey || event.altKey || event.repeat || isEditableTarget(event.target)) return;
      const code = detector.push(event.key, event.timeStamp);
      if (code) {
        event.preventDefault();
        handler.current(code);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [enabled]);
}
