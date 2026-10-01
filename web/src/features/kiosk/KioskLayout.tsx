import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useLocalKioskCartStore } from './localCart';

/** How long a cart with items sits untouched before the kiosk assumes the customer walked away and
 * resets for the next one (E6 design decision). */
export const IDLE_RESET_MS = 90_000;

/** The kiosk is portrait and customer-facing: one column, no staff navigation, large targets. */
export function KioskLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const hasLines = useLocalKioskCartStore((s) => s.lines.length > 0);
  const clearCart = useLocalKioskCartStore((s) => s.clear);

  useEffect(() => {
    if (!hasLines) return;
    const id = window.setInterval(() => {
      const idleFor = Date.now() - useLocalKioskCartStore.getState().lastActivityAt;
      if (idleFor >= IDLE_RESET_MS) {
        clearCart();
        navigate('/kiosk', { replace: true });
      }
    }, 5_000);
    return () => window.clearInterval(id);
  }, [hasLines, navigate, clearCart]);

  // Any interaction anywhere in the kiosk flow counts as activity, not just cart edits — a customer
  // browsing the menu for a minute is not "idle".
  useEffect(() => {
    const touch = () => {
      if (useLocalKioskCartStore.getState().lines.length > 0) useLocalKioskCartStore.setState({ lastActivityAt: Date.now() });
    };
    window.addEventListener('pointerdown', touch);
    window.addEventListener('keydown', touch);
    return () => {
      window.removeEventListener('pointerdown', touch);
      window.removeEventListener('keydown', touch);
    };
  }, []);

  // Landing back on the start screen means a fresh customer: nothing left over to reset.
  useEffect(() => {
    if (location.pathname === '/kiosk') clearCart();
  }, [location.pathname, clearCart]);

  return (
    <div className="min-h-dvh bg-canvas text-ink">
      <div className="mx-auto flex min-h-dvh max-w-xl flex-col">
        <Outlet />
      </div>
    </div>
  );
}
