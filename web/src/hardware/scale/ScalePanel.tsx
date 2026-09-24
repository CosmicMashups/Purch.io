import { useEffect, useState } from 'react';
import { usableKilograms, scaleState, type ScaleState } from './parser';
import { useScale } from './scaleStore';

const STATE_LABEL: Record<ScaleState, string> = {
  stable: 'Stable',
  settling: 'Settling',
  overload: 'Overload',
  quiet: 'No reading',
};

const STATE_STYLE: Record<ScaleState, string> = {
  stable: 'bg-emerald-100 text-emerald-900',
  settling: 'bg-amber-100 text-amber-900',
  overload: 'bg-red-100 text-red-900',
  quiet: 'bg-slate-100 text-slate-700',
};

/** Re-renders every half second so a scale that goes quiet is noticed even when no new reading arrives. */
function useNow(intervalMs = 500): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), intervalMs);
    return () => window.clearInterval(id);
  }, [intervalMs]);
  return now;
}

const button = 'h-12 rounded-control border border-line bg-surface px-4 text-base font-semibold hover:border-brand disabled:opacity-50';

interface ScalePanelProps {
  /** Called with the settled weight in kilograms when the person accepts it. */
  onUse?: (kilograms: number) => void;
}

export function ScalePanel({ onUse }: ScalePanelProps) {
  const reading = useScale((s) => s.reading);
  const zero = useScale((s) => s.zero);
  const tare = useScale((s) => s.tare);
  const now = useNow();
  const state = scaleState(reading, now);
  const kilograms = usableKilograms(reading, now);

  return (
    <div className="flex flex-col gap-3 rounded-panel border border-line bg-surface p-4" aria-label="Scale">
      <div className="flex items-center justify-between gap-3">
        <p className="text-4xl font-bold tabular-nums" aria-live="off">
          {reading && state !== 'quiet' && !reading.isOverload ? `${reading.weight.toFixed(3)} ${reading.unit}` : '— —'}
        </p>
        <span role="status" className={`rounded-full px-3 py-1 text-sm font-bold ${STATE_STYLE[state]}`}>
          {STATE_LABEL[state]}
        </span>
      </div>
      <div className="flex flex-wrap gap-2">
        <button type="button" className={button} onClick={() => void zero()}>
          Zero
        </button>
        <button type="button" className={button} onClick={() => void tare()}>
          Tare
        </button>
        {onUse && (
          <button
            type="button"
            disabled={kilograms === null}
            onClick={() => kilograms !== null && onUse(kilograms)}
            className="h-12 rounded-control bg-brand px-4 text-base font-bold text-on-brand disabled:opacity-50"
          >
            {kilograms !== null ? `Use ${kilograms.toFixed(3)} kg` : 'Waiting for a stable weight'}
          </button>
        )}
      </div>
    </div>
  );
}
