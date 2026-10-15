import { PeriodChip } from '../../../components/PeriodChip';
import { SegmentedToggle } from '../../../components/SegmentedToggle';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { rangeProblem } from '../../reports/range';
import { DASHBOARD_PRESET_LABELS, dashboardDays, type DashboardPreset, type DashboardRangeChoice } from '../dashboardRange';
import { formatDay } from '../format';

const OPTIONS = (Object.keys(DASHBOARD_PRESET_LABELS) as DashboardPreset[]).map((id) => ({ id, label: DASHBOARD_PRESET_LABELS[id] }));

/** Picks the period the Revenue, Busiest days and Top sellers panels report on. */
export function DashboardRangeFilter({ value, onChange }: { value: DashboardRangeChoice; onChange: (next: DashboardRangeChoice) => void }) {
  const problem = value.preset === 'custom' ? rangeProblem(value.custom) : null;
  const days = dashboardDays(value);
  const dates = days.fromDay === days.toDay ? formatDay(days.fromDay) : `${formatDay(days.fromDay)} to ${formatDay(days.toDay)}`;

  return (
    <PeriodChip label={DASHBOARD_PRESET_LABELS[value.preset]} dates={dates}>
      <SegmentedToggle label="Reporting period" value={value.preset} options={OPTIONS} onChange={(preset) => onChange({ ...value, preset })} />
      {value.preset === 'custom' && (
        <div className="grid max-w-xl gap-4 sm:grid-cols-2">
          <FormField label="From">
            <input type="date" value={value.custom.fromDay} onChange={(e) => onChange({ ...value, custom: { ...value.custom, fromDay: e.target.value } })} className={controlClass} />
          </FormField>
          <FormField label="To" error={problem ?? undefined}>
            <input type="date" value={value.custom.toDay} onChange={(e) => onChange({ ...value, custom: { ...value.custom, toDay: e.target.value } })} className={controlClass} />
          </FormField>
        </div>
      )}
    </PeriodChip>
  );
}
