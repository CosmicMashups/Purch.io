import { Link } from 'react-router-dom';
import { Package } from '@phosphor-icons/react';
import { PurchImage } from '../../../components/brand/PurchImage';
import { Tabs } from '../../../components/Tabs';
import { useItems } from '../../catalog/queries';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { stockRatio } from '../../dashboard/format';
import { useInventoryDashboard } from '../../dashboard/queries';
import { STOCK_SCOPE_TABS, type StockScope } from '../stockScope';
import { MovementType } from '../types';

interface Row {
  id: string;
  name: string;
  imageUrl?: string | null;
  onHand: number;
  threshold: number;
  unit: string;
  addStockTo: string;
}

/**
 * What needs stock, worst first. The bar is stock against the warning level, so a full bar means "at the level".
 * With separate ingredient tracking on, items and ingredients each get a tab. An ingredient is restocked by receiving a delivery.
 */
export function RestockList({ scope, onScopeChange }: { scope: StockScope; onScopeChange: (scope: StockScope) => void }) {
  const dashboard = useInventoryDashboard(true);
  const items = useItems();
  const pictures = new Map((items.data ?? []).map((item) => [item.id, item.imageUrl]));
  const hasIngredients = Boolean(dashboard.data?.ingredients);
  const active: StockScope = hasIngredients ? scope : 'items';

  function rowsFor(d: NonNullable<typeof dashboard.data>): Row[] {
    if (active === 'ingredients') {
      return (d.ingredients?.lowStock ?? []).map((i) => ({
        id: i.inventoryItemId,
        name: i.name,
        onHand: i.quantityOnHand,
        threshold: i.lowStockThreshold,
        unit: ` ${i.baseUnit}`,
        addStockTo: `/inventory/ingredients?receive=${i.inventoryItemId}`,
      }));
    }
    return d.lowStockItems.map((i) => ({
      id: i.itemId,
      name: i.itemName,
      imageUrl: pictures.get(i.itemId),
      onHand: i.stockOnHand,
      threshold: i.lowStockThreshold,
      unit: '',
      addStockTo: `/inventory/movements?record=1&stockRef=item:${i.itemId}&recordType=${MovementType.StockIn}`,
    }));
  }

  return (
    <AsyncPanel
      title="Restock first"
      subtitle="Most urgent first"
      query={dashboard}
      tabs={hasIngredients && <Tabs label="Restock for" tabs={STOCK_SCOPE_TABS} active={active} onChange={onScopeChange} idPrefix="restock" />}
      isEmpty={(d) => rowsFor(d).length === 0}
      emptyMessage={active === 'ingredients' ? 'No ingredient is running low right now.' : 'Nothing is running low right now.'}
    >
      {(d) => (
        <ul id="restock-panel" role="tabpanel" className="grid gap-x-8 divide-y divide-line @container xl:grid-cols-2 xl:divide-y-0">
          {rowsFor(d)
            .sort((a, b) => stockRatio(a.onHand, a.threshold) - stockRatio(b.onHand, b.threshold))
            .map((row) => {
              const out = row.onHand <= 0;
              const fill = Math.min(100, Math.max(0, stockRatio(row.onHand, row.threshold) * 100));
              return (
                <li key={row.id} className="flex items-center gap-4 py-3 xl:border-b xl:border-line">
                  <span className="grid size-14 shrink-0 place-items-center overflow-hidden rounded-lg bg-canvas text-ink-soft">
                    <PurchImage src={row.imageUrl} alt="" className="size-full object-cover" errorNode={<Package size={24} aria-hidden="true" />} />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-base font-semibold">{row.name}</p>
                    <div className="mt-1.5 h-2 overflow-hidden rounded-full bg-viz-grid" aria-hidden="true">
                      <div className="h-full rounded-full" style={{ width: `${fill}%`, background: out ? 'var(--color-danger)' : 'var(--color-warn)' }} />
                    </div>
                    <p className={`mt-1 text-sm ${out ? 'font-semibold text-danger' : 'text-ink-soft'}`}>
                      {out ? 'Out of stock' : `${row.onHand}${row.unit} left`}, alert at {row.threshold}
                    </p>
                  </div>
                  <Link to={row.addStockTo} className="grid h-12 shrink-0 place-items-center rounded-control border border-line px-4 text-base font-semibold hover:border-brand">
                    {active === 'ingredients' ? 'Receive delivery' : 'Add stock'}
                  </Link>
                </li>
              );
            })}
        </ul>
      )}
    </AsyncPanel>
  );
}
