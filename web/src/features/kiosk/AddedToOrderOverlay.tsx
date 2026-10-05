import { gsap } from 'gsap';
import { useLayoutEffect, useRef } from 'react';

interface AddedToOrderOverlayProps {
  title: string;
  subtitle?: string;
  /** Called once, when the animation has played out (or, under reduced motion, after a short pause). */
  onDone: () => void;
}

const RING_RADIUS = 52;
const RING_LENGTH = 2 * Math.PI * RING_RADIUS;

/**
 * The confirmation after an item is added. A ring draws itself, fills with the business's colour, and a checkmark is
 * drawn inside it while a soft pulse leaves the ring; then the message settles in. Every move is a state change the
 * customer needs to read ("it worked, you are being taken back to the menu"), and nothing loops. With reduced motion
 * the finished mark is shown at once and the screen simply waits a moment.
 */
export function AddedToOrderOverlay({ title, subtitle, onDone }: AddedToOrderOverlayProps) {
  const root = useRef<HTMLDivElement>(null);
  const done = useRef(onDone);
  // The animation runs once; this keeps its finish pointing at the latest callback without restarting it.
  useLayoutEffect(() => {
    done.current = onDone;
  });

  useLayoutEffect(() => {
    const scope = root.current;
    if (!scope) return;
    const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
    const check = scope.querySelector<SVGPathElement>('[data-check]');
    const checkLength = check?.getTotalLength?.() ?? 60;

    if (reduce) {
      const timer = window.setTimeout(() => done.current(), 1100);
      return () => window.clearTimeout(timer);
    }

    const ctx = gsap.context(() => {
      gsap.set('[data-ring]', { strokeDasharray: RING_LENGTH, strokeDashoffset: RING_LENGTH, rotation: -90, transformOrigin: '50% 50%' });
      gsap.set('[data-disc]', { scale: 0.4, opacity: 0, transformOrigin: '50% 50%' });
      gsap.set('[data-check]', { strokeDasharray: checkLength, strokeDashoffset: checkLength });
      gsap.set('[data-pulse]', { scale: 1, opacity: 0, transformOrigin: '50% 50%' });
      gsap.set('[data-text]', { y: 16, opacity: 0 });

      gsap
        .timeline({ defaults: { ease: 'power3.out' }, onComplete: () => done.current() })
        .fromTo(scope, { opacity: 0 }, { opacity: 1, duration: 0.18, ease: 'none' })
        .to('[data-ring]', { strokeDashoffset: 0, duration: 0.55 }, 0.05)
        .to('[data-disc]', { scale: 1, opacity: 1, duration: 0.45, ease: 'back.out(1.7)' }, 0.38)
        .to('[data-check]', { strokeDashoffset: 0, duration: 0.4, ease: 'power2.inOut' }, 0.62)
        .fromTo('[data-pulse]', { scale: 1, opacity: 0.45 }, { scale: 1.55, opacity: 0, duration: 0.7, ease: 'power2.out' }, 0.9)
        .to('[data-text]', { y: 0, opacity: 1, duration: 0.42, stagger: 0.08 }, 0.95)
        .to({}, { duration: 0.55 })
        .to(scope, { opacity: 0, duration: 0.22, ease: 'power1.in' });
    }, scope);

    return () => ctx.revert();
  }, []);

  return (
    <div ref={root} role="status" aria-live="polite" className="fixed inset-0 z-50 grid place-items-center bg-surface/95 px-8 text-center backdrop-blur-sm">
      <div className="flex flex-col items-center gap-6">
        <svg viewBox="0 0 120 120" className="size-48 landscape:size-40" aria-hidden="true">
          <circle data-pulse cx="60" cy="60" r={RING_RADIUS} fill="none" stroke="var(--brand)" strokeWidth="3" opacity="0" />
          <circle data-disc cx="60" cy="60" r={RING_RADIUS - 3} fill="var(--brand)" />
          <circle data-ring cx="60" cy="60" r={RING_RADIUS} fill="none" stroke="var(--brand-strong)" strokeWidth="5" strokeLinecap="round" />
          <path data-check d="M38 62 L54 77 L83 44" fill="none" stroke="var(--on-brand)" strokeWidth="9" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        <div className="flex flex-col gap-2">
          <p data-text className="text-5xl font-extrabold tracking-tight landscape:text-4xl">
            {title}
          </p>
          {subtitle && (
            <p data-text className="text-2xl text-ink-soft">
              {subtitle}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
