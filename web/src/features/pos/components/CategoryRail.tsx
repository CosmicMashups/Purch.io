import { Tag } from '@phosphor-icons/react';
import { useEffect, useRef } from 'react';
import { PurchImage } from '../../../components/brand/PurchImage';
import type { RailTile } from '../catalogView';

interface CategoryRailProps {
  tiles: RailTile[];
  activeId: string | null;
  onSelect: (id: string) => void;
  /** `lg` is the kiosk's: bigger pictures and type for a customer standing at arm's length. */
  size?: 'md' | 'lg';
  className?: string;
}

/**
 * The category column shared by the till and the kiosk: each category's picture over its name, the current one
 * marked with a brand bar. It only reports taps. What happens next (scrolling the menu to that section) belongs to
 * the page, and the page tells the rail which category is current as the menu scrolls.
 */
export function CategoryRail({ tiles, activeId, onSelect, size = 'md', className = '' }: CategoryRailProps) {
  return (
    <nav aria-label="Categories" className={`kiosk-scroll-hidden flex snap-y flex-col overflow-y-auto overflow-x-hidden border-r border-line bg-surface print:hidden ${className}`}>
      {tiles.map((tile) => (
        <RailButton key={tile.id} tile={tile} selected={tile.id === activeId} size={size} onSelect={onSelect} />
      ))}
    </nav>
  );
}

function RailButton({ tile, selected, size, onSelect }: { tile: RailTile; selected: boolean; size: 'md' | 'lg'; onSelect: (id: string) => void }) {
  const ref = useRef<HTMLButtonElement>(null);
  const large = size === 'lg';

  // As the menu scrolls, keep the current tile visible inside the rail without moving the page.
  useEffect(() => {
    if (selected) ref.current?.scrollIntoView?.({ block: 'nearest' });
  }, [selected]);

  return (
    <button
      ref={ref}
      type="button"
      aria-pressed={selected}
      aria-disabled={tile.disabled}
      onClick={() => !tile.disabled && onSelect(tile.id)}
      className={`relative flex w-full shrink-0 snap-start flex-col items-center justify-center border-b border-line/60 text-center transition-colors duration-150 ${
        large ? 'min-h-36 gap-2 px-2' : 'h-28 gap-1.5 px-1.5'
      } ${selected ? 'bg-brand-tint text-brand-strong' : 'text-ink hover:bg-canvas'} ${tile.disabled ? 'cursor-default opacity-35' : 'active:bg-brand-tint'}`}
    >
      {selected && <span aria-hidden="true" className="absolute inset-y-3 left-0 w-1 rounded-r bg-brand" />}
      <span className={`grid shrink-0 place-items-center text-ink-soft ${large ? 'size-[4.5rem]' : 'size-14'}`}>
        <PurchImage src={tile.imageUrl} alt="" className="size-full object-contain" errorNode={<Tag size={large ? 44 : 36} aria-hidden="true" />} />
      </span>
      <span className={`line-clamp-2 w-full leading-tight ${large ? 'text-sm' : 'text-xs'} ${selected ? 'font-bold' : 'font-semibold'}`}>{tile.label}</span>
    </button>
  );
}
