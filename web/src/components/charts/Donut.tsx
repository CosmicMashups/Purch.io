import { useState } from 'react';
import { ChartTip, Legend } from './ChartParts';
import { useChartTip } from './chartHooks';
import { arcPath, shareLabel, sliceAngles } from './scale';

export interface DonutSlice {
  key: string;
  label: string;
  value: number;
  color: string;
}

interface DonutProps {
  slices: DonutSlice[];
  formatValue: (value: number) => string;
  totalLabel: string;
  totalValue: string;
  ariaLabel: string;
}

const SIZE = 184;
const OUTER = 88;
const INNER = 58;
/** Slices are separated by a 2px gap of surface, worked out as an angle at the middle of the ring. */
const GAP_PX = 2;

/** Part of a whole, for a handful of parts. The legend carries every value, so colour is never the only channel. */
export function Donut({ slices, formatValue, totalLabel, totalValue, ariaLabel }: DonutProps) {
  const { tip, show, hide } = useChartTip();
  const [active, setActive] = useState<string | null>(null);
  const total = slices.reduce((sum, s) => sum + s.value, 0);
  const angles = sliceAngles(slices.map((s) => s.value));
  const gap = slices.filter((s) => s.value > 0).length > 1 ? GAP_PX / ((OUTER + INNER) / 2) : 0;

  return (
    <figure className="flex flex-wrap items-center gap-x-8 gap-y-4">
      <div className="relative shrink-0" style={{ width: SIZE, height: SIZE }}>
        <svg width={SIZE} height={SIZE} role="img" aria-label={ariaLabel}>
          {slices.map((slice, i) => {
            const { start, end } = angles[i];
            if (slice.value <= 0) return null;
            const lifted = active === slice.key;
            const d = arcPath(SIZE / 2, SIZE / 2, lifted ? OUTER + 3 : OUTER, INNER, start + gap / 2, end - gap / 2);
            const mid = (start + end) / 2;
            return (
              <path
                key={slice.key}
                d={d}
                fill={slice.color}
                onPointerEnter={() => {
                  setActive(slice.key);
                  show(SIZE / 2 + Math.cos(mid) * OUTER, SIZE / 2 + Math.sin(mid) * OUTER, {
                    title: slice.label,
                    rows: [{ label: shareLabel(slice.value, total), value: formatValue(slice.value), color: slice.color }],
                  });
                }}
                onPointerLeave={() => {
                  setActive(null);
                  hide();
                }}
              />
            );
          })}
        </svg>
        <div className="pointer-events-none absolute inset-0 grid place-items-center text-center">
          <div>
            <p className="text-lg font-bold leading-tight">{totalValue}</p>
            <p className="text-xs text-ink-soft">{totalLabel}</p>
          </div>
        </div>
        <ChartTip tip={tip} width={SIZE} />
      </div>
      <Legend
        className="min-w-56 flex-1"
        items={slices.map((s) => ({ key: s.key, label: s.label, color: s.color, detail: `${formatValue(s.value)}, ${shareLabel(s.value, total)}` }))}
      />
    </figure>
  );
}
