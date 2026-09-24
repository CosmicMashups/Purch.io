import { Link } from 'react-router-dom';
import { LinkButton, PageHeader } from '../../../components/PageHeader';
import { Skeleton } from '../../../components/Skeleton';
import { ErrorState } from '../../../components/ErrorState';
import { userMessage } from '../../../lib/apiError';
import { useSession } from '../../auth/useSession';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { stockRatio } from '../../dashboard/format';
import { useInventoryDashboard } from '../../dashboard/queries';
import { MovementType } from '../types';

interface Tile {
  label: string;
  hint: string;
  to: string;
  managersOnly?: boolean;
}

const TILES: readonly Tile[] = [
  { label: 'Stock movements', hint: 'Every stock-in, spoilage and sale', to: '/inventory/movements' },
  { label: 'Stock transfers', hint: 'Move stock between branches', to: '/inventory/transfers' },
  { label: 'Ingredients', hint: 'Counts and deliveries', to: '/inventory/ingredients' },
  { label: 'Suppliers', hint: 'Who you buy from', to: '/inventory/suppliers' },
  { label: 'Purchase orders', hint: 'Order and receive stock', to: '/inventory/purchase-orders' },
  { label: 'Items', hint: 'Prices and pricing types', to: '/catalog/items', managersOnly: true },
  { label: 'Categories', hint: 'Group items for the till', to: '/catalog/categories', managersOnly: true },
];

function StatCard({ label, value, tone }: { label: string; value: number | undefined; tone?: 'warn' | 'danger' }) {
  const color = tone === 'danger' ? 'text-danger' : tone === 'warn' ? 'text-warn' : '';
  return (
    <li className="rounded-panel border border-line bg-surface p-5">
      <p className="text-sm font-medium text-ink-soft">{label}</p>
      {value === undefined ? <Skeleton className="mt-2 h-9 w-16" /> : <p className={`mt-1 text-3xl font-bold tabular-nums ${color}`}>{value}</p>}
    </li>
  );
}

export function InventoryHomePage() {
  const { role } = useSession();
  const dashboard = useInventoryDashboard(true);
  const isManager = role === 'Admin' || role === 'Manager';

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Inventory" action={<LinkButton to="/inventory/movements/new" primary>Record movement</LinkButton>} />

      {dashboard.isError ? (
        <ErrorState title="Stock totals are unavailable" message={userMessage(dashboard.error)} onRetry={() => void dashboard.refetch()} />
      ) : (
        <ul className="grid gap-4 sm:grid-cols-3">
          <StatCard label="Items tracked" value={dashboard.data?.totalSkus} />
          <StatCard label="Running low" value={dashboard.data?.lowStockCount} tone="warn" />
          <StatCard label="Out of stock" value={dashboard.data?.outOfStockCount} tone="danger" />
        </ul>
      )}

      <AsyncPanel
        title="Low stock alerts"
        subtitle="Most urgent first"
        query={dashboard}
        isEmpty={(d) => d.lowStockItems.length === 0}
        emptyMessage="Nothing is running low right now."
      >
        {(d) => (
          <ul className="flex flex-col divide-y divide-line">
            {[...d.lowStockItems]
              .sort((a, b) => stockRatio(a.stockOnHand, a.lowStockThreshold) - stockRatio(b.stockOnHand, b.lowStockThreshold))
              .map((item) => (
                <li key={item.itemId} className="flex flex-wrap items-center justify-between gap-3 py-3">
                  <div className="min-w-0">
                    <p className="truncate text-base font-semibold">{item.itemName}</p>
                    <p className={`text-sm ${item.stockOnHand <= 0 ? 'font-semibold text-danger' : 'text-ink-soft'}`}>
                      {item.stockOnHand <= 0 ? 'Out of stock' : `${item.stockOnHand} left`}, alert at {item.lowStockThreshold}
                    </p>
                  </div>
                  <Link
                    to={`/inventory/movements/new?itemId=${item.itemId}&type=${MovementType.StockIn}`}
                    className="grid h-12 place-items-center rounded-control border border-line px-5 text-base font-semibold hover:border-brand"
                  >
                    Add stock
                  </Link>
                </li>
              ))}
          </ul>
        )}
      </AsyncPanel>

      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {TILES.filter((tile) => !tile.managersOnly || isManager).map((tile) => (
          <li key={tile.to}>
            <Link to={tile.to} className="flex min-h-24 flex-col justify-center rounded-panel border border-line bg-surface p-5 hover:border-brand">
              <span className="text-lg font-semibold">{tile.label}</span>
              <span className="text-base text-ink-soft">{tile.hint}</span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
