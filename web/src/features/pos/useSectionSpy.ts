import { useCallback, useEffect, useRef, useState } from 'react';

/** How long a tap on the rail owns the highlight, so the smooth scroll it starts cannot flicker through the tiles it passes. */
const CLICK_LOCK_MS = 900;

interface SectionSpy {
  /** The section currently at the top of the list, which the rail highlights. */
  activeId: string | null;
  /** Attach to each section's element so the spy can watch it. */
  sectionRef: (id: string) => (element: HTMLElement | null) => void;
  /** Scrolls the list to a section and highlights it straight away. */
  scrollTo: (id: string) => void;
}

/**
 * Keeps a category rail in step with a continuously scrolling menu. An IntersectionObserver watches a thin band at
 * the top of the scroll area (no scroll listeners, nothing re-renders per frame) and the last section whose top has
 * entered that band is the current one. Pass the scroll container as `root`, or null when the page itself scrolls.
 */
export function useSectionSpy(sectionIds: readonly string[], root: HTMLElement | null): SectionSpy {
  const [spied, setSpied] = useState<string | null>(null);
  const [picked, setPicked] = useState<string | null>(null);
  const elements = useRef(new Map<string, HTMLElement>());
  const lockedUntil = useRef(0);
  const idsKey = sectionIds.join('|');

  const sectionRef = useCallback(
    (id: string) => (element: HTMLElement | null) => {
      if (element) elements.current.set(id, element);
      else elements.current.delete(id);
    },
    [],
  );

  useEffect(() => {
    if (typeof IntersectionObserver === 'undefined' || sectionIds.length === 0) return;
    const inBand = new Set<string>();

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          const id = (entry.target as HTMLElement).dataset.sectionId;
          if (!id) continue;
          if (entry.isIntersecting) inBand.add(id);
          else inBand.delete(id);
        }
        if (performance.now() < lockedUntil.current) return;
        const atTop = (root ? root.scrollTop : window.scrollY) <= 1;
        const current = atTop ? sectionIds[0] : [...sectionIds].reverse().find((id) => inBand.has(id));
        if (current) setSpied(current);
      },
      // A band across the top tenth of the area: a section is "current" once its top reaches it.
      { root, rootMargin: '0px 0px -88% 0px', threshold: 0 },
    );

    for (const id of sectionIds) {
      const element = elements.current.get(id);
      if (element) observer.observe(element);
    }
    return () => observer.disconnect();
    // idsKey stands in for sectionIds, which is a fresh array every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [idsKey, root]);

  const scrollTo = useCallback((id: string) => {
    lockedUntil.current = performance.now() + CLICK_LOCK_MS;
    setPicked(id);
    elements.current.get(id)?.scrollIntoView?.({ block: 'start', behavior: window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
  }, []);

  // A tap wins until the lock runs out, after which whatever the observer last saw takes over again.
  const activeId = picked !== null && performance.now() < lockedUntil.current ? picked : (spied ?? sectionIds[0] ?? null);
  return { activeId, sectionRef, scrollTo };
}
