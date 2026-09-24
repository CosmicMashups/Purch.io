import { PricingType, type Item } from '../../catalog/types';
import { formatPeso } from '../../dashboard/format';
import { addFlowFor, stockBadge } from '../catalogView';

interface ItemGridProps {
  items: Item[];
  onPick: (item: Item) => void;
}

function priceLabel(item: Item): string {
  if (item.pricingType === PricingType.VariantMatrix) return 'Choose option';
  return formatPeso(item.basePrice);
}

export function ItemGrid({ items, onPick }: ItemGridProps) {
  if (items.length === 0) {
    return <p className="rounded-panel border border-dashed border-ink-soft/40 p-8 text-center text-base text-ink-soft">No items match. Try another category or search.</p>;
  }

  return (
    <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-4">
      {items.map((item) => {
        const badge = stockBadge(item);
        const flow = addFlowFor(item);
        return (
          <li key={item.id}>
            <button
              type="button"
              onClick={() => onPick(item)}
              className="flex min-h-32 w-full flex-col justify-between overflow-hidden rounded-panel border border-line bg-surface p-4 text-left hover:border-brand active:translate-y-px"
            >
              <span className="flex flex-col items-start gap-1">
                {badge && (
                  <span className={`rounded-full px-2 py-0.5 text-xs font-bold ${badge === 'out' ? 'bg-red-100 text-red-900' : 'bg-amber-100 text-amber-900'}`}>
                    {badge === 'out' ? 'Out of stock' : 'Low stock'}
                  </span>
                )}
                <span className="line-clamp-2 text-lg font-semibold leading-snug">{item.name}</span>
              </span>
              <span className="mt-2 flex items-end justify-between gap-2">
                <span className="text-base font-semibold tabular-nums text-brand-strong">{priceLabel(item)}</span>
                {flow === 'weight' && <span className="text-sm text-ink-soft">by weight</span>}
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
