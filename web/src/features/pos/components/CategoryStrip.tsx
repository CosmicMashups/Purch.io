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
    <div role="group" aria-label="Categories" className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
      <CategoryTile label="All" selected={selectedId === null} onClick={() => onSelect(null)} icon={<SquaresFour size={24} aria-hidden="true" />} />
      {sorted.map((category) => (
        <CategoryTile
          key={category.id}
          label={category.name}
          selected={selectedId === category.id}
          onClick={() => onSelect(category.id)}
          icon={<Tag size={24} aria-hidden="true" />}
          imageUrl={category.imageUrl}
        />
      ))}
    </div>
  );
}

function CategoryTile({ label, selected, onClick, icon, imageUrl }: { label: string; selected: boolean; onClick: () => void; icon: React.ReactNode; imageUrl?: string | null }) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onClick}
      className={`flex h-16 w-44 shrink-0 items-center gap-3 rounded-control border py-2 pl-2 pr-4 text-left active:translate-y-px ${
        selected ? 'border-brand bg-brand-tint text-brand-strong ring-1 ring-brand' : 'border-line bg-surface hover:border-brand'
      }`}
    >
      <span className={`grid size-12 shrink-0 place-items-center overflow-hidden rounded-lg ${selected ? 'bg-surface text-brand-strong' : 'bg-canvas text-ink-soft'}`}>
        <PurchImage src={imageUrl} alt="" className="size-full object-cover" errorNode={icon} />
      </span>
      <span className={`line-clamp-2 min-w-0 text-base leading-tight ${selected ? 'font-bold' : 'font-semibold'}`}>{label}</span>
    </button>
  );
}
