import { Link } from 'react-router-dom';
import { LinkButton, PageHeader } from '../../../components/PageHeader';
import { RestockList } from '../components/RestockList';
import { StockOverview } from '../components/StockOverview';
import { useSession } from '../../auth/useSession';

interface Tile {
  label: string;
  hint: string;
  to: string;
  managersOnly?: boolean;
}

const TILES: readonly Tile[] = [
  { label: 'Stock movements', hint: 'Every stock-in, spoilage and sale', to: '/inventory/movements' },
  { label: 'Stock transfers', hint: 'Move stock between branches', to: '/inventory/transfers' },
  { label: 'Ingredients', hint: 'Counts and deliveries', to: '/inventory/ingredients' },
  { label: 'Suppliers', hint: 'Who you buy from', to: '/inventory/suppliers' },
  { label: 'Purchase orders', hint: 'Order and receive stock', to: '/inventory/purchase-orders' },
  { label: 'Items', hint: 'Prices and pricing types', to: '/catalog/items', managersOnly: true },
  { label: 'Categories', hint: 'Group items for the till', to: '/catalog/categories', managersOnly: true },
];

export function InventoryHomePage() {
  const { role } = useSession();
  const isManager = role === 'Admin' || role === 'Manager';

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Inventory" action={<LinkButton to="/inventory/movements/new" primary>Record movement</LinkButton>} />

      <StockOverview />

      <RestockList />

      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {TILES.filter((tile) => !tile.managersOnly || isManager).map((tile) => (
          <li key={tile.to}>
            <Link to={tile.to} className="flex min-h-24 flex-col justify-center rounded-panel border border-line bg-surface p-5 hover:border-brand">
              <span className="text-lg font-semibold">{tile.label}</span>
              <span className="text-base text-ink-soft">{tile.hint}</span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
