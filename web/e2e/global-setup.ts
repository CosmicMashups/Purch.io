import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { call, signIn } from './support/api';
import type { PairedDevice, Seed } from './support/seed';

const SEED_FILE = path.join(path.dirname(fileURLToPath(import.meta.url)), '.seed', 'seed.json');

interface Created {
  id: string;
}

/**
 * Creates a brand new business through the real API, so every run starts from known data and never
 * touches another run's. People and devices are set up the way the product does it: staff are invited and open
 * their link, devices are created and paired with their one-time code. The tenant is left in the database; the
 * container is throwaway.
 */
export default async function globalSetup(): Promise<void> {
  const stamp = Date.now().toString(36);
  const owner = { email: `owner-${stamp}@e2e.test`, password: 'E2e-password-1', pin: '1234' };

  const boot = await call<{ tenantId: string; branchId: string; adminMembershipId: string }>('POST', '/onboarding/bootstrap', {
    body: { tenantName: `E2E Cafe ${stamp}`, businessType: 2, branchName: 'Main Branch', adminName: 'Olive Owner', adminPin: owner.pin, adminEmail: owner.email, adminPassword: owner.password },
  });
  const { accessToken } = await signIn(owner.email, owner.password);

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

  // Role 1 Manager, 2 Staff; duties 1 Cashier, 2 Warehouse.
  const people = [
    { key: 'manager', name: 'Mia Manager', role: 1, duties: 0, branchIds: [] as string[], pin: '2222' },
    { key: 'cashier', name: 'Carlo Cashier', role: 2, duties: 1, branchIds: [boot.branchId], pin: '3333' },
    { key: 'warehouse', name: 'Wendy Warehouse', role: 2, duties: 2, branchIds: [boot.branchId], pin: '4444' },
  ] as const;
  const emails = new Map<string, string>();
  for (const person of people) {
    const email = `${person.key}-${stamp}@e2e.test`;
    emails.set(person.key, email);
    const link = await api<{ token: string }>('POST', '/staff/invites', { name: person.name, email, role: person.role, duties: person.duties, branchIds: person.branchIds });
    await call('POST', '/enrol/redeem', { body: { token: link.token, password: 'E2e-password-1', pin: person.pin } });
  }
  const members = await api<{ id: string; email: string }[]>('GET', '/staff/members');
  const memberId = (key: string) => members.find((m) => m.email === emails.get(key))!.id;

  const device = async (deviceType: number, name: string): Promise<PairedDevice> => {
    const request = await api<{ device: { id: string }; pairingCode: string }>('POST', '/devices/pairing-requests', { name, deviceType, branchId: boot.branchId, linkedRegisterDeviceId: null });
    const paired = await call<{ deviceCredential: string }>('POST', '/devices/pair', { body: { pairingCode: request.pairingCode } });
    return { id: request.device.id, credential: paired.deviceCredential };
  };

  const seed: Seed = {
    branchId: boot.branchId,
    owner,
    register: await device(0, 'E2E Till'),
    warehouse: await device(4, 'E2E Stock Room'),
    members: { admin: boot.adminMembershipId, manager: memberId('manager'), cashier: memberId('cashier'), warehouse: memberId('warehouse') },
    pins: { admin: owner.pin, manager: '2222', cashier: '3333', warehouse: '4444' },
    kiosk: await device(1, 'E2E Kiosk'),
    orderBoard: await device(2, 'E2E Order Board'),
    kitchen: await device(3, 'E2E Kitchen'),
    items: Object.fromEntries(items.map((i) => [i.name, { barcode: i.barcode, price: i.basePrice }])),
  };

  mkdirSync(path.dirname(SEED_FILE), { recursive: true });
  writeFileSync(SEED_FILE, JSON.stringify(seed, null, 2));
}
