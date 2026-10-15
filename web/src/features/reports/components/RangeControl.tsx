import { PeriodChip } from '../../../components/PeriodChip';
import { SegmentedToggle } from '../../../components/SegmentedToggle';
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

const OPTIONS = [
  ...(Object.keys(PRESET_LABELS) as RangePreset[]).map((id) => ({ id, label: PRESET_LABELS[id] })),
  { id: 'custom' as const, label: 'Custom' },
];

/** Picks the reporting window and, for accounts that can see several, the branch. The API applies the account's own limits. */
export function RangeControl({ value, onChange, branches }: RangeControlProps) {
  const problem = value.preset === 'custom' ? rangeProblem(value.custom) : null;
  const days = value.preset === 'custom' ? value.custom : presetRange(value.preset);
  const label = value.preset === 'custom' ? 'Custom range' : PRESET_LABELS[value.preset];
  const dates = days.fromDay === days.toDay ? formatDay(days.fromDay) : `${formatDay(days.fromDay)} to ${formatDay(days.toDay)}`;
  const branchName = value.branchId ? branches.find((b) => b.id === value.branchId)?.name : branches.length > 1 ? 'All branches' : undefined;

  return (
    <PeriodChip label={label} dates={dates} detail={branchName}>
      <SegmentedToggle label="Date range" value={value.preset} options={OPTIONS} onChange={(preset) => onChange({ ...value, preset })} />

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
    </PeriodChip>
  );
}
