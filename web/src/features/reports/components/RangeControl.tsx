import { useId, useState } from 'react';
import { CalendarBlank, CaretDown } from '@phosphor-icons/react';
import { FormField, controlClass } from '../../../components/forms/FormField';
import type { Branch } from '../../branches/types';
import { formatDay } from '../../dashboard/format';
import type { RangeChoice } from '../params';
import { PRESET_LABELS, presetRange, rangeProblem, type RangePreset } from '../range';

interface RangeControlProps {
  value: RangeChoice;
  onChange: (next: RangeChoice) => void;
  branches: Branch[];
}

/** A small segmented picker: ink selection and a hairline border, so it never reads as a report button. */
const segment = (active: boolean) =>
  `h-12 px-4 text-base font-semibold ${active ? 'bg-ink text-canvas' : 'bg-surface hover:bg-canvas'}`;

function summary(value: RangeChoice): { label: string; dates: string } {
  const days = value.preset === 'custom' ? value.custom : presetRange(value.preset);
  const label = value.preset === 'custom' ? 'Custom range' : PRESET_LABELS[value.preset];
  const dates = days.fromDay === days.toDay ? formatDay(days.fromDay) : `${formatDay(days.fromDay)} to ${formatDay(days.toDay)}`;
  return { label, dates };
}

/**
 * The reporting period (and, for accounts that see several, the branch) as one quiet chip that opens its choices.
 * The report buttons above it are the only brand-filled controls, so "which report" and "which dates" stay distinct.
 * The API applies the account's own limits.
 */
export function RangeControl({ value, onChange, branches }: RangeControlProps) {
  const [open, setOpen] = useState(false);
  const panelId = useId();
  const problem = value.preset === 'custom' ? rangeProblem(value.custom) : null;
  const { label, dates } = summary(value);
  const branchName = value.branchId ? branches.find((b) => b.id === value.branchId)?.name : branches.length > 1 ? 'All branches' : undefined;

  return (
    <div className="flex flex-col gap-3">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((o) => !o)}
        className="flex min-h-14 w-full items-center gap-3 rounded-control border border-line bg-surface px-4 text-left hover:border-ink sm:w-auto sm:min-w-80"
      >
        <CalendarBlank size={24} aria-hidden="true" className="shrink-0 text-ink-soft" />
        <span className="flex flex-col">
          <span className="text-sm text-ink-soft">Period</span>
          <span className="text-base font-semibold">
            {label}
            <span className="font-normal text-ink-soft"> from {dates}</span>
          </span>
          {branchName && <span className="text-sm text-ink-soft">{branchName}</span>}
        </span>
        <CaretDown size={20} aria-hidden="true" className={`ml-auto shrink-0 text-ink-soft transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div id={panelId} className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-4">
          <div role="group" aria-label="Date range" className="inline-flex w-fit max-w-full flex-wrap overflow-hidden rounded-control border border-line">
            {(Object.keys(PRESET_LABELS) as RangePreset[]).map((preset) => (
              <button key={preset} type="button" aria-pressed={value.preset === preset} onClick={() => onChange({ ...value, preset })} className={segment(value.preset === preset)}>
                {PRESET_LABELS[preset]}
              </button>
            ))}
            <button type="button" aria-pressed={value.preset === 'custom'} onClick={() => onChange({ ...value, preset: 'custom' })} className={segment(value.preset === 'custom')}>
              Custom
            </button>
          </div>

          {value.preset === 'custom' && (
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField label="From">
                <input type="date" value={value.custom.fromDay} onChange={(e) => onChange({ ...value, custom: { ...value.custom, fromDay: e.target.value } })} className={controlClass} />
              </FormField>
              <FormField label="To" error={problem ?? undefined}>
                <input type="date" value={value.custom.toDay} onChange={(e) => onChange({ ...value, custom: { ...value.custom, toDay: e.target.value } })} className={controlClass} />
              </FormField>
            </div>
          )}

          {branches.length > 1 && (
            <FormField label="Branch">
              <select value={value.branchId} onChange={(e) => onChange({ ...value, branchId: e.target.value })} className={controlClass}>
                <option value="">All branches</option>
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))}
              </select>
            </FormField>
          )}
        </div>
      )}
    </div>
  );
}
