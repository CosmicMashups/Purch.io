import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { adminLogin, call } from './support/api';
import type { Seed } from './support/seed';

const SEED_FILE = path.join(path.dirname(fileURLToPath(import.meta.url)), '.seed', 'seed.json');

interface Created {
  id: string;
  pairingCode?: string;
}

/**
 * Creates a brand new business through the real API, so every run starts from known data and never
 * touches another run's. The tenant is left in the database; the container is throwaway.
 */
export default async function globalSetup(): Promise<void> {
  const stamp = Date.now().toString(36);
  const owner = { email: `owner-${stamp}@e2e.test`, password: 'E2e-password-1', pin: '1234' };

  const boot = await call<{ tenantId: string; branchId: string; deviceId: string; devicePairingCode: string }>('POST', '/onboarding/bootstrap', {
    body: { tenantName: `E2E Cafe ${stamp}`, businessType: 2, branchName: 'Main Branch', adminName: 'Olive Owner', adminPin: owner.pin, adminEmail: owner.email, adminPassword: owner.password },
  });
  const { accessToken } = await adminLogin(owner.email, owner.password);

  const api = <T>(method: string, route: string, body?: unknown) => call<T>(method, route, { token: accessToken, body });

  const coffee = await api<Created>('POST', '/categories', { name: 'Coffee', sortOrder: 1, imageUrl: null });
  const bakery = await api<Created>('POST', '/categories', { name: 'Bakery', sortOrder: 2, imageUrl: null });
  const items = [
    { name: 'Iced Latte', sku: 'LATTE', barcode: '4800001', categoryId: coffee.id, basePrice: 150 },
    { name: 'Mocha', sku: 'MOCHA', barcode: '4800002', categoryId: coffee.id, basePrice: 170 },
    { name: 'Ube Cookie', sku: 'COOKIE', barcode: '4800003', categoryId: bakery.id, basePrice: 60 },
  ];
  for (const item of items) {
    const created = await api<Created>('POST', '/items', { ...item, imageUrl: null, pricingType: 0 });
    await api('POST', '/inventory/movements', { itemId: created.id, branchId: boot.branchId, type: 0, quantity: 100, note: 'e2e seed', reasonCategory: null, photoUrl: null, supplierReference: null });
  }

  const staff = [
    { name: 'Mia Manager', role: 1, scopeType: 0, scopeId: null, branchId: null, pin: '2222' },
    { name: 'Carlo Cashier', role: 2, scopeType: 1, scopeId: boot.branchId, branchId: boot.branchId, pin: '3333' },
    { name: 'Wendy Warehouse', role: 3, scopeType: 1, scopeId: boot.branchId, branchId: boot.branchId, pin: '4444' },
  ];
  for (const member of staff) await api('POST', '/staff', member);

  const device = async (deviceType: number, identifier: string, pairingPin: string) => {
    const created = await api<Created>('POST', '/devices', { branchId: boot.branchId, deviceIdentifier: identifier, deviceType, pairingPin });
    return { code: created.pairingCode as string, pin: pairingPin };
  };

  const seed: Seed = {
    branchId: boot.branchId,
    owner,
    register: { code: boot.devicePairingCode },
    pins: { admin: '1234', manager: '2222', cashier: '3333', warehouse: '4444' },
    kiosk: await device(1, 'E2E Kiosk', '5555'),
    orderBoard: await device(2, 'E2E Order Board', '6666'),
    kitchen: await device(3, 'E2E Kitchen', '7777'),
    items: Object.fromEntries(items.map((i) => [i.name, { barcode: i.barcode, price: i.basePrice }])),
  };

  mkdirSync(path.dirname(SEED_FILE), { recursive: true });
  writeFileSync(SEED_FILE, JSON.stringify(seed, null, 2));
}
