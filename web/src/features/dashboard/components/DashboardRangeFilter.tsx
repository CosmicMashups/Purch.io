import { FormField, controlClass } from '../../../components/forms/FormField';
import { rangeProblem } from '../../reports/range';
import { DASHBOARD_PRESET_LABELS, type DashboardPreset, type DashboardRangeChoice } from '../dashboardRange';

const chip = (active: boolean) =>
  `h-10 rounded-control px-4 text-sm font-semibold ${active ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`;

/** Picks the period the Revenue, Busiest days and Top sellers panels report on. */
export function DashboardRangeFilter({ value, onChange }: { value: DashboardRangeChoice; onChange: (next: DashboardRangeChoice) => void }) {
  const problem = value.preset === 'custom' ? rangeProblem(value.custom) : null;
  return (
    <div className="flex flex-col gap-3">
      <div role="group" aria-label="Reporting period" className="flex flex-wrap gap-2">
        {(Object.keys(DASHBOARD_PRESET_LABELS) as DashboardPreset[]).map((preset) => (
          <button key={preset} type="button" aria-pressed={value.preset === preset} onClick={() => onChange({ ...value, preset })} className={chip(value.preset === preset)}>
            {DASHBOARD_PRESET_LABELS[preset]}
          </button>
        ))}
      </div>
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
    </div>
  );
}
