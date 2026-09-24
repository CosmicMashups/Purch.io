import { Link } from 'react-router-dom';
import { Package } from '@phosphor-icons/react';
import { PurchImage } from '../../../components/brand/PurchImage';
import { useItems } from '../../catalog/queries';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { stockRatio } from '../../dashboard/format';
import { useInventoryDashboard } from '../../dashboard/queries';
import { MovementType } from '../types';

/** The items that need stock, worst first. The bar is stock against the item's own warning level, so a full bar means "at the level". */
export function RestockList() {
  const dashboard = useInventoryDashboard(true);
  const items = useItems();
  const pictures = new Map((items.data ?? []).map((item) => [item.id, item.imageUrl]));

  return (
    <AsyncPanel
      title="Restock first"
      subtitle="Most urgent first"
      query={dashboard}
      isEmpty={(d) => d.lowStockItems.length === 0}
      emptyMessage="Nothing is running low right now."
    >
      {(d) => (
        <ul className="grid gap-x-8 divide-y divide-line @container xl:grid-cols-2 xl:divide-y-0">
          {[...d.lowStockItems]
            .sort((a, b) => stockRatio(a.stockOnHand, a.lowStockThreshold) - stockRatio(b.stockOnHand, b.lowStockThreshold))
            .map((item) => {
              const out = item.stockOnHand <= 0;
              const fill = Math.min(100, Math.max(0, stockRatio(item.stockOnHand, item.lowStockThreshold) * 100));
              return (
                <li key={item.itemId} className="flex items-center gap-4 py-3 xl:border-b xl:border-line">
                  <span className="grid size-14 shrink-0 place-items-center overflow-hidden rounded-lg bg-canvas text-ink-soft">
                    <PurchImage src={pictures.get(item.itemId)} alt="" className="size-full object-cover" errorNode={<Package size={24} aria-hidden="true" />} />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-base font-semibold">{item.itemName}</p>
                    <div className="mt-1.5 h-2 overflow-hidden rounded-full bg-viz-grid" aria-hidden="true">
                      <div className="h-full rounded-full" style={{ width: `${fill}%`, background: out ? 'var(--color-danger)' : 'var(--color-warn)' }} />
                    </div>
                    <p className={`mt-1 text-sm ${out ? 'font-semibold text-danger' : 'text-ink-soft'}`}>
                      {out ? 'Out of stock' : `${item.stockOnHand} left`}, alert at {item.lowStockThreshold}
                    </p>
                  </div>
                  <Link
                    to={`/inventory/movements/new?itemId=${item.itemId}&type=${MovementType.StockIn}`}
                    className="grid h-12 shrink-0 place-items-center rounded-control border border-line px-4 text-base font-semibold hover:border-brand"
                  >
                    Add stock
                  </Link>
                </li>
              );
            })}
        </ul>
      )}
    </AsyncPanel>
  );
}
