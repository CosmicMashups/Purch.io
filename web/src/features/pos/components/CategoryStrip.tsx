import { SquaresFour, Tag } from '@phosphor-icons/react';
import { PurchImage } from '../../../components/brand/PurchImage';
import type { Category } from '../../catalog/types';

interface CategoryStripProps {
  categories: Category[];
  selectedId: string | null;
  onSelect: (id: string | null) => void;
}

/** Category tiles with their picture, like the Flutter cashier. A category without a picture shows an icon instead. */
export function CategoryStrip({ categories, selectedId, onSelect }: CategoryStripProps) {
  const sorted = [...categories].sort((a, b) => a.sortOrder - b.sortOrder);
  return (
    <div
      role="group"
      aria-label="Categories"
      className="fixed bottom-0 left-0 top-[4.5rem] z-20 flex w-24 snap-y flex-col overflow-y-auto overflow-x-hidden border-r border-line bg-surface [scrollbar-width:none] md:left-28 md:top-0 print:hidden"
    >
      <CategoryTile label="All" selected={selectedId === null} onClick={() => onSelect(null)} icon={<SquaresFour size={36} aria-hidden="true" />} />
      {sorted.map((category) => (
        <CategoryTile
          key={category.id}
          label={category.name}
          selected={selectedId === category.id}
          onClick={() => onSelect(category.id)}
          icon={<Tag size={36} aria-hidden="true" />}
          imageUrl={category.imageUrl}
        />
      ))}
    </div>
  );
}

/** One slot in the rail: the picture on top with no backdrop, the name underneath. Every slot is the same size. */
function CategoryTile({ label, selected, onClick, icon, imageUrl }: { label: string; selected: boolean; onClick: () => void; icon: React.ReactNode; imageUrl?: string | null }) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onClick}
      className={`relative flex h-28 w-24 shrink-0 snap-start flex-col items-center justify-center gap-1.5 px-1.5 text-center w-full border-b border-line/60 ${
        selected ? 'bg-brand-tint text-brand-strong' : 'text-ink hover:bg-canvas'
      }`}
    >
      {selected && <span aria-hidden="true" className="absolute inset-y-3 left-0 w-1 rounded-r bg-brand" />}
      <span className="grid size-14 shrink-0 place-items-center text-ink-soft">
        <PurchImage src={imageUrl} alt="" className="size-full object-contain" errorNode={icon} />
      </span>
      <span className={`line-clamp-2 w-full text-xs leading-tight ${selected ? 'font-bold' : 'font-semibold'}`}>{label}</span>
    </button>
  );
}
