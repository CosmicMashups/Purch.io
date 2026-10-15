import { lazy, Suspense } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Skeleton } from '../../components/Skeleton';
import { PageHeader } from '../../components/PageHeader';
import { useSession } from '../auth/useSession';
import { useSelectableBranches } from '../branches/queries';
import { ApprovalsPanel } from './components/ApprovalsPanel';
import { BirPanel } from './components/BirPanel';
import { ExportsPanel } from './components/ExportsPanel';
import { DepartmentPanel, MovementPanel, StaffPanels } from './components/RangePanels';
import { RangeControl } from './components/RangeControl';
import { ReportPicker } from './components/ReportPicker';
import { choiceToParams } from './params';
import { useRememberedChoice } from './useRememberedChoice';
import { useDepartmentTracking } from '../tenant/queries';

// The newer report tabs load on demand: most visits open one tab, and the page shell should not carry the rest.
const SalesMixPanel = lazy(() => import('./components/SalesPanels').then((m) => ({ default: m.SalesMixPanel })));
const TaxPanel = lazy(() => import('./components/SalesPanels').then((m) => ({ default: m.TaxPanel })));
const ProfitPanel = lazy(() => import('./components/SalesPanels').then((m) => ({ default: m.ProfitPanel })));
const DiscountsPanel = lazy(() => import('./components/LeakagePanels').then((m) => ({ default: m.DiscountsPanel })));
const VoidsRefundsPanel = lazy(() => import('./components/LeakagePanels').then((m) => ({ default: m.VoidsRefundsPanel })));

const TABS = [
  { id: 'staff', label: 'Staff', usesRange: true },
  { id: 'departments', label: 'Departments', usesRange: true },
  { id: 'stock', label: 'Stock movement', usesRange: true },
  { id: 'sales', label: 'Sales mix', usesRange: true },
  { id: 'tax', label: 'Tax', usesRange: true },
  { id: 'profit', label: 'Profit', usesRange: true },
  { id: 'discounts', label: 'Discounts', usesRange: true },
  { id: 'voids', label: 'Voids and refunds', usesRange: true },
  { id: 'bir', label: 'X and Z readings', usesRange: false },
  { id: 'approvals', label: 'Approvals', usesRange: false },
  { id: 'exports', label: 'Exports', usesRange: true },
] as const;

type TabId = (typeof TABS)[number]['id'];

function isTabId(value: string | null): value is TabId {
  return TABS.some((t) => t.id === value);
}

export function ReportsPage() {
  const [search, setSearch] = useSearchParams();
  const departmentsOn = useDepartmentTracking();
  const tabs = TABS.filter((t) => t.id !== 'departments' || departmentsOn);
  const requested = search.get('tab');
  const active: TabId = isTabId(requested) && tabs.some((t) => t.id === requested) ? requested : 'sales';
  const tab = TABS.find((t) => t.id === active)!;
  const { role } = useSession();
  const { branches } = useSelectableBranches();
  const [choice, setChoice] = useRememberedChoice();
  const params = choiceToParams(choice);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Reports" backTo={{ to: '/business', label: 'Business' }} />

      <ReportPicker
        active={active}
        available={(id) => tabs.some((t) => t.id === id)}
        onSelect={(id) => setSearch({ tab: id }, { replace: true })}
      />

      {tab.usesRange && (
        <div className="print:hidden">
          <RangeControl value={choice} onChange={setChoice} branches={branches ?? []} />
        </div>
      )}

      <div role="tabpanel" id={`report-panel-${active}`} aria-labelledby={`report-tab-${active}`}>
       <Suspense fallback={<Skeleton className="h-64 w-full" />}>
        {active === 'bir' && <BirPanel />}
        {active === 'approvals' && <ApprovalsPanel />}
        {active === 'exports' && <ExportsPanel params={params} isAdmin={role === 'Admin'} />}
        {(active === 'staff' || active === 'departments' || active === 'stock' || active === 'sales' || active === 'tax' || active === 'profit' || active === 'discounts' || active === 'voids') &&
          (params ? (
            <>
              {active === 'staff' && <StaffPanels params={params} />}
              {active === 'departments' && <DepartmentPanel params={params} />}
              {active === 'stock' && <MovementPanel params={params} />}
              {active === 'sales' && <SalesMixPanel params={params} />}
              {active === 'tax' && <TaxPanel params={params} />}
              {active === 'profit' && <ProfitPanel params={params} />}
              {active === 'discounts' && <DiscountsPanel params={params} isAdmin={role === 'Admin'} />}
              {active === 'voids' && <VoidsRefundsPanel params={params} isAdmin={role === 'Admin'} />}
            </>
          ) : (
            <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">Choose a valid date range to see this report.</p>
          ))}
       </Suspense>
      </div>
    </div>
  );
}
