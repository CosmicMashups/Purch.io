import { CaretRight, WarningCircle } from '@phosphor-icons/react';
import { Link } from 'react-router-dom';
import { isBusinessTileVisible } from '../../permissions/navPolicy';
import { useSession } from '../auth/useSession';
import { useCategories, useItems, useModifierGroups } from '../catalog/queries';
import { useBranches } from '../branches/queries';
import { useCreditLedgers } from '../credit/queries';
import { useFlaggedSync } from '../dashboard/queries';
import { useDevices } from './deviceQueries';
import { TILE_GROUPS, hubStats } from './hub';
import { MoneyOwed } from './overview/MoneyOwed';
import { NeedsAttention } from './overview/NeedsAttention';
import { DevicesPanel, TeamPanel } from './overview/TeamAndDevices';
import { useStaff } from './staffQueries';

export function BusinessPage() {
  const { role } = useSession();
  const isAdmin = role === 'Admin';
  const canManage = role === 'Admin' || role === 'Manager';

  const items = useItems();
  const categories = useCategories();
  const modifierGroups = useModifierGroups();
  const staff = useStaff();
  const branches = useBranches();
  const devices = useDevices(isAdmin);
  const credit = useCreditLedgers(canManage);
  const flagged = useFlaggedSync(canManage);

  const stats = hubStats({
    itemCount: items.data?.length,
    categoryCount: categories.data?.length,
    modifierGroupCount: modifierGroups.data?.length,
    staff: staff.data,
    branchCount: branches.data?.length,
    deviceCount: devices.data?.length,
    credit: credit.data,
    flagged: flagged.data,
  });

  const groups = TILE_GROUPS.map((group) => ({ ...group, tiles: group.tiles.filter((tile) => isBusinessTileVisible(tile.id, role)) })).filter((g) => g.tiles.length > 0);

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold tracking-tight">Business</h1>

      {canManage && <NeedsAttention />}

      {canManage && (
        <div className="grid gap-6 lg:grid-cols-2">
          <MoneyOwed />
          <TeamPanel />
        </div>
      )}

      {isAdmin && <DevicesPanel />}

      <div className="grid gap-x-8 gap-y-8 md:grid-cols-2">
        {groups.map((group) => (
          <section key={group.title} aria-labelledby={`hub-${group.title}`}>
            <h2 id={`hub-${group.title}`} className="text-xl font-bold tracking-tight">
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
    </div>
  );
}
