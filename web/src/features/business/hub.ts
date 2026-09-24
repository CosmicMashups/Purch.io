import { formatPeso } from '../dashboard/format';
import type { CreditLedger } from '../credit/types';
import type { FlaggedSyncRecord } from '../dashboard/types';
import type { StaffMember } from './staffApi';

export interface Tile {
  id: string;
  label: string;
  hint: string;
  to: string;
}

export interface TileGroup {
  title: string;
  tiles: readonly Tile[];
}

export const TILE_GROUPS: readonly TileGroup[] = [
  {
    title: 'Sell and stock',
    tiles: [
      { id: 'catalog-items', label: 'Items', hint: 'Prices, variants, combos', to: '/catalog/items' },
      { id: 'catalog-categories', label: 'Categories', hint: 'Group items for the till', to: '/catalog/categories' },
      { id: 'catalog-modifiers', label: 'Modifier groups', hint: 'Add-ons and options', to: '/catalog/modifier-groups' },
      { id: 'promotions', label: 'Promotions', hint: 'Buy 1 Take 1, combos, discounts, codes', to: '/business/promotions' },
    ],
  },
  {
    title: 'People and places',
    tiles: [
      { id: 'staff', label: 'Staff', hint: 'Roles, access and PINs', to: '/business/staff' },
      { id: 'branches', label: 'Branches', hint: 'Hardware, GCash QR, departments', to: '/business/branches' },
      { id: 'devices', label: 'Devices', hint: 'Pairing codes for registers and kiosks', to: '/business/devices' },
    ],
  },
  {
    title: 'Money',
    tiles: [
      { id: 'customers', label: 'Customers', hint: 'Utang accounts, payments, reminders', to: '/business/customers' },
      { id: 'reports', label: 'Reports', hint: 'Staff, stock, X and Z readings, exports', to: '/business/reports' },
    ],
  },
  {
    title: 'Records and settings',
    tiles: [
      { id: 'audit-log', label: 'Audit log', hint: 'Who did what, and when', to: '/business/audit-log' },
      { id: 'sync-conflicts', label: 'Sync conflicts', hint: 'Changes that clashed between devices', to: '/business/sync-conflicts' },
      { id: 'business-settings', label: 'Business settings', hint: 'Logo, colours, BIR details, options', to: '/business/settings' },
    ],
  },
];

export interface HubStat {
  text: string;
  /** A stat that asks for action is shown with a warning icon as well as words. */
  warn?: boolean;
}

export interface HubData {
  itemCount?: number;
  categoryCount?: number;
  modifierGroupCount?: number;
  staff?: StaffMember[];
  branchCount?: number;
  deviceCount?: number;
  credit?: CreditLedger[];
  flagged?: FlaggedSyncRecord[];
}

const n = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/** The live figure shown beside each link. A figure that has not loaded (or that a role cannot see) is simply left out. */
export function hubStats(data: HubData): Record<string, HubStat | undefined> {
  const stats: Record<string, HubStat | undefined> = {};
  if (data.itemCount !== undefined) stats['catalog-items'] = { text: n(data.itemCount, 'item', 'items') };
  if (data.categoryCount !== undefined) stats['catalog-categories'] = { text: n(data.categoryCount, 'category', 'categories') };
  if (data.modifierGroupCount !== undefined) stats['catalog-modifiers'] = { text: n(data.modifierGroupCount, 'group', 'groups') };
  if (data.staff) {
    const active = data.staff.filter((s) => s.isActive).length;
    const off = data.staff.length - active;
    stats.staff = { text: off > 0 ? `${active} active, ${off} off` : `${active} active` };
  }
  if (data.branchCount !== undefined) stats.branches = { text: n(data.branchCount, 'branch', 'branches') };
  if (data.deviceCount !== undefined) stats.devices = { text: `${data.deviceCount} paired` };
  if (data.credit) {
    const owed = data.credit.filter((l) => l.isActive).reduce((sum, l) => sum + l.balance, 0);
    stats.customers = { text: owed > 0 ? `${formatPeso(owed)} owed` : 'Nothing owed' };
  }
  if (data.flagged) {
    const open = data.flagged.filter((r) => r.reviewedAt === null).length;
    stats['sync-conflicts'] = open > 0 ? { text: `${open} to review`, warn: true } : { text: 'All clear' };
  }
  return stats;
}
