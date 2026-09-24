import { Link } from 'react-router-dom';
import { useSession } from '../auth/useSession';
import { AsyncPanel } from './components/AsyncPanel';
import { RankedList } from './components/RankedList';
import { RevenueCards } from './components/RevenueCards';
import { TrendChart } from './components/TrendChart';
import { formatPeso, greetingFor, stockRatio } from './format';
import { useFlaggedSync, useInventoryDashboard, useSalesDashboard } from './queries';
import { tabsForRole } from '../../permissions/navPolicy';
import { DepartmentPanel, MovementPanel, StaffPanels } from '../reports/components/RangePanels';
import { choiceToParams, defaultChoice } from '../reports/params';

export function HomePage() {
  const { role } = useSession();
  // Mirrors the API's rules (reports and sync review: Admin/Manager; inventory dashboard also Warehouse).
  const canReport = role === 'Admin' || role === 'Manager';
  const canSeeStock = canReport || role === 'Warehouse';
  const tabs = tabsForRole(role);

  const sales = useSalesDashboard(canReport);
  const inventory = useInventoryDashboard(canSeeStock);
  const flagged = useFlaggedSync(canReport);
  const monthParams = canReport ? choiceToParams(defaultChoice('30d')) : null;
  const openConflicts = flagged.data?.filter((r) => r.reviewedAt === null).length ?? 0;

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold tracking-tight">{greetingFor(new Date().getHours())}</h1>

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

      {canReport && <RevenueCards />}

      {canReport && (
        <AsyncPanel
          title="Sales trend"
          subtitle="Daily revenue"
          query={sales}
          isEmpty={(d) => d.trend.length === 0}
          emptyMessage="No sales recorded yet. Completed sales appear here after checkout."
          minHeight="min-h-56"
        >
          {(d) => <TrendChart points={d.trend} />}
        </AsyncPanel>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        {canReport && (
          <AsyncPanel
            title="Top sellers"
            subtitle="Best-selling items by revenue"
            query={sales}
            isEmpty={(d) => d.topSellingItems.length === 0}
            emptyMessage="No item sales yet."
          >
            {(d) => {
              const max = Math.max(...d.topSellingItems.map((i) => i.revenue), 1);
              return (
                <RankedList
                  rows={d.topSellingItems.map((item) => ({
                    key: item.itemId,
                    label: item.itemName,
                    share: item.revenue / max,
                    detail: `${item.quantitySold} sold, ${formatPeso(item.revenue)}`,
                  }))}
                />
              );
            }}
          </AsyncPanel>
        )}

        {canSeeStock && (
          <AsyncPanel
            title="Running low"
            subtitle="Stock on hand against its alert level"
            query={inventory}
            isEmpty={(d) => d.lowStockItems.length === 0}
            emptyMessage="Nothing is running low right now."
          >
            {(d) => (
              <RankedList
                rows={[...d.lowStockItems]
                  .sort((a, b) => stockRatio(a.stockOnHand, a.lowStockThreshold) - stockRatio(b.stockOnHand, b.lowStockThreshold))
                  .slice(0, 8)
                  .map((item) => ({
                    key: item.itemId,
                    label: item.itemName,
                    share: stockRatio(item.stockOnHand, item.lowStockThreshold),
                    detail: `${item.stockOnHand} left, alert at ${item.lowStockThreshold}`,
                    tone: item.stockOnHand <= 0 ? 'danger' : 'warn',
                  }))}
              />
            )}
          </AsyncPanel>
        )}

        {canReport && (
          <AsyncPanel
            title="Branches"
            subtitle="Revenue by branch"
            query={sales}
            isEmpty={(d) => d.branchComparison.length < 2}
            emptyMessage="Branch comparison shows once more than one branch is in your scope."
          >
            {(d) => {
              const max = Math.max(...d.branchComparison.map((b) => b.revenue), 1);
              return (
                <RankedList
                  rows={d.branchComparison.map((b) => ({
                    key: b.branchId,
                    label: b.branchName,
                    share: b.revenue / max,
                    detail: formatPeso(b.revenue),
                  }))}
                />
              );
            }}
          </AsyncPanel>
        )}
      </div>
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
            <DepartmentPanel params={monthParams} />
            <MovementPanel params={monthParams} />
          </div>
        </section>
      )}
    </div>
  );
}
