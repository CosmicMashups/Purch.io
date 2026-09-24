import { formatPeso } from '../../dashboard/format';
import { cashQuickAmounts, changePreview, tenderCoversTotal } from '../catalogView';

interface CashKeypadProps {
  total: number;
  /** The tender as typed, kept as text so "12." and "0.5" edit naturally. */
  value: string;
  onChange: (value: string) => void;
}

const KEYS = ['7', '8', '9', '4', '5', '6', '1', '2', '3', '.', '0', 'back'] as const;

const key = 'grid h-16 place-items-center rounded-control border border-line bg-surface text-2xl font-semibold hover:border-brand active:translate-y-px';

export function CashKeypad({ total, value, onChange }: CashKeypadProps) {
  const tendered = value === '' ? NaN : Number(value);
  const covers = tenderCoversTotal(tendered, total);

  function press(next: (typeof KEYS)[number]) {
    if (next === 'back') return onChange(value.slice(0, -1));
    if (next === '.' && value.includes('.')) return;
    // Two decimal places is the smallest peso amount a till handles.
    if (value.includes('.') && value.split('.')[1].length >= 2) return;
    onChange(value === '0' && next !== '.' ? next : value + next);
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <label htmlFor="tendered" className="mb-1 block text-base font-semibold">
          Cash received
        </label>
        <input
          id="tendered"
          inputMode="decimal"
          autoComplete="off"
          value={value}
          onChange={(e) => onChange(e.target.value.replace(/[^0-9.]/g, ''))}
          className="h-16 w-full rounded-control border border-ink-soft/40 bg-surface px-4 text-right text-3xl font-bold tabular-nums"
        />
      </div>

      <div role="group" aria-label="Quick amounts" className="flex flex-wrap gap-2">
        {cashQuickAmounts(total).map((quick) => (
          <button key={`${quick.label}-${quick.amount}`} type="button" onClick={() => onChange(String(quick.amount))} className="h-12 rounded-control border border-line px-4 text-base font-semibold hover:border-brand">
            {quick.label === 'Exact' || quick.label === 'Round up' ? `${quick.label} ${formatPeso(quick.amount)}` : formatPeso(quick.amount)}
          </button>
        ))}
      </div>

      <div className="grid grid-cols-3 gap-2">
        {KEYS.map((k) => (
          <button key={k} type="button" onClick={() => press(k)} aria-label={k === 'back' ? 'Delete last digit' : k} className={key}>
            {k === 'back' ? 'Del' : k}
          </button>
        ))}
      </div>

      <p aria-live="polite" className={`rounded-control px-4 py-3 text-lg font-bold ${covers ? 'bg-brand-tint text-brand-strong' : 'bg-line text-ink-soft'}`}>
        {covers ? `Change: ${formatPeso(changePreview(tendered, total))}` : value === '' ? 'Enter the amount received' : `Still needs ${formatPeso(total - (Number.isFinite(tendered) ? tendered : 0))}`}
      </p>
    </div>
  );
}
