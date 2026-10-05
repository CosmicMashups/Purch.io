import { useEffect, useState } from 'react';
import { useSelectableBranches } from '../features/branches/queries';

/** The branch a quick stock change is recorded against: the only one available, or the one chosen in the picker. */
export function useStockBranch() {
  const { branches } = useSelectableBranches();
  const [chosen, setChosen] = useState<string | null>(null);
  const list = branches ?? [];
  const branchId = chosen && list.some((b) => b.id === chosen) ? chosen : (list[0]?.id ?? null);
  return { branches: list, branchId, setBranchId: setChosen };
}

/** Shown only when the account can record stock at more than one branch. */
export function StockBranchPicker({ branches, branchId, onChange }: { branches: { id: string; name: string }[]; branchId: string | null; onChange: (id: string) => void }) {
  if (branches.length < 2) return null;
  return (
    <label className="flex items-center gap-2 text-xs text-gray-600">
      Stock changes are recorded at
      <select value={branchId ?? ''} onChange={(e) => onChange(e.target.value)} className="rounded-md border border-gray-300 px-2 py-1 text-xs">
        {branches.map((b) => (
          <option key={b.id} value={b.id}>
            {b.name}
          </option>
        ))}
      </select>
    </label>
  );
}

const button =
  'grid size-8 shrink-0 place-items-center rounded-md border border-gray-300 bg-white text-base font-semibold text-gray-700 hover:border-gray-500 disabled:cursor-not-allowed disabled:opacity-40';

/**
 * A count with "-" and "+" beside it. The number can also be typed over; Enter or leaving the field saves it.
 * `onSet` receives the new count (never below zero) and is expected to save it; the shown value follows `value`.
 */
export function StockStepper({ value, unit, label, disabled = false, onSet }: { value: number; unit?: string; label: string; disabled?: boolean; onSet: (next: number) => void | Promise<unknown> }) {
  const [draft, setDraft] = useState(String(value));
  const [editing, setEditing] = useState(false);

  useEffect(() => {
    if (!editing) setDraft(String(value));
  }, [value, editing]);

  function commit() {
    setEditing(false);
    const next = Number(draft);
    if (draft.trim() === '' || !Number.isFinite(next) || next < 0) {
      setDraft(String(value));
      return;
    }
    if (next !== value) void onSet(next);
  }

  return (
    <div className="flex items-center gap-1" role="group" aria-label={`Stock for ${label}`}>
      <button type="button" className={button} disabled={disabled || value <= 0} aria-label={`Decrease stock of ${label}`} onClick={() => void onSet(Math.max(0, value - 1))}>
        −
      </button>
      <input
        type="number"
        inputMode="decimal"
        min={0}
        step="any"
        value={draft}
        disabled={disabled}
        aria-label={`Stock count of ${label}`}
        onFocus={() => setEditing(true)}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === 'Enter') e.currentTarget.blur();
          if (e.key === 'Escape') {
            setDraft(String(value));
            e.currentTarget.blur();
          }
        }}
        className="h-8 w-20 rounded-md border border-gray-300 px-2 text-center text-sm tabular-nums focus:border-gray-500 focus:outline-none disabled:opacity-60"
      />
      <button type="button" className={button} disabled={disabled} aria-label={`Increase stock of ${label}`} onClick={() => void onSet(value + 1)}>
        +
      </button>
      {unit && <span className="text-xs text-gray-500">{unit}</span>}
    </div>
  );
}
