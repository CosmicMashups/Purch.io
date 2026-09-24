import { BulletList } from '../../../components/charts/RankedCharts';
import { Meter } from '../../../components/charts/Meter';
import { AllClear } from '../../../components/feedback/AllClear';
import { stockRatio } from '../format';
import { useInventoryDashboard } from '../queries';
import { AsyncPanel } from './AsyncPanel';

const items = (n: number) => `${n} item${n === 1 ? '' : 's'}`;

/** How much of the range is short, and which items are closest to running out. */
export function StockHealth() {
  const inventory = useInventoryDashboard(true);
  return (
    <AsyncPanel
      title="Stock"
      subtitle="Items against the level you set to be warned at"
      query={inventory}
      isEmpty={(d) => d.totalSkus === 0}
      emptyMessage="No items yet. Add items to track their stock."
    >
      {(d) => (
        <div className="grid gap-x-10 gap-y-6 lg:grid-cols-[minmax(0,18rem)_minmax(0,1fr)]">
          <div className="flex flex-col gap-6">
            <Meter
              label="Running low"
              value={d.lowStockCount}
              total={d.totalSkus}
              unit="items"
              tone={d.lowStockCount > 0 ? 'warn' : 'ok'}
              status={d.lowStockCount > 0 ? `${items(d.lowStockCount)} at or under the warning level` : 'Nothing is running low'}
            />
            <Meter
              label="Out of stock"
              value={d.outOfStockCount}
              total={d.totalSkus}
              unit="items"
              tone={d.outOfStockCount > 0 ? 'danger' : 'ok'}
              status={d.outOfStockCount > 0 ? `${items(d.outOfStockCount)} cannot be sold` : 'Everything can be sold'}
            />
          </div>
          <div>
            <h3 className="mb-3 text-base font-semibold">Closest to running out</h3>
            {d.lowStockItems.length === 0 ? (
              <AllClear>Nothing is running low right now.</AllClear>
            ) : (
              <BulletList
                mode="floor"
                rows={[...d.lowStockItems]
                  .sort((a, b) => stockRatio(a.stockOnHand, a.lowStockThreshold) - stockRatio(b.stockOnHand, b.lowStockThreshold))
                  .slice(0, 8)
                  .map((item) => ({ key: item.itemId, label: item.itemName, value: item.stockOnHand, target: item.lowStockThreshold }))}
                describe={(row) => (row.value <= 0 ? { value: 'Out', note: `of stock, alert at ${row.target}`, alert: true } : { value: String(row.value), note: `left, alert at ${row.target}` })}
              />
            )}
          </div>
        </div>
      )}
    </AsyncPanel>
  );
}
