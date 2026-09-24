import { useEffect, useRef, useState, type RefObject } from 'react';

/** Width of an element in pixels, kept current as it resizes. Charts draw at real size so their text stays crisp. */
export function useElementWidth<T extends HTMLElement>(fallback = 640): [RefObject<T | null>, number] {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(fallback);

  useEffect(() => {
    const element = ref.current;
    if (!element || typeof ResizeObserver === 'undefined') return;
    const measure = () => setWidth(Math.max(0, Math.round(element.getBoundingClientRect().width)) || fallback);
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [fallback]);

  return [ref, width];
}

export interface TipRow {
  label: string;
  value: string;
  /** A short line of the series colour keys the row to its mark. */
  color?: string;
}

export interface TipContent {
  title: string;
  rows: TipRow[];
}

export interface TipState extends TipContent {
  x: number;
  y: number;
}

/** Hover or focus readout state for one chart. Discrete, so React state is the right tool. */
export function useChartTip() {
  const [tip, setTip] = useState<TipState | null>(null);
  return {
    tip,
    show: (x: number, y: number, content: TipContent) => setTip({ x, y, ...content }),
    hide: () => setTip(null),
  };
}
