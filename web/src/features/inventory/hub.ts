import type { HubGroup, HubStat } from '../../components/HubGroups';
import { BranchTransferStatus, PurchaseOrderStatus, type BranchTransfer, type InventoryItem, type PurchaseOrder, type Supplier } from './types';


export const INVENTORY_GROUPS: readonly HubGroup[] = [
  {
    title: 'Stock',
    tiles: [
      { id: 'movements', label: 'Stock movements', hint: 'Every stock-in, spoilage and sale', to: '/inventory/movements' },
      { id: 'transfers', label: 'Stock transfers', hint: 'Move stock between branches', to: '/inventory/transfers' },
      { id: 'ingredients', label: 'Ingredients', hint: 'Counts and deliveries', to: '/inventory/ingredients' },
    ],
  },
  {
    title: 'Buying',
    tiles: [
      { id: 'suppliers', label: 'Suppliers', hint: 'Who you buy from', to: '/inventory/suppliers' },
      { id: 'purchase-orders', label: 'Purchase orders', hint: 'Order and receive stock', to: '/inventory/purchase-orders' },
    ],
  },
  {
    title: 'Catalog',
    tiles: [
      { id: 'items', label: 'Items', hint: 'Prices and pricing types', to: '/catalog/items', managersOnly: true },
      { id: 'categories', label: 'Categories', hint: 'Group items for the till', to: '/catalog/categories', managersOnly: true },
    ],
  },
];

export interface InventoryHubData {
  ingredients?: InventoryItem[];
  suppliers?: Supplier[];
  purchaseOrders?: PurchaseOrder[];
  transfers?: BranchTransfer[];
  itemCount?: number;
  categoryCount?: number;
}

const n = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/** The live figure beside each link. One that has not loaded, or that a role cannot see, is left out. */
export function inventoryHubStats(data: InventoryHubData): Record<string, HubStat | undefined> {
  const stats: Record<string, HubStat | undefined> = {};
  if (data.ingredients) {
    const low = data.ingredients.filter((i) => i.lowStockThreshold !== null && i.quantityOnHand <= i.lowStockThreshold).length;
    const total = n(data.ingredients.length, 'ingredient', 'ingredients');
    stats.ingredients = low > 0 ? { text: `${total}, ${low} low`, warn: true } : { text: total };
  }
  if (data.suppliers) {
    const active = data.suppliers.filter((s) => s.isActive).length;
    stats.suppliers = { text: `${n(data.suppliers.length, 'supplier', 'suppliers')}, ${active} active` };
  }
  if (data.purchaseOrders) {
    const open = data.purchaseOrders.filter((p) => p.status === PurchaseOrderStatus.Sent || p.status === PurchaseOrderStatus.PartiallyReceived).length;
    const drafts = data.purchaseOrders.filter((p) => p.status === PurchaseOrderStatus.Draft).length;
    const parts = [open > 0 && `${open} awaiting delivery`, drafts > 0 && n(drafts, 'draft', 'drafts')].filter(Boolean);
    stats['purchase-orders'] = { text: parts.length > 0 ? parts.join(', ') : 'None open' };
  }
  if (data.transfers) {
    const moving = data.transfers.filter((t) => t.status === BranchTransferStatus.InTransit).length;
    const pending = data.transfers.filter((t) => t.status === BranchTransferStatus.Pending).length;
    const parts = [moving > 0 && `${moving} in transit`, pending > 0 && `${pending} pending`].filter(Boolean);
    stats.transfers = { text: parts.length > 0 ? parts.join(', ') : 'None moving' };
  }
  if (data.itemCount !== undefined) stats.items = { text: n(data.itemCount, 'item', 'items') };
  if (data.categoryCount !== undefined) stats.categories = { text: n(data.categoryCount, 'category', 'categories') };
  return stats;
}
