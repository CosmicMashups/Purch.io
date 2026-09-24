import { CheckCircle, Warning, XCircle } from '@phosphor-icons/react';
import { Donut } from '../../../components/charts/Donut';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { useInventoryDashboard } from '../../dashboard/queries';

const count = (n: number) => `${n} item${n === 1 ? '' : 's'}`;

/** How the range splits into fine, running low and out. Every count is the server's; "in good supply" is what is left of the total. */
export function StockOverview() {
  const dashboard = useInventoryDashboard(true);
  return (
    <AsyncPanel
      title="Stock health"
      subtitle="Every item you track, by how well stocked it is"
      query={dashboard}
      isEmpty={(d) => d.totalSkus === 0}
      emptyMessage="No items are tracked yet. Add an item with a stock level to see it here."
    >
      {(d) => {
        const healthy = Math.max(0, d.totalSkus - d.lowStockCount - d.outOfStockCount);
        return (
          <div className="grid items-center gap-x-10 gap-y-6 @container lg:grid-cols-[auto_minmax(0,1fr)]">
            <Donut
              ariaLabel="Items by stock level"
              totalLabel="Tracked"
              totalValue={String(d.totalSkus)}
              formatValue={count}
              slices={[
                { key: 'ok', label: 'In good supply', value: healthy, color: 'var(--color-ok)' },
                { key: 'low', label: 'Running low', value: d.lowStockCount, color: 'var(--color-warn)' },
                { key: 'out', label: 'Out of stock', value: d.outOfStockCount, color: 'var(--color-danger)' },
              ]}
            />
            <ul className="flex flex-col gap-3">
              <Verdict icon={<CheckCircle size={24} weight="fill" className="text-ok" aria-hidden="true" />} title={count(healthy)} note="in good supply" />
              <Verdict icon={<Warning size={24} weight="fill" className="text-warn" aria-hidden="true" />} title={count(d.lowStockCount)} note="at or under the warning level" />
              <Verdict icon={<XCircle size={24} weight="fill" className="text-danger" aria-hidden="true" />} title={count(d.outOfStockCount)} note="cannot be sold until restocked" />
            </ul>
          </div>
        );
      }}
    </AsyncPanel>
  );
}

function Verdict({ icon, title, note }: { icon: React.ReactNode; title: string; note: string }) {
  return (
    <li className="flex items-center gap-3">
      {icon}
      <p className="text-base">
        <span className="font-bold tabular-nums">{title}</span> <span className="text-ink-soft">{note}</span>
      </p>
    </li>
  );
}
