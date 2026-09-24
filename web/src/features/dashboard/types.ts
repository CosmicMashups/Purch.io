/** Mirrors Purch.Application.Reporting.SalesDashboardDto. Dates are ISO `yyyy-MM-dd` strings. */
export interface DailyRevenuePoint {
  date: string;
  revenue: number;
}

export interface TopSellingItem {
  itemId: string;
  itemName: string;
  quantitySold: number;
  revenue: number;
}

export interface BranchRevenue {
  branchId: string;
  branchName: string;
  revenue: number;
}

export interface SalesDashboard {
  revenueToday: number;
  revenueLast7Days: number;
  revenueLast30Days: number;
  trend: DailyRevenuePoint[];
  topSellingItems: TopSellingItem[];
  branchComparison: BranchRevenue[];
}

/** Mirrors Purch.Application.Inventory.InventoryDashboardDto. */
export interface LowStockItem {
  itemId: string;
  itemName: string;
  stockOnHand: number;
  lowStockThreshold: number;
}

export interface InventoryDashboard {
  totalSkus: number;
  outOfStockCount: number;
  lowStockCount: number;
  lowStockItems: LowStockItem[];
}

/** Mirrors Purch.Application.Sync.FlaggedSyncRecordDto. */
export interface FlaggedSyncRecord {
  id: string;
  deviceId: string;
  entityType: string;
  entityId: string;
  clientTimestamp: string;
  reviewedAt: string | null;
}
