import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PageHeader } from '../../components/PageHeader';
import { useSession } from '../auth/useSession';
import { useSelectableBranches } from '../branches/queries';
import { BirPanel } from './components/BirPanel';
import { ExportsPanel } from './components/ExportsPanel';
import { DepartmentPanel, MovementPanel, StaffPanels } from './components/RangePanels';
import { RangeControl } from './components/RangeControl';
import { choiceToParams, defaultChoice, type RangeChoice } from './params';

const TABS = [
  { id: 'staff', label: 'Staff', usesRange: true },
  { id: 'departments', label: 'Departments', usesRange: true },
  { id: 'stock', label: 'Stock movement', usesRange: true },
  { id: 'bir', label: 'X and Z readings', usesRange: false },
  { id: 'exports', label: 'Exports', usesRange: true },
] as const;

type TabId = (typeof TABS)[number]['id'];

function isTabId(value: string | null): value is TabId {
  return TABS.some((t) => t.id === value);
}

export function ReportsPage() {
  const [search, setSearch] = useSearchParams();
  const requested = search.get('tab');
  const active: TabId = isTabId(requested) ? requested : 'staff';
  const tab = TABS.find((t) => t.id === active)!;
  const { role } = useSession();
  const { branches } = useSelectableBranches();
  const [choice, setChoice] = useState<RangeChoice>(defaultChoice('30d'));
  const params = choiceToParams(choice);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Reports" backTo={{ to: '/business', label: 'Business' }} />

      <div role="tablist" aria-label="Report" className="flex flex-wrap gap-2 print:hidden">
        {TABS.map((t) => {
          const selected = t.id === active;
          return (
            <button
              key={t.id}
              type="button"
              role="tab"
              id={`report-tab-${t.id}`}
              aria-selected={selected}
              aria-controls={`report-panel-${t.id}`}
              onClick={() => setSearch({ tab: t.id }, { replace: true })}
              className={`h-12 rounded-control px-5 text-base font-semibold ${selected ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`}
            >
              {t.label}
            </button>
          );
        })}
      </div>

      {tab.usesRange && (
        <div className="print:hidden">
          <RangeControl value={choice} onChange={setChoice} branches={branches ?? []} />
        </div>
      )}

      <div role="tabpanel" id={`report-panel-${active}`} aria-labelledby={`report-tab-${active}`}>
        {active === 'bir' && <BirPanel />}
        {active === 'exports' && <ExportsPanel params={params} isAdmin={role === 'Admin'} />}
        {(active === 'staff' || active === 'departments' || active === 'stock') &&
          (params ? (
            <>
              {active === 'staff' && <StaffPanels params={params} />}
              {active === 'departments' && <DepartmentPanel params={params} />}
              {active === 'stock' && <MovementPanel params={params} />}
            </>
          ) : (
            <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">Choose a valid date range to see this report.</p>
          ))}
      </div>
    </div>
  );
}
