import { Package, Scales } from '@phosphor-icons/react';
import { PurchImage } from '../../components/brand/PurchImage';
import { PricingType, type Item } from '../catalog/types';
import { formatPeso } from '../dashboard/format';
import { addFlowFor, stockBadge } from '../pos/catalogView';

function priceLabel(item: Item): string {
  return item.pricingType === PricingType.VariantMatrix ? 'Choose option' : formatPeso(item.basePrice);
}

/** One item on the kiosk menu. A sold-out item stays on the menu so the catalog reads the same everywhere, but cannot be tapped. */
export function KioskItemCard({ item, onPick }: { item: Item; onPick: (item: Item) => void }) {
  const badge = stockBadge(item);
  const byWeight = addFlowFor(item) === 'weight';
  const soldOut = badge === 'out';

  return (
    <button
      type="button"
      disabled={soldOut}
      onClick={() => onPick(item)}
      className="flex h-full w-full flex-col overflow-hidden rounded-panel border border-line bg-surface text-left transition-transform duration-100 hover:border-brand active:translate-y-px disabled:cursor-not-allowed"
    >
      <span className="relative block aspect-[4/3] w-full shrink-0 overflow-hidden bg-canvas">
        <PurchImage
          src={item.imageUrl}
          alt=""
          className="size-full object-cover"
          errorNode={
            <span className="grid size-full place-items-center">
              <span className="grid size-16 place-items-center rounded-control bg-brand-tint text-brand-strong">
                {byWeight ? <Scales size={34} aria-hidden="true" /> : <Package size={34} aria-hidden="true" />}
              </span>
            </span>
          }
        />
        {soldOut && (
          <span className="absolute inset-0 grid place-items-center bg-black/45">
            <span className="rounded-full bg-red-100 px-4 py-1.5 text-base font-bold text-red-900">Out of stock</span>
          </span>
        )}
        {badge === 'low' && (
          <span className="absolute left-3 top-3 rounded-full bg-amber-100 px-3 py-1 text-sm font-bold text-amber-900">
            {item.stockOnHand > 0 ? `Only ${Math.floor(item.stockOnHand)} left` : 'Running low'}
          </span>
        )}
      </span>
      <span className="flex flex-1 flex-col justify-between gap-2 p-4">
        <span className="line-clamp-2 text-xl font-semibold leading-snug">{item.name}</span>
        <span className="flex items-end justify-between gap-2">
          <span className="text-xl font-bold tabular-nums text-brand-strong">{priceLabel(item)}</span>
          {byWeight && <span className="text-base text-ink-soft">Ask staff</span>}
        </span>
      </span>
    </button>
  );
}
