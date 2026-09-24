/** Integer enums from Purch.Domain.Enums, as the API serializes them. */
export const Role = { Admin: 0, Manager: 1, Cashier: 2, Warehouse: 3, Kiosk: 4, OrderBoard: 5, KitchenDisplay: 6 } as const;
export type Role = (typeof Role)[keyof typeof Role];

export const ScopeType = { Tenant: 0, Branch: 1, Department: 2 } as const;
export type ScopeType = (typeof ScopeType)[keyof typeof ScopeType];

export const DeviceType = { Register: 0, Kiosk: 1, OrderBoard: 2, KitchenDisplay: 3, WarehouseOfficer: 4 } as const;
export type DeviceType = (typeof DeviceType)[keyof typeof DeviceType];

export const ReceiptPrinterProfile = { None: 0, ThermalEscPos: 1 } as const;
export type ReceiptPrinterProfile = (typeof ReceiptPrinterProfile)[keyof typeof ReceiptPrinterProfile];

export const CashDrawerPolicy = { KickOnSaleOnly: 0, AllowManualOpenWithManagerOverride: 1 } as const;
export type CashDrawerPolicy = (typeof CashDrawerPolicy)[keyof typeof CashDrawerPolicy];

export const BusinessType = {
  ConvenienceStore: 0,
  Restaurant: 1,
  Cafe: 2,
  ClothingShop: 3,
  DepartmentStore: 4,
  GroceryStore: 5,
  SariSariStore: 6,
  ServiceEstablishment: 7,
  Other: 8,
} as const;
export type BusinessType = (typeof BusinessType)[keyof typeof BusinessType];

export const AuditActionType = {
  Void: 0,
  Refund: 1,
  DiscountOverride: 2,
  PriceOverride: 3,
  InventoryAdjustment: 4,
  DepartmentReassignment: 5,
  CreditLimitOverride: 6,
  CashDrawerManualOpen: 7,
  StaffAccessChanged: 8,
  CatalogPriceChanged: 9,
  CustomerAnonymized: 10,
} as const;
export type AuditActionType = (typeof AuditActionType)[keyof typeof AuditActionType];

export const roleLabels: Record<number, string> = {
  [Role.Admin]: 'Admin',
  [Role.Manager]: 'Manager',
  [Role.Cashier]: 'Cashier',
  [Role.Warehouse]: 'Warehouse',
  [Role.Kiosk]: 'Kiosk',
  [Role.OrderBoard]: 'Order board',
  [Role.KitchenDisplay]: 'Kitchen display',
};

/** Roles a person can hold. Kiosk, order board and kitchen display are device roles, never staff. */
export const STAFF_ROLES: readonly Role[] = [Role.Admin, Role.Manager, Role.Cashier, Role.Warehouse];

export const scopeLabels: Record<number, string> = {
  [ScopeType.Tenant]: 'Whole business',
  [ScopeType.Branch]: 'One branch',
  [ScopeType.Department]: 'One department',
};

export const deviceTypeLabels: Record<number, string> = {
  [DeviceType.Register]: 'Register',
  [DeviceType.Kiosk]: 'Kiosk',
  [DeviceType.OrderBoard]: 'Order board',
  [DeviceType.KitchenDisplay]: 'Kitchen display',
  [DeviceType.WarehouseOfficer]: 'Warehouse officer',
};

export const businessTypeLabels: Record<number, string> = {
  [BusinessType.ConvenienceStore]: 'Convenience store',
  [BusinessType.Restaurant]: 'Restaurant',
  [BusinessType.Cafe]: 'Cafe',
  [BusinessType.ClothingShop]: 'Clothing shop',
  [BusinessType.DepartmentStore]: 'Department store',
  [BusinessType.GroceryStore]: 'Grocery store',
  [BusinessType.SariSariStore]: 'Sari-sari store',
  [BusinessType.ServiceEstablishment]: 'Service business',
  [BusinessType.Other]: 'Other',
};

export const printerProfileLabels: Record<number, string> = {
  [ReceiptPrinterProfile.None]: 'No receipt printer',
  [ReceiptPrinterProfile.ThermalEscPos]: 'Thermal receipt printer',
};

export const cashDrawerPolicyLabels: Record<number, string> = {
  [CashDrawerPolicy.KickOnSaleOnly]: 'Opens only when a sale is paid',
  [CashDrawerPolicy.AllowManualOpenWithManagerOverride]: 'Also opens by hand with manager approval',
};

export const auditActionLabels: Record<number, string> = {
  [AuditActionType.Void]: 'Sale voided',
  [AuditActionType.Refund]: 'Sale refunded',
  [AuditActionType.DiscountOverride]: 'Discount override',
  [AuditActionType.PriceOverride]: 'Price override',
  [AuditActionType.InventoryAdjustment]: 'Inventory adjusted',
  [AuditActionType.DepartmentReassignment]: 'Department changed',
  [AuditActionType.CreditLimitOverride]: 'Credit limit override',
  [AuditActionType.CashDrawerManualOpen]: 'Drawer opened by hand',
  [AuditActionType.StaffAccessChanged]: 'Staff access changed',
  [AuditActionType.CatalogPriceChanged]: 'Catalog price changed',
  [AuditActionType.CustomerAnonymized]: 'Customer anonymized',
};

export function labelOf(labels: Record<number, string>, value: number): string {
  return labels[value] ?? 'Unknown';
}
