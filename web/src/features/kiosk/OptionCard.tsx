import { Check } from '@phosphor-icons/react';
import { PurchImage } from '../../components/brand/PurchImage';

interface OptionCardProps {
  /** A radio card replaces the group's choice; a checkbox card toggles on its own. */
  kind: 'radio' | 'checkbox';
  title: string;
  selected: boolean;
  onSelect: () => void;
  /** Shown on the right: "+₱25", "Included", or nothing. */
  price?: string;
  /** A second line under the title, such as what the choice uses or why it cannot be picked. */
  detail?: string;
  /** Pictures are only for choices that are different products (a drink to pick for a combo), never for plain options. */
  imageUrl?: string | null;
  showImage?: boolean;
  soldOut?: boolean;
}

/** One big, tappable choice. Selected cards take the business's colour and settle in with a short scale; sold-out ones stay visible but cannot be picked. */
export function OptionCard({ kind, title, selected, onSelect, price, detail, imageUrl, showImage = false, soldOut = false }: OptionCardProps) {
  return (
    <button
      type="button"
      role={kind}
      aria-checked={selected}
      disabled={soldOut}
      onClick={onSelect}
      className={`flex min-h-24 w-full items-center gap-4 rounded-panel border-2 px-5 py-4 text-left transition-colors duration-150 disabled:cursor-not-allowed disabled:opacity-50 ${
        selected ? 'border-brand bg-brand-tint motion-safe:animate-[kiosk-pick_220ms_ease-out]' : 'border-line bg-surface hover:border-brand/60'
      }`}
    >
      <span
        aria-hidden="true"
        className={`grid size-9 shrink-0 place-items-center border-2 ${kind === 'radio' ? 'rounded-full' : 'rounded-lg'} ${selected ? 'border-brand bg-brand text-on-brand' : 'border-ink-soft/50 bg-surface'}`}
      >
        {selected && <Check size={22} weight="bold" />}
      </span>
      {showImage && (
        <span className="size-16 shrink-0 overflow-hidden rounded-control bg-canvas">
          <PurchImage src={imageUrl} alt="" className="size-full object-cover" errorNode={<span className="block size-full" />} />
        </span>
      )}
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="text-xl font-semibold leading-snug">{title}</span>
        {(detail || soldOut) && <span className={`text-base ${soldOut ? 'font-semibold text-danger' : 'text-ink-soft'}`}>{soldOut ? 'Sold out' : detail}</span>}
      </span>
      {price && <span className="shrink-0 text-xl font-bold tabular-nums text-brand-strong">{price}</span>}
    </button>
  );
}
