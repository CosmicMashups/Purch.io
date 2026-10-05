import { useCallback, useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { useHardwareConfig } from '../../hardware/config';
import { KioskSlip } from './KioskSlip';
import type { SlipData } from './slipData';

/**
 * Prints a slip through the browser: it puts the slip on the page, marks the page as "printing a slip" so that nothing
 * else is printed, and asks for the print. Where Chrome runs in kiosk printing mode this goes straight to the printer.
 */
export function useSlipPrinter() {
  const paperWidth = useHardwareConfig((s) => s.paperWidth);
  const [slip, setSlip] = useState<SlipData | null>(null);

  useEffect(() => {
    if (!slip) return;
    const root = document.documentElement;
    const finish = () => {
      root.classList.remove('printing-kiosk-slip');
      setSlip(null);
    };
    root.classList.add('printing-kiosk-slip');
    window.addEventListener('afterprint', finish, { once: true });
    // One frame so the slip is laid out before the print preview is taken.
    const frame = window.requestAnimationFrame(() => {
      try {
        window.print();
      } catch {
        finish();
      }
    });
    return () => {
      window.cancelAnimationFrame(frame);
      window.removeEventListener('afterprint', finish);
      root.classList.remove('printing-kiosk-slip');
    };
  }, [slip]);

  /** Prints this slip. Calling it again prints it again. */
  const print = useCallback((next: SlipData) => setSlip({ ...next }), []);

  return { print, portal: slip ? createPortal(<KioskSlip slip={slip} paperWidth={paperWidth} />, document.body) : null };
}
