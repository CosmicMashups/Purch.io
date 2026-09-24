import { Package, Scales } from '@phosphor-icons/react';
import { PurchImage } from '../../../components/brand/PurchImage';
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
    <ul className="grid auto-rows-fr grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-4">
      {items.map((item) => {
        const badge = stockBadge(item);
        const flow = addFlowFor(item);
        return (
          <li key={item.id} className="h-full">
            <button
              type="button"
              onClick={() => onPick(item)}
              className="flex h-full w-full flex-col overflow-hidden rounded-panel border border-line bg-surface text-left hover:border-brand active:translate-y-px"
            >
              <span className="relative block aspect-[4/3] w-full shrink-0 overflow-hidden bg-canvas">
                <PurchImage
                  src={item.imageUrl}
                  alt=""
                  className="size-full object-cover"
                  errorNode={
                    <span className="grid size-full place-items-center text-ink-soft">
                      <span className="grid size-12 place-items-center rounded-control bg-brand-tint text-brand-strong">
                        {flow === 'weight' ? <Scales size={26} aria-hidden="true" /> : <Package size={26} aria-hidden="true" />}
                      </span>
                    </span>
                  }
                />
                {badge === 'low' && <span className="absolute left-2 top-2 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-bold text-amber-900">Low stock</span>}
                {badge === 'out' && (
                  <span className="absolute inset-0 grid place-items-center bg-black/45">
                    <span className="rounded-full bg-red-100 px-3 py-1 text-xs font-bold text-red-900">Out of stock</span>
                  </span>
                )}
              </span>
              <span className="flex h-24 shrink-0 flex-col justify-between gap-1 p-3">
                <span className="line-clamp-2 min-h-[2.75rem] text-base font-semibold leading-snug">{item.name}</span>
                <span className="flex items-end justify-between gap-2">
                  <span className="text-base font-bold tabular-nums text-brand-strong">{priceLabel(item)}</span>
                  {flow === 'weight' && <span className="text-sm text-ink-soft">by weight</span>}
                </span>
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
