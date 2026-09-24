export const STAFF_ROLES = ['Admin', 'Manager', 'Cashier', 'Warehouse'] as const;
export type StaffRole = (typeof STAFF_ROLES)[number];

export type AppTab = 'home' | 'sell' | 'inventory' | 'business';

export const BUSINESS_TILES = {
  devices: 'devices',
  businessSettings: 'business-settings',
} as const;

const ADMIN_ONLY_TILES: readonly string[] = [BUSINESS_TILES.devices, BUSINESS_TILES.businessSettings];

/** The backend serializes the C# enum name; match it case-insensitively like the Flutter client. */
export function staffRoleFromClaim(claim: string | null): StaffRole | null {
  if (!claim) return null;
  const lower = claim.toLowerCase();
  return STAFF_ROLES.find((role) => role.toLowerCase() === lower) ?? null;
}

/** Presentation only. The API re-checks every action. An unknown role gets the most restrictive set. */
export function tabsForRole(role: StaffRole | null): readonly AppTab[] {
  switch (role) {
    case 'Admin':
    case 'Manager':
      return ['home', 'sell', 'inventory', 'business'];
    case 'Warehouse':
      return ['home', 'inventory'];
    case 'Cashier':
    case null:
      return ['home', 'sell'];
  }
}

export function isBusinessTileVisible(tileId: string, role: StaffRole | null): boolean {
  if (role === 'Admin') return true;
  if (role === 'Manager') return !ADMIN_ONLY_TILES.includes(tileId);
  return false;
}

const TAB_ROOTS: Record<AppTab, string> = {
  home: '/',
  sell: '/sell',
  inventory: '/inventory',
  business: '/business',
};

export function tabPath(tab: AppTab): string {
  return TAB_ROOTS[tab];
}
