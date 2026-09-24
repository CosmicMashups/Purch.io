import { describe, expect, it } from 'vitest';
import { inventoryHubStats } from './hub';
import { BranchTransferStatus, PurchaseOrderStatus, type BranchTransfer, type InventoryItem, type PurchaseOrder, type Supplier } from './types';

const ingredient = (quantityOnHand: number, lowStockThreshold: number | null) => ({ quantityOnHand, lowStockThreshold }) as InventoryItem;
const order = (status: PurchaseOrderStatus) => ({ status }) as PurchaseOrder;
const transfer = (status: BranchTransferStatus) => ({ status }) as BranchTransfer;

describe('inventoryHubStats', () => {
  it('leaves out anything that has not loaded', () => {
    expect(inventoryHubStats({})).toEqual({});
  });

  it('flags ingredients at or under their warning level', () => {
    const stats = inventoryHubStats({ ingredients: [ingredient(2, 5), ingredient(9, 5), ingredient(0, null)] });
    expect(stats.ingredients).toEqual({ text: '3 ingredients, 1 low', warn: true });
  });

  it('counts active suppliers and open orders', () => {
    const stats = inventoryHubStats({
      suppliers: [{ isActive: true }, { isActive: false }] as Supplier[],
      purchaseOrders: [order(PurchaseOrderStatus.Sent), order(PurchaseOrderStatus.Draft), order(PurchaseOrderStatus.Received)],
    });
    expect(stats.suppliers?.text).toBe('2 suppliers, 1 active');
    expect(stats['purchase-orders']?.text).toBe('1 awaiting delivery, 1 draft');
  });

  it('summarises transfers on the move', () => {
    expect(inventoryHubStats({ transfers: [transfer(BranchTransferStatus.InTransit), transfer(BranchTransferStatus.Received)] }).transfers?.text).toBe('1 in transit');
    expect(inventoryHubStats({ transfers: [] }).transfers?.text).toBe('None moving');
  });
});
