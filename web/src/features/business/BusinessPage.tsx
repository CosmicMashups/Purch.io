import { HubGroups } from '../../components/HubGroups';
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

      <HubGroups groups={groups} stats={stats} idPrefix="hub" />
    </div>
  );
}
