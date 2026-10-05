import type { RailTile } from '../catalogView';
import { CategoryRail } from './CategoryRail';

interface CategoryStripProps {
  tiles: RailTile[];
  activeId: string | null;
  onSelect: (id: string) => void;
}

/** The till's category column: the shared rail pinned to the left edge beside the app shell's navigation. */
export function CategoryStrip({ tiles, activeId, onSelect }: CategoryStripProps) {
  return (
    <CategoryRail
      tiles={tiles}
      activeId={activeId}
      onSelect={onSelect}
      className="fixed bottom-0 left-0 top-[4.5rem] z-20 w-24 md:left-28 md:top-0"
    />
  );
}
