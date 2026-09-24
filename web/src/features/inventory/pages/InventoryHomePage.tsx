import { HubGroups } from '../../../components/HubGroups';
import { LinkButton, PageHeader } from '../../../components/PageHeader';
import { useSession } from '../../auth/useSession';
import { useCategories, useItems } from '../../catalog/queries';
import { RestockList } from '../components/RestockList';
import { StockOverview } from '../components/StockOverview';
import { INVENTORY_GROUPS, inventoryHubStats } from '../hub';
import { useInventoryItems, usePurchaseOrders, useSuppliers, useTransfers } from '../queries';

export function InventoryHomePage() {
  const { role } = useSession();
  const isManager = role === 'Admin' || role === 'Manager';
  const ingredients = useInventoryItems();
  const suppliers = useSuppliers();
  const orders = usePurchaseOrders();
  const transfers = useTransfers();
  const items = useItems();
  const categories = useCategories();

  const stats = inventoryHubStats({
    ingredients: ingredients.data,
    suppliers: suppliers.data,
    purchaseOrders: orders.data,
    transfers: transfers.data,
    itemCount: isManager ? items.data?.length : undefined,
    categoryCount: isManager ? categories.data?.length : undefined,
  });
  const groups = INVENTORY_GROUPS.map((g) => ({ ...g, tiles: g.tiles.filter((tile) => !tile.managersOnly || isManager) })).filter((g) => g.tiles.length > 0);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Inventory" action={<LinkButton to="/inventory/movements/new" primary>Record movement</LinkButton>} />
      <StockOverview />
      <RestockList />
      <HubGroups groups={groups} stats={stats} idPrefix="inventory-hub" />
    </div>
  );
}
