import { useCallback, useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Modal } from '../../components/Modal';
import { KioskHeader } from './KioskHeader';
import { primaryButton, secondaryButton } from './KioskActionBar';
import { useKioskStore } from './kioskStore';
import { menuScroll } from './menuScroll';
import { useLocalKioskCartStore } from './localCart';
import { useKioskIdle } from './useKioskIdle';

/** The screens that never time out: the welcome screen waits for the next customer, and the other two are mid-send or hand-off. */
const NO_IDLE_PATHS = new Set(['/kiosk', '/kiosk/processing', '/kiosk/done', '/kiosk/printer']);

/**
 * The kiosk is customer-facing and fills the screen in either orientation: no staff navigation, large targets, the
 * business's logo and name at the top of every screen except the welcome one, and a content area that scrolls on its own.
 */
export function KioskLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const hasLines = useLocalKioskCartStore((s) => s.lines.length > 0);
  const clearCart = useLocalKioskCartStore((s) => s.clear);
  const resetCheckout = useKioskStore((s) => s.resetCheckout);
  const onLanding = location.pathname === '/kiosk';

  const startFresh = useCallback(() => {
    clearCart();
    resetCheckout();
    menuScroll.top = 0;
  }, [clearCart, resetCheckout]);

  const { stage, secondsLeft, keepGoing } = useKioskIdle({
    enabled: !NO_IDLE_PATHS.has(location.pathname),
    hasLines,
    onExpire: () => {
      startFresh();
      navigate('/kiosk', { replace: true });
    },
  });

  // Landing back on the start screen means a fresh customer: nothing left over to reset.
  useEffect(() => {
    if (onLanding) startFresh();
  }, [onLanding, startFresh]);

  return (
    <div className="flex h-dvh flex-col bg-canvas text-ink">
      {!onLanding && <KioskHeader />}
      <div className="min-h-0 flex-1">
        <Outlet />
      </div>

      <Modal
        open={stage === 'warn'}
        title="Need more time?"
        onClose={keepGoing}
        footer={
          <div className="flex gap-3">
            <button
              type="button"
              className={`${secondaryButton} flex-1`}
              onClick={() => {
                startFresh();
                navigate('/kiosk', { replace: true });
              }}
            >
              Start over
            </button>
            <button type="button" className={`${primaryButton} flex-1`} onClick={keepGoing}>
              Yes, continue
            </button>
          </div>
        }
      >
        <p role="timer" aria-live="polite" className="text-xl">
          {hasLines ? 'Your order will be cleared in ' : 'Returning to the start in '}
          <strong className="tabular-nums">{secondsLeft}s</strong>.
        </p>
      </Modal>
    </div>
  );
}
