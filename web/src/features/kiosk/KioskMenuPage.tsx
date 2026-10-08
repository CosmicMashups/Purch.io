import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { toast } from '../../components/feedback/toastStore';
import { userMessage } from '../../lib/apiError';
import { useCategories, useItems } from '../catalog/queries';
import type { Item } from '../catalog/types';
import { CategoryRail } from '../pos/components/CategoryRail';
import { addFlowFor, buildRailTiles, groupItemsByCategory } from '../pos/catalogView';
import { useSectionSpy } from '../pos/useSectionSpy';
import { KioskActionBar } from './KioskActionBar';
import { KioskItemCard } from './KioskItemCard';
import { menuScroll } from './menuScroll';

const grid = 'grid grid-cols-[repeat(auto-fill,minmax(15rem,1fr))] gap-4 landscape:grid-cols-[repeat(auto-fill,minmax(16rem,1fr))]';

/**
 * The whole menu in one scroll, grouped by category, with the category rail on the left. Tapping a category scrolls
 * to it and the rail follows as the customer scrolls. Tapping an item opens its page to choose quantity and options.
 */
export function KioskMenuPage() {
  const navigate = useNavigate();
  const items = useItems();
  const categories = useCategories();
  const [scroller, setScroller] = useState<HTMLElement | null>(null);

  const sections = useMemo(() => groupItemsByCategory(items.data ?? [], (categories.data ?? []).filter((c) => c.isActive !== false)), [items.data, categories.data]);
  const tiles = useMemo(() => buildRailTiles((categories.data ?? []).filter((c) => c.isActive !== false), sections), [categories.data, sections]);
  const spy = useSectionSpy(
    useMemo(() => sections.map((section) => section.id), [sections]),
    scroller,
  );

  // Put the customer back where they were scrolled to, once there is a menu to scroll.
  const ready = items.isSuccess && scroller !== null;
  useEffect(() => {
    if (ready) scroller.scrollTo?.({ top: menuScroll.top, behavior: 'instant' });
  }, [ready, scroller]);
  useEffect(
    () => () => {
      if (scroller) menuScroll.top = scroller.scrollTop;
    },
    [scroller],
  );

  function pick(item: Item) {
    if (addFlowFor(item) === 'weight') {
      toast.info(`${item.name} is sold by weight. Please order it at the counter.`);
      return;
    }
    if (scroller) menuScroll.top = scroller.scrollTop;
    navigate(`/kiosk/item/${item.id}`);
  }

  return (
    <div className="flex h-full flex-col">
      <div className="flex min-h-0 flex-1">
        <CategoryRail tiles={tiles} activeId={spy.activeId} onSelect={spy.scrollTo} size="lg" className="w-28 shrink-0 landscape:w-36" />

        <main ref={setScroller} aria-label="Menu" className="kiosk-scroll min-w-0 flex-1 overflow-y-auto px-5 pb-8 pt-2">
          {items.isPending && (
            <div className={grid} aria-busy="true">
              {Array.from({ length: 8 }, (_, i) => (
                <Skeleton key={i} className="h-64 w-full" />
              ))}
            </div>
          )}
          {items.isError && <ErrorState title="The menu could not be loaded" message={userMessage(items.error)} onRetry={() => void items.refetch()} />}
          {items.isSuccess && sections.length === 0 && (
            <p className="rounded-panel border border-dashed border-ink-soft/40 p-10 text-center text-xl text-ink-soft">Nothing is on the menu right now. Please order at the counter.</p>
          )}
          {items.isSuccess &&
            sections.map((section) => (
              <section key={section.id} ref={spy.sectionRef(section.id)} data-section-id={section.id} aria-labelledby={`menu-${section.id}`} className="scroll-mt-2 pb-8">
                <h2 id={`menu-${section.id}`} className="mb-3 mt-4 text-3xl font-extrabold tracking-tight">
                  {section.name}
                </h2>
                <ul className={grid}>
                  {section.items.map((item) => (
                    <li key={item.id}>
                      <KioskItemCard item={item} onPick={pick} />
                    </li>
                  ))}
                </ul>
              </section>
            ))}
        </main>
      </div>

      <KioskActionBar />
    </div>
  );
}
