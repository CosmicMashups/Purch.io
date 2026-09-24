import { FormField, controlClass } from '../../../components/forms/FormField';
import type { Branch } from '../../branches/types';
import type { RangeChoice } from '../params';
import { PRESET_LABELS, rangeProblem, type RangePreset } from '../range';

interface RangeControlProps {
  value: RangeChoice;
  onChange: (next: RangeChoice) => void;
  branches: Branch[];
}

const chip = (active: boolean) =>
  `h-12 rounded-control px-5 text-base font-semibold ${active ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`;

/** Picks the reporting window and, for accounts that can see several, the branch. The API applies the account's own limits. */
export function RangeControl({ value, onChange, branches }: RangeControlProps) {
  const problem = value.preset === 'custom' ? rangeProblem(value.custom) : null;

  return (
    <div className="flex flex-col gap-4">
      <div role="group" aria-label="Date range" className="flex flex-wrap gap-2">
        {(Object.keys(PRESET_LABELS) as RangePreset[]).map((preset) => (
          <button key={preset} type="button" aria-pressed={value.preset === preset} onClick={() => onChange({ ...value, preset })} className={chip(value.preset === preset)}>
            {PRESET_LABELS[preset]}
          </button>
        ))}
        <button type="button" aria-pressed={value.preset === 'custom'} onClick={() => onChange({ ...value, preset: 'custom' })} className={chip(value.preset === 'custom')}>
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
  );
}

