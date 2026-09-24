/** The three unattended device roles the API issues tokens for. They never see the staff shell. */
export type DeviceRole = 'Kiosk' | 'KitchenDisplay' | 'OrderBoard';

export const DEVICE_HOME: Record<DeviceRole, string> = {
  Kiosk: '/kiosk',
  KitchenDisplay: '/kitchen',
  OrderBoard: '/order-board',
};

export const DEVICE_PAIR: Record<DeviceRole, string> = {
  Kiosk: '/kiosk/pair',
  KitchenDisplay: '/kitchen/pair',
  OrderBoard: '/order-board/pair',
};

export const DEVICE_LABEL: Record<DeviceRole, string> = {
  Kiosk: 'an order kiosk',
  KitchenDisplay: 'a kitchen display',
  OrderBoard: 'an order board',
};

export function deviceRoleFromClaim(role: string | null | undefined): DeviceRole | null {
  return role === 'Kiosk' || role === 'KitchenDisplay' || role === 'OrderBoard' ? role : null;
}
