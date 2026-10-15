import { useState } from 'react';
import { StockTakeLineState, type StockTakeLine } from '../types';

interface Props {
  lines: StockTakeLine[];
  /** True while a count is being sent: ignores re-entry. */
  busy: boolean;
  onCount: (lineId: string, counted: number | null, skip: boolean) => void;
}

/** Large-target counting list: one numeric entry per line. Expected is shown only when the API supplies it (never on a blind take). */
export function StockTakeCountList({ lines, busy, onCount }: Props) {
  return (
    <ul className="flex flex-col gap-3" aria-label="Lines to count">
      {lines.map((line) => (
        <CountRow key={`${line.id}-${line.countedQty}`} line={line} busy={busy} onCount={onCount} />
      ))}
    </ul>
  );
}

function CountRow({ line, busy, onCount }: { line: StockTakeLine; busy: boolean; onCount: Props['onCount'] }) {
  const [draft, setDraft] = useState(line.countedQty !== null ? String(line.countedQty) : '');
  const parsed = draft.trim() === '' ? null : Number(draft);
  const valid = parsed !== null && Number.isFinite(parsed) && parsed >= 0;
  const stateLabel = line.state === StockTakeLineState.Counted ? 'Counted' : line.state === StockTakeLineState.Skipped ? 'Skipped' : 'Not counted';

  return (
    <li className={`flex flex-col gap-3 rounded-panel border bg-surface p-4 sm:flex-row sm:items-center ${line.needsRecount ? 'border-2 border-danger' : 'border-line'}`}>
      <div className="min-w-0 flex-1">
        <p className="text-base font-semibold text-ink">{line.name}</p>
        <p className="text-sm text-ink-soft">
          {stateLabel}
          {line.expectedQty !== null && ` · Expected ${line.expectedQty}`}
        </p>
        {line.needsRecount && (
          <p className="text-sm font-medium text-danger">Stock moved by {line.movementsSinceCount ?? 0} since this was counted. Count it again.</p>
        )}
      </div>
      <input
        type="number"
        inputMode="decimal"
        step="any"
        min={0}
        aria-label={`Counted quantity for ${line.name}`}
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        className="h-12 w-full rounded-control border border-ink-soft/40 bg-surface px-3 text-lg tabular-nums text-ink sm:w-32"
      />
      <div className="flex gap-2">
        <button
          type="button"
          disabled={busy || !valid}
          onClick={() => onCount(line.id, parsed, false)}
          className="h-12 min-w-24 rounded-control bg-brand px-4 text-base font-semibold text-on-brand hover:bg-brand-strong disabled:opacity-60"
        >
          Save count
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={() => onCount(line.id, null, true)}
          className="h-12 rounded-control border border-line bg-surface px-4 text-base font-semibold text-ink hover:border-brand disabled:opacity-60"
        >
          Skip
        </button>
        {line.state === StockTakeLineState.Counted && (
          <button
            type="button"
            disabled={busy}
            onClick={() => onCount(line.id, null, false)}
            className="h-12 rounded-control border border-line bg-surface px-4 text-base font-semibold text-ink hover:border-brand disabled:opacity-60"
          >
            Clear
          </button>
        )}
      </div>
    </li>
  );
}
