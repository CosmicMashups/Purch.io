import { Link } from 'react-router-dom';
import { isBusinessTileVisible } from '../../permissions/navPolicy';
import { useSession } from '../auth/useSession';

interface Tile {
  id: string;
  label: string;
  hint: string;
  to: string;
}

const TILES: readonly Tile[] = [
  { id: 'catalog-items', label: 'Items', hint: 'Prices, variants, combos', to: '/catalog/items' },
  { id: 'catalog-categories', label: 'Categories', hint: 'Group items for the till', to: '/catalog/categories' },
  { id: 'catalog-modifiers', label: 'Modifier groups', hint: 'Add-ons and options', to: '/catalog/modifier-groups' },
  { id: 'reports', label: 'Reports', hint: 'Staff, stock, X and Z readings, exports', to: '/business/reports' },
  { id: 'customers', label: 'Customers', hint: 'Utang accounts, payments, reminders', to: '/business/customers' },
  { id: 'staff', label: 'Staff', hint: 'Roles, access and PINs', to: '/business/staff' },
  { id: 'branches', label: 'Branches', hint: 'Hardware, GCash QR, departments', to: '/business/branches' },
  { id: 'devices', label: 'Devices', hint: 'Pairing codes for registers and kiosks', to: '/business/devices' },
  { id: 'business-settings', label: 'Business settings', hint: 'Logo, colours, BIR details, options', to: '/business/settings' },
  { id: 'sync-conflicts', label: 'Sync conflicts', hint: 'Changes that clashed between devices', to: '/business/sync-conflicts' },
  { id: 'audit-log', label: 'Audit log', hint: 'Who did what, and when', to: '/business/audit-log' },
  { id: 'promotions', label: 'Promotions', hint: 'Buy 1 Take 1, combos, discounts, codes', to: '/business/promotions' },
];

export function BusinessPage() {
  const { role } = useSession();
  const tiles = TILES.filter((tile) => isBusinessTileVisible(tile.id, role));

  return (
    <section>
      <h1 className="text-2xl font-bold tracking-tight">Business</h1>
      <ul className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {tiles.map((tile) => (
          <li key={tile.id}>
            <Link
              to={tile.to}
              className="flex min-h-28 flex-col justify-center rounded-panel border border-line bg-surface p-6 hover:border-brand"
            >
              <span className="text-lg font-semibold">{tile.label}</span>
              <span className="text-base text-ink-soft">{tile.hint}</span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
