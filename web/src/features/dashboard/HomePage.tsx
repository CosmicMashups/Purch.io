import { useState } from 'react';
import { Link } from 'react-router-dom';
import { DashboardRangeFilter } from './components/DashboardRangeFilter';
import { DASHBOARD_PRESET_LABELS, dashboardParams, defaultDashboardRange } from './dashboardRange';
import { CalendarHeatmap } from '../../components/charts/CalendarHeatmap';
import { assignColors } from '../../components/charts/colors';
import { BarList } from '../../components/charts/RankedCharts';
import { StackedStrip } from '../../components/charts/Proportions';
import { useSession } from '../auth/useSession';
import { AsyncPanel } from './components/AsyncPanel';
import { RevenueOverview } from './components/RevenueOverview';
import { StockHealth } from './components/StockHealth';
import { formatDay, formatPeso, greetingFor } from './format';
import { useFlaggedSync, useRefreshDashboards, useSalesDashboard } from './queries';
import { tabsForRole } from '../../permissions/navPolicy';
import { DepartmentPanel, MovementPanel, StaffPanels } from '../reports/components/RangePanels';
import { choiceToParams, defaultChoice } from '../reports/params';
import { useDepartmentTracking } from '../tenant/queries';

const TOP_SORTS = [
  { key: 'revenue', label: 'By revenue' },
  { key: 'quantity', label: 'By quantity sold' },
] as const;

export function HomePage() {
  const departmentsOn = useDepartmentTracking();
  const { role } = useSession();
  // Mirrors the API's rules (reports and sync review: Admin/Manager; inventory dashboard also Warehouse).
  const canReport = role === 'Admin' || role === 'Manager';
  const canSeeStock = canReport || role === 'Warehouse';
  const tabs = tabsForRole(role);

  const [period, setPeriod] = useState(defaultDashboardRange);
  const [topSort, setTopSort] = useState<'revenue' | 'quantity'>('revenue');
  const periodParams = dashboardParams(period);
  const periodLabel = DASHBOARD_PRESET_LABELS[period.preset];
  const sales = useSalesDashboard(canReport && periodParams !== null, periodParams);
  // The branch comparison is not period-scoped, so it shares the default (last 30 days) query.
  const branchSales = useSalesDashboard(canReport);
  const flagged = useFlaggedSync(canReport);
  const monthParams = canReport ? choiceToParams(defaultChoice('30d')) : null;
  const openConflicts = flagged.data?.filter((r) => r.reviewedAt === null).length ?? 0;
  const refreshDashboards = useRefreshDashboards();

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-3xl font-bold tracking-tight">{greetingFor(new Date().getHours())}</h1>
        {canReport && (
          <button
            type="button"
            onClick={() => void refreshDashboards()}
            className="inline-flex h-10 items-center rounded-control border border-line bg-surface px-4 text-sm font-semibold hover:border-brand"
          >
            Refresh
          </button>
        )}
      </div>

      {openConflicts > 0 && (
        <div role="status" className="rounded-panel border border-warn bg-surface p-5">
          <p className="text-lg font-semibold">
            {openConflicts === 1 ? '1 offline record needs review' : `${openConflicts} offline records need review`}
          </p>
          <p className="text-base text-ink-soft">
            Two devices changed the same record. The later change won; the other is kept here so nothing is lost.
          </p>
          <Link to="/business/sync-conflicts" className="mt-2 inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
            Review them
          </Link>
        </div>
      )}

      <div className="flex flex-wrap gap-3">
        {tabs.includes('sell') && (
          <Link to="/sell" className="grid h-14 place-items-center rounded-control bg-brand px-8 text-lg font-semibold text-on-brand hover:bg-brand-strong">
            New sale
          </Link>
        )}
        {tabs.includes('sell') && (
          <>
            <Link to="/sell/shift" className="grid h-14 place-items-center rounded-control border border-line bg-surface px-8 text-lg font-semibold hover:border-brand">
              Shift
            </Link>
            <Link to="/sell/kiosk-orders" className="grid h-14 place-items-center rounded-control border border-line bg-surface px-8 text-lg font-semibold hover:border-brand">
              Kiosk orders
            </Link>
          </>
        )}
        {tabs.includes('inventory') && (
          <Link to="/inventory" className="grid h-14 place-items-center rounded-control border border-line bg-surface px-8 text-lg font-semibold hover:border-brand">
            Inventory
          </Link>
        )}
      </div>

      {canReport && <DashboardRangeFilter value={period} onChange={setPeriod} />}

      {canReport && <RevenueOverview range={periodParams} periodLabel={periodLabel} />}

      {canReport && (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,19rem)_minmax(0,1fr)]">
          <AsyncPanel
            title="Busiest days"
            subtitle="Revenue each day. Darker is more."
            query={sales}
            isEmpty={(d) => d.trend.length === 0}
            emptyMessage="Days fill in as sales are recorded."
          >
            {(d) => (
              <CalendarHeatmap
                points={d.trend.map((p) => ({ key: p.date, label: formatDay(p.date), value: p.revenue }))}
                seriesName="Revenue"
                formatValue={formatPeso}
                ariaLabel="Revenue for each of the last days"
              />
            )}
          </AsyncPanel>

          <AsyncPanel
            title="Top sellers"
            subtitle={topSort === 'revenue' ? `Highest-earning items · ${periodLabel}` : `Most units sold · ${periodLabel}`}
            query={sales}
            isEmpty={(d) => d.topSellingItems.length === 0}
            emptyMessage="No item sales in this period."
            tabs={
              <div role="group" aria-label="Rank top sellers" className="flex gap-2">
                {TOP_SORTS.map(({ key, label }) => (
                  <button
                    key={key}
                    type="button"
                    aria-pressed={topSort === key}
                    onClick={() => setTopSort(key)}
                    className={`h-9 rounded-control px-4 text-sm font-semibold ${topSort === key ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`}
                  >
                    {label}
                  </button>
                ))}
              </div>
            }
          >
            {(d) =>
              topSort === 'revenue' ? (
                <BarList
                  formatValue={formatPeso}
                  rows={d.topSellingItems.map((item) => ({ key: item.itemId, label: item.itemName, value: item.revenue, detail: `${item.quantitySold} sold` }))}
                />
              ) : (
                <BarList
                  formatValue={(n) => `${n.toLocaleString('en-PH', { maximumFractionDigits: 2 })} sold`}
                  rows={d.topSellingItemsByQuantity.map((item) => ({ key: item.itemId, label: item.itemName, value: item.quantitySold, detail: formatPeso(item.revenue) }))}
                />
              )
            }
          </AsyncPanel>
        </div>
      )}

      {canSeeStock && <StockHealth />}

      {/* A single-branch business has nothing to compare, so the card stays out of the way. */}
      {canReport && (branchSales.data?.branchComparison.length ?? 0) > 1 && (
        <AsyncPanel
          title="Branches"
          subtitle="Revenue by branch"
          query={branchSales}
          isEmpty={(d) => d.branchComparison.length < 2}
          emptyMessage="Branch comparison shows once more than one branch is in your scope."
        >
          {(d) => {
            const colors = assignColors(d.branchComparison.map((b) => b.branchId));
            return (
              <StackedStrip
                formatValue={formatPeso}
                ariaLabel="Revenue by branch"
                items={[...d.branchComparison]
                  .sort((a, b) => b.revenue - a.revenue)
                  .map((b) => ({ key: b.branchId, label: b.branchName, value: b.revenue, color: colors.get(b.branchId) as string }))}
              />
            );
          }}
        </AsyncPanel>
      )}

      {monthParams && (
        <section aria-labelledby="month-heading" className="flex flex-col gap-6">
          <div className="flex flex-wrap items-baseline justify-between gap-3">
            <h2 id="month-heading" className="text-2xl font-bold tracking-tight">
              Last 30 days
            </h2>
            <Link to="/business/reports" className="inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
              More reports and exports
            </Link>
          </div>
          <StaffPanels params={monthParams} />
          <div className="grid gap-6 lg:grid-cols-2">
            {departmentsOn && <DepartmentPanel params={monthParams} />}
            <MovementPanel params={monthParams} />
          </div>
        </section>
      )}
    </div>
  );
}
