import { CheckCircle, Warning, XCircle } from '@phosphor-icons/react';
import { Donut } from '../../../components/charts/Donut';
import { Tabs } from '../../../components/Tabs';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { useInventoryDashboard } from '../../dashboard/queries';
import { STOCK_SCOPE_TABS, type StockScope } from '../stockScope';

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

/**
 * How the range splits into fine, running low and out. Every count is the server's; "in good supply" is what is left of the total.
 * When the business tracks ingredients separately there is an Items tab and an Ingredients tab, each counted on its own.
 */
export function StockOverview({ scope, onScopeChange }: { scope: StockScope; onScopeChange: (scope: StockScope) => void }) {
  const dashboard = useInventoryDashboard(true);
  const ingredients = dashboard.data?.ingredients ?? null;
  const active: StockScope = ingredients ? scope : 'items';
  const noun = active === 'ingredients' ? { one: 'ingredient', many: 'ingredients' } : { one: 'item', many: 'items' };
  const count = (n: number) => plural(n, noun.one, noun.many);

  return (
    <AsyncPanel
      title="Stock health"
      subtitle={active === 'ingredients' ? 'Every ingredient you track, by how well stocked it is' : 'Every item you track, by how well stocked it is'}
      query={dashboard}
      tabs={ingredients && <Tabs label="Stock health for" tabs={STOCK_SCOPE_TABS} active={active} onChange={onScopeChange} idPrefix="stock-health" />}
      isEmpty={(d) => (active === 'ingredients' ? (d.ingredients?.total ?? 0) === 0 : d.totalSkus === 0)}
      emptyMessage={active === 'ingredients' ? 'No ingredients are tracked yet. Add one under Ingredients to see it here.' : 'No items are tracked yet. Add an item with a stock level to see it here.'}
    >
      {(d) => {
        const shown = active === 'ingredients' && d.ingredients ? { total: d.ingredients.total, low: d.ingredients.lowStockCount, out: d.ingredients.outOfStockCount } : { total: d.totalSkus, low: d.lowStockCount, out: d.outOfStockCount };
        const healthy = Math.max(0, shown.total - shown.low - shown.out);
        return (
          <div id="stock-health-panel" role="tabpanel" className="grid items-center gap-x-10 gap-y-6 @container lg:grid-cols-[auto_minmax(0,1fr)]">
            <Donut
              ariaLabel={`${active === 'ingredients' ? 'Ingredients' : 'Items'} by stock level`}
              totalLabel="Tracked"
              totalValue={String(shown.total)}
              formatValue={count}
              slices={[
                { key: 'ok', label: 'In good supply', value: healthy, color: 'var(--color-ok)' },
                { key: 'low', label: 'Running low', value: shown.low, color: 'var(--color-warn)' },
                { key: 'out', label: 'Out of stock', value: shown.out, color: 'var(--color-danger)' },
              ]}
            />
            <ul className="flex flex-col gap-3">
              <Verdict icon={<CheckCircle size={24} weight="fill" className="text-ok" aria-hidden="true" />} title={count(healthy)} note="in good supply" />
              <Verdict icon={<Warning size={24} weight="fill" className="text-warn" aria-hidden="true" />} title={count(shown.low)} note="at or under the warning level" />
              <Verdict
                icon={<XCircle size={24} weight="fill" className="text-danger" aria-hidden="true" />}
                title={count(shown.out)}
                note={active === 'ingredients' ? 'used up, so recipes that need it cannot be made' : 'cannot be sold until restocked'}
              />
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
