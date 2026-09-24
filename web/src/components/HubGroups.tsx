import { CaretRight, WarningCircle } from '@phosphor-icons/react';
import { Link } from 'react-router-dom';

export interface HubTile {
  id: string;
  label: string;
  hint: string;
  to: string;
  /** Left out for roles that cannot manage the catalog. */
  managersOnly?: boolean;
}

export interface HubGroup {
  title: string;
  tiles: readonly HubTile[];
}

export interface HubStat {
  text: string;
  /** A stat that asks for action is shown with a warning icon as well as words. */
  warn?: boolean;
}

/** Grouped links, each with a live figure beside it. Shared by the Business and Inventory hubs. */
export function HubGroups({ groups, stats, idPrefix }: { groups: readonly HubGroup[]; stats: Record<string, HubStat | undefined>; idPrefix: string }) {
  return (
    <div className="grid gap-x-8 gap-y-8 md:grid-cols-2">
      {groups.map((group) => (
        <section key={group.title} aria-labelledby={`${idPrefix}-${group.title}`}>
          <h2 id={`${idPrefix}-${group.title}`} className="text-xl font-bold tracking-tight">
            {group.title}
          </h2>
          <ul className="mt-2 divide-y divide-line border-y border-line">
            {group.tiles.map((tile) => {
              const stat = stats[tile.id];
              return (
                <li key={tile.id}>
                  <Link to={tile.to} className="-mx-2 flex min-h-16 items-center gap-4 rounded-control px-2 py-3 hover:bg-surface">
                    <span className="min-w-0 flex-1">
                      <span className="block text-lg font-semibold">{tile.label}</span>
                      <span className="block text-sm text-ink-soft">{tile.hint}</span>
                    </span>
                    {stat && (
                      <span className={`flex shrink-0 items-center gap-1.5 text-sm font-medium tabular-nums ${stat.warn ? 'text-warn' : 'text-ink-soft'}`}>
                        {stat.warn && <WarningCircle size={18} weight="fill" aria-hidden="true" />}
                        {stat.text}
                      </span>
                    )}
                    <CaretRight size={20} aria-hidden="true" className="shrink-0 text-ink-soft" />
                  </Link>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}
