import { ChartLineUp, FileText, ShieldWarning, UsersThree, type Icon } from '@phosphor-icons/react';

export interface ReportTab {
  id: string;
  label: string;
}

export interface ReportGroup {
  id: string;
  label: string;
  icon: Icon;
  tabs: ReportTab[];
}

/** Reports grouped by the question they answer. */
export const REPORT_GROUPS: ReportGroup[] = [
  {
    id: 'sales',
    label: 'Sales',
    icon: ChartLineUp,
    tabs: [
      { id: 'sales', label: 'Sales mix' },
      { id: 'profit', label: 'Profit' },
      { id: 'tax', label: 'Tax' },
    ],
  },
  {
    id: 'control',
    label: 'Control',
    icon: ShieldWarning,
    tabs: [
      { id: 'discounts', label: 'Discounts' },
      { id: 'voids', label: 'Voids and refunds' },
      { id: 'approvals', label: 'Approvals' },
    ],
  },
  {
    id: 'people',
    label: 'People and stock',
    icon: UsersThree,
    tabs: [
      { id: 'staff', label: 'Staff' },
      { id: 'departments', label: 'Departments' },
      { id: 'stock', label: 'Stock movement' },
    ],
  },
  {
    id: 'compliance',
    label: 'Compliance',
    icon: FileText,
    tabs: [
      { id: 'bir', label: 'X and Z readings' },
      { id: 'exports', label: 'Exports' },
    ],
  },
];

interface ReportPickerProps {
  active: string;
  /** Tab ids that exist for this business (departments can be switched off). */
  available: (id: string) => boolean;
  onSelect: (tabId: string) => void;
}

/**
 * Two rows of buttons. The top row picks a group (the only brand-filled controls on the page); the row under it
 * picks the report inside that group as underlined tabs. Every report is at most two taps away.
 */
export function ReportPicker({ active, available, onSelect }: ReportPickerProps) {
  const groups = REPORT_GROUPS.map((g) => ({ ...g, tabs: g.tabs.filter((t) => available(t.id)) })).filter((g) => g.tabs.length > 0);
  const current = groups.find((g) => g.tabs.some((t) => t.id === active)) ?? groups[0];

  return (
    <div className="flex flex-col gap-4 print:hidden">
      <div role="group" aria-label="Report group" className="grid grid-cols-2 gap-2 lg:grid-cols-4">
        {groups.map((g) => {
          const selected = g.id === current.id;
          const GroupIcon = g.icon;
          return (
            <button
              key={g.id}
              type="button"
              aria-pressed={selected}
              onClick={() => onSelect(g.tabs[0].id)}
              className={`flex h-14 items-center justify-center gap-2 rounded-control px-4 text-base font-semibold ${selected ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`}
            >
              <GroupIcon size={24} weight={selected ? 'fill' : 'regular'} aria-hidden="true" />
              {g.label}
            </button>
          );
        })}
      </div>

      <div role="tablist" aria-label={`${current.label} reports`} className="flex gap-1 overflow-x-auto border-b border-line">
        {current.tabs.map((t) => {
          const selected = t.id === active;
          return (
            <button
              key={t.id}
              type="button"
              role="tab"
              id={`report-tab-${t.id}`}
              aria-selected={selected}
              aria-controls={`report-panel-${t.id}`}
              onClick={() => onSelect(t.id)}
              className={`-mb-px h-12 shrink-0 border-b-2 px-4 text-base ${selected ? 'border-ink font-semibold text-ink' : 'border-transparent text-ink-soft hover:text-ink'}`}
            >
              {t.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
