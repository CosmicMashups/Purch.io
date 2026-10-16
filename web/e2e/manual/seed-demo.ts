import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { randomBytes, randomInt } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { call, deviceSession, signIn, unlock, type Tokens } from '../support/api';

/**
 * A believable demo business for the user manual: "Kapihan ni Aling Nena", a Filipino cafe with a small grocery counter.
 * Everything goes through the real API, the way the product's own screens do it. Only the age of the sales is changed
 * afterwards (see backdate), because the API stamps every sale with the moment it is made.
 *
 * Passwords and PINs are generated here, written to e2e/.seed/demo.json (git-ignored) and never to the docs.
 */

export const DEMO_FILE = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '.seed', 'demo.json');

export interface DemoPerson {
  name: string;
  email: string;
  password: string;
  pin: string;
  membershipId: string;
}

export interface DemoDevice {
  id: string;
  credential: string;
  name: string;
}

export interface DemoSeed {
  tenantName: string;
  branches: { poblacion: string; riverside: string };
  people: { admin: DemoPerson; manager: DemoPerson; cashier: DemoPerson; cashier2: DemoPerson; warehouse: DemoPerson };
  devices: { register: DemoDevice; register2: DemoDevice; register3: DemoDevice; kiosk: DemoDevice; kitchen: DemoDevice; orderBoard: DemoDevice; customerDisplay: DemoDevice; warehouse: DemoDevice };
  /** A device waiting for its one-time code, for the pairing screen. */
  pendingPairing: { name: string; code: string };
  /** An invite nobody has opened yet. */
  pendingInvite: { name: string; email: string; token: string };
  items: Record<string, { id: string; price: number; barcode: string | null; categoryId: string }>;
  categories: Record<string, string>;
  ids: Record<string, string>;
  customers: Record<string, string>;
  /** Receipt numbers (per register) of recent sales at Counter 1, to look up. */
  receipts: { cash: number; split: number };
}

interface Created {
  id: string;
}

/** The pictures the web app ships with (see lib/images.ts), and flat monogram tiles for everything else. */
const PHOTO = { drink: 'assets/images/beverage_iced_latte.jpg', meal: 'assets/images/combo_rice_bowl.jpg', poster: 'assets/images/kiosk_poster_default.jpg', gcash: 'assets/images/sample_qr_ph.jpg' };
const TILE_COLOURS: Record<string, string> = { Kape: '#8A4B1F', Meryenda: '#C9833A', Ulam: '#A33B2A', Inumin: '#2F7F86', Grocery: '#5D7A3A', 'Sari-sari': '#7A4F8F' };
function tile(label: string, category: string): string {
  const initials = label
    .replace(/\(.*?\)/g, '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join('');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="240" height="240"><rect width="240" height="240" fill="${TILE_COLOURS[category]}"/><text x="120" y="142" font-family="Georgia,serif" font-size="88" fill="#FFF8EE" text-anchor="middle">${initials}</text></svg>`;
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
}

// A fixed seed makes every run produce the same catalogue and the same sales.
let rngState = 20261016;
function rand(): number {
  rngState = (rngState + 0x6d2b79f5) | 0;
  let t = Math.imul(rngState ^ (rngState >>> 15), 1 | rngState);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}
const pick = <T>(list: readonly T[]): T => list[Math.floor(rand() * list.length)];
const between = (min: number, max: number) => min + Math.floor(rand() * (max - min + 1));
const log = (message: string) => console.log(`[seed] ${message}`);
const password = () => `Kp-${randomBytes(9).toString('base64url')}`;

function pins(count: number): string[] {
  const out = new Set<string>();
  while (out.size < count) {
    const pin = String(randomInt(100000, 999999));
    // No runs or repeats that a server might refuse as too guessable.
    if (!/(\d)\1{2}/.test(pin) && !/0123|1234|2345|3456|4567|5678|6789/.test(pin)) out.add(pin);
  }
  return [...out];
}

/**
 * Runs SQL against the e2e database on its published port, which is the same whether Docker or a local Postgres
 * (see e2e/stack/run-backend.mjs) is serving it. Uses a local psql when there is one, else the Docker container's.
 */
function runSql(sql: string): void {
  const port = process.env.E2E_DB_PORT ?? '55432';
  const bin = process.env.PG_BIN ?? (process.env.ProgramFiles ? path.join(process.env.ProgramFiles, 'PostgreSQL', '18', 'bin') : '');
  const local = path.join(bin, process.platform === 'win32' ? 'psql.exe' : 'psql');
  const options = { input: sql, stdio: ['pipe', 'ignore', 'inherit'] as ['pipe', 'ignore', 'inherit'] };
  if (existsSync(local)) {
    execFileSync(local, ['-h', '127.0.0.1', '-p', port, '-U', 'purch', '-d', process.env.E2E_DB_NAME ?? 'purch', '-v', 'ON_ERROR_STOP=1'], { ...options, env: { ...process.env, PGPASSWORD: 'purch_e2e_password' } });
  } else {
    execFileSync('docker', ['exec', '-i', 'purch-e2e-db', 'psql', '-U', 'purch', '-d', 'purch', '-v', 'ON_ERROR_STOP=1'], options);
  }
}

/** The e2e database is throwaway. Clearing it lets the seed run again with the same demo accounts. */
function wipeDatabase(): void {
  const sql = `DO $$ DECLARE t text; BEGIN
    FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory' LOOP
      EXECUTE 'TRUNCATE TABLE public.' || quote_ident(t) || ' RESTART IDENTITY CASCADE';
    END LOOP; END $$;`;
  try {
    runSql(sql);
  } catch {
    log('could not clear the database; continuing');
  }
}

export async function seedDemo(): Promise<DemoSeed> {
  wipeDatabase();
  const [adminPin, managerPin, cashierPin, cashier2Pin, warehousePin] = pins(5);
  const domain = 'kapihan.test';
  const account = (key: string, name: string, pin: string) => ({ name, email: `${key}@${domain}`, password: password(), pin });

  const admin = account('nena', 'Nena Villanueva', adminPin);
  log('bootstrap tenant');
  const boot = await call<{ tenantId: string; branchId: string; adminMembershipId: string }>('POST', '/onboarding/bootstrap', {
    body: { tenantName: 'Kapihan ni Aling Nena', businessType: 6, branchName: 'Poblacion', adminName: admin.name, adminPin: admin.pin, adminEmail: admin.email, adminPassword: admin.password },
  });
  const { accessToken } = await signIn(admin.email, admin.password);
  const api = <T = unknown>(method: string, route: string, body?: unknown) => call<T>(method, route, { token: accessToken, body });
  const poblacion = boot.branchId;

  // ---- Business settings -------------------------------------------------------------------------------------------
  log('settings');
  await api('PUT', '/tenant/settings/inventory-tracking', { useSeparateInventoryTracking: true, updateCostOnReceiving: true });
  await api('PUT', '/tenant/settings/credit-ledger', { creditLedgerEnabled: true });
  await api('PUT', '/tenant/settings/barcode', { requiresBarcodePerItem: false, openItemEnabled: true, parkedCartTtlHours: 24 });
  await api('PUT', '/tenant/settings/bir', { tin: '000-123-456-000', registeredBusinessName: 'Kapihan ni Aling Nena Food Services', registeredAddress: '12 Rizal Street, Poblacion, Batangas City', creditLedgerRetentionDays: 365 });
  await api('PUT', '/tenant/settings/branding', { logoUrl: null, backgroundColorHex: '#FBF3E6', accentColorHex: '#8A4B1F', primaryTextColorHex: '#2B1A0E', secondaryTextColorHex: '#6B4E36', fontFamily: null, kioskPosterImageUrl: PHOTO.poster });

  // ---- Branches ----------------------------------------------------------------------------------------------------
  log('branches');
  await api('PUT', `/branches/${poblacion}/details`, { name: 'Poblacion', address: '12 Rizal Street, Poblacion, Batangas City', code: 'POB' });
  await api('PUT', `/branches/${poblacion}/hardware-settings`, { receiptPrinterProfile: 1, cashDrawerEnabled: true, cashDrawerPolicy: 1 });
  await api('PUT', `/branches/${poblacion}/manual-gcash-qr`, { qrImageUrl: PHOTO.gcash, accountName: 'Kapihan ni Aling Nena', accountNumber: '09170000101' });
  const riversideBranch = await api<Created>('POST', '/branches', { name: 'Riverside', address: '88 Ilog Road, Riverside, Lipa City' });
  const riverside = riversideBranch.id;
  await api('PUT', `/branches/${riverside}/details`, { name: 'Riverside', address: '88 Ilog Road, Riverside, Lipa City', code: 'RIV' });
  await api('PUT', `/branches/${riverside}/hardware-settings`, { receiptPrinterProfile: 1, cashDrawerEnabled: true, cashDrawerPolicy: 0 });
  await api('PUT', `/branches/${riverside}/manual-gcash-qr`, { qrImageUrl: PHOTO.gcash, accountName: 'Kapihan ni Aling Nena Riverside', accountNumber: '09170000102' });

  // ---- Staff: invited, then each opens their link (role 1 Manager, 2 Staff; duties 1 Cashier, 2 Warehouse) -------------
  log('staff');
  const people = [
    { key: 'manager', ...account('ramon', 'Ramon Bautista', managerPin), role: 1, duties: 0, branchIds: [] as string[] },
    { key: 'cashier', ...account('liza', 'Liza Mendoza', cashierPin), role: 2, duties: 1, branchIds: [poblacion] },
    { key: 'cashier2', ...account('joel', 'Joel Reyes', cashier2Pin), role: 2, duties: 1, branchIds: [riverside] },
    { key: 'warehouse', ...account('mario', 'Mario Santos', warehousePin), role: 2, duties: 2, branchIds: [poblacion, riverside] },
  ];
  for (const person of people) {
    const link = await api<{ token: string }>('POST', '/staff/invites', { name: person.name, email: person.email, role: person.role, duties: person.duties, branchIds: person.branchIds });
    await call('POST', '/enrol/redeem', { body: { token: link.token, password: person.password, pin: person.pin } });
  }
  const pendingInvite = { name: 'Grace Ocampo', email: `grace@${domain}`, token: '' };
  pendingInvite.token = (await api<{ token: string }>('POST', '/staff/invites', { name: pendingInvite.name, email: pendingInvite.email, role: 2, duties: 1, branchIds: [riverside] })).token;
  const members = await api<{ id: string; email: string }[]>('GET', '/staff/members');
  const memberId = (email: string) => members.find((m) => m.email === email)!.id;
  const person = (p: { name: string; email: string; password: string; pin: string }): DemoPerson => ({ ...p, membershipId: memberId(p.email) });
  const byKey = Object.fromEntries(people.map((p) => [p.key, person(p)]));

  // ---- Devices -----------------------------------------------------------------------------------------------------
  log('devices');
  const pair = async (deviceType: number, name: string, branchId: string, linked: string | null = null): Promise<DemoDevice> => {
    const request = await api<{ device: { id: string }; pairingCode: string }>('POST', '/devices/pairing-requests', { name, deviceType, branchId, linkedRegisterDeviceId: linked });
    const paired = await call<{ deviceCredential: string }>('POST', '/devices/pair', { body: { pairingCode: request.pairingCode } });
    return { id: request.device.id, credential: paired.deviceCredential, name };
  };
  const register = await pair(0, 'Counter 1', poblacion);
  const register2 = await pair(0, 'Counter 1 Riverside', riverside);
  const devices = {
    register,
    register2,
    // A till nobody has opened a shift on yet.
    register3: await pair(0, 'Counter 2', poblacion),
    kiosk: await pair(1, 'Kiosk sa Pinto', poblacion),
    kitchen: await pair(3, 'Kusina', poblacion),
    orderBoard: await pair(2, 'Order Board', poblacion),
    customerDisplay: await pair(5, 'Customer Display 1', poblacion, register.id),
    warehouse: await pair(4, 'Bodega', poblacion),
  };
  const pending = await api<{ pairingCode: string }>('POST', '/devices/pairing-requests', { name: 'Kiosk Riverside', deviceType: 1, branchId: riverside, linkedRegisterDeviceId: null });

  // ---- Catalogue ---------------------------------------------------------------------------------------------------
  log('categories');
  const categoryNames = ['Kape', 'Meryenda', 'Ulam', 'Inumin', 'Grocery', 'Sari-sari'];
  const categories: Record<string, string> = {};
  for (const [index, name] of categoryNames.entries()) categories[name] = (await api<Created>('POST', '/categories', { name, sortOrder: index + 1, imageUrl: tile(name, name) })).id;

  interface ItemSpec {
    name: string;
    cat: string;
    price: number;
    cost: number;
    unit?: string;
    pricingType?: number;
    /** Stock in per branch; omitted for items made from ingredients. */
    stock?: number;
    barcode?: boolean;
  }
  const specs: ItemSpec[] = [
    { name: 'Kapeng Barako', cat: 'Kape', price: 85, cost: 24 },
    { name: 'Iced Americano', cat: 'Kape', price: 110, cost: 28 },
    { name: 'Caffe Latte', cat: 'Kape', price: 130, cost: 38, pricingType: 5 },
    { name: 'Spanish Latte', cat: 'Kape', price: 150, cost: 46 },
    { name: 'Iced Mocha', cat: 'Kape', price: 155, cost: 48 },
    { name: 'Ube Latte', cat: 'Kape', price: 160, cost: 52 },
    { name: 'Salted Caramel Macchiato', cat: 'Kape', price: 165, cost: 54 },
    { name: 'Kape 3-in-1 (pack of 10)', cat: 'Kape', price: 95, cost: 62, stock: 40, barcode: true, unit: 'pack' },
    { name: 'Pandesal (6 pcs)', cat: 'Meryenda', price: 36, cost: 16, stock: 60, barcode: true },
    { name: 'Ensaymada', cat: 'Meryenda', price: 55, cost: 24, stock: 40, barcode: true },
    { name: 'Ube Cheese Pandesal', cat: 'Meryenda', price: 45, cost: 19, stock: 40, barcode: true },
    { name: 'Turon', cat: 'Meryenda', price: 25, cost: 10, stock: 50, barcode: true },
    { name: 'Banana Cue', cat: 'Meryenda', price: 20, cost: 8, stock: 50, barcode: true },
    { name: 'Puto Cuchinta', cat: 'Meryenda', price: 35, cost: 13, stock: 40, barcode: true },
    { name: 'Bibingka', cat: 'Meryenda', price: 70, cost: 28, stock: 30, barcode: true },
    { name: 'Siopao Asado', cat: 'Meryenda', price: 55, cost: 26, stock: 30, barcode: true },
    { name: 'Tapsilog', cat: 'Ulam', price: 145, cost: 62 },
    { name: 'Longsilog', cat: 'Ulam', price: 135, cost: 55 },
    { name: 'Chicken Adobo Rice', cat: 'Ulam', price: 130, cost: 52 },
    { name: 'Sinigang na Baboy', cat: 'Ulam', price: 160, cost: 70, stock: 25 },
    { name: 'Pancit Canton', cat: 'Ulam', price: 90, cost: 34, stock: 30 },
    { name: 'Lumpiang Shanghai (6 pcs)', cat: 'Ulam', price: 85, cost: 35, stock: 30 },
    { name: 'Extra Rice', cat: 'Ulam', price: 25, cost: 8 },
    { name: 'Almusal Combo', cat: 'Ulam', price: 175, cost: 0, pricingType: 4 },
    { name: 'Buko Juice', cat: 'Inumin', price: 45, cost: 18, stock: 60 },
    { name: "Sago't Gulaman", cat: 'Inumin', price: 40, cost: 12, stock: 60 },
    { name: 'Bottled Water 500ml', cat: 'Inumin', price: 20, cost: 9, stock: 120, barcode: true },
    { name: 'Calamansi Juice', cat: 'Inumin', price: 50, cost: 14, stock: 60 },
    { name: 'Soft Drink 330ml', cat: 'Inumin', price: 40, cost: 24, stock: 96, barcode: true },
    { name: 'Jasmine Rice (per kg)', cat: 'Grocery', price: 58, cost: 46, unit: 'kg', pricingType: 1, stock: 150 },
    { name: 'White Sugar (per kg)', cat: 'Grocery', price: 72, cost: 60, unit: 'kg', pricingType: 1, stock: 80 },
    { name: 'Itlog (1 pc)', cat: 'Grocery', price: 9, cost: 7, stock: 240, barcode: true },
    { name: 'Canned Sardines 155g', cat: 'Grocery', price: 28, cost: 21, stock: 100, barcode: true },
    { name: 'Instant Noodles', cat: 'Grocery', price: 15, cost: 11, stock: 150, barcode: true },
    { name: 'Cooking Oil 1L', cat: 'Grocery', price: 95, cost: 78, stock: 48, barcode: true },
    { name: 'Toyo 350ml', cat: 'Grocery', price: 32, cost: 24, stock: 60, barcode: true },
    { name: 'Iodized Salt 500g', cat: 'Grocery', price: 20, cost: 13, stock: 60, barcode: true },
    { name: 'Shampoo Sachet', cat: 'Sari-sari', price: 8, cost: 5.5, stock: 300, barcode: true },
    { name: 'Laundry Powder Sachet', cat: 'Sari-sari', price: 9, cost: 6, stock: 200, barcode: true },
    { name: 'Candy (1 pc)', cat: 'Sari-sari', price: 1, cost: 0.6, stock: 800 },
    { name: 'Chewing Gum', cat: 'Sari-sari', price: 5, cost: 3.2, stock: 200, barcode: true },
    { name: 'Mixed Nuts 40g', cat: 'Sari-sari', price: 25, cost: 17, stock: 90, barcode: true },
    { name: 'Toothpaste Sachet', cat: 'Sari-sari', price: 12, cost: 8, stock: 150, barcode: true },
  ];

  const itemImage = (spec: ItemSpec): string => (spec.cat === 'Kape' && spec.price >= 100 ? PHOTO.drink : spec.cat === 'Ulam' && spec.price >= 125 ? PHOTO.meal : tile(spec.name, spec.cat));
  log(`items (${specs.length})`);
  const items: DemoSeed['items'] = {};
  let seq = 1;
  for (const [index, spec] of specs.entries()) {
    const barcode = spec.barcode ? `48070${String(1000 + index).padStart(5, '0')}` : null;
    const created = await api<Created>('POST', '/items', {
      name: spec.name,
      sku: `${spec.cat.slice(0, 3).toUpperCase()}-${String(seq++).padStart(3, '0')}`,
      barcode,
      categoryId: categories[spec.cat],
      basePrice: spec.price,
      imageUrl: itemImage(spec),
      pricingType: spec.pricingType ?? 0,
      sortOrder: index + 1,
      costPrice: spec.cost > 0 ? spec.cost : null,
      unit: spec.unit ?? null,
    });
    items[spec.name] = { id: created.id, price: spec.price, barcode, categoryId: categories[spec.cat] };
    if (spec.stock) {
      for (const [branchId, share] of [[poblacion, 1], [riverside, 0.6]] as const) {
        await api('POST', '/inventory/movements', { itemId: created.id, branchId, type: 0, quantity: Math.round(spec.stock * share), note: 'Opening stock', reasonCategory: null, photoUrl: null, supplierReference: null });
      }
    }
  }
  const item = (name: string) => items[name].id;

  // Variants: the latte comes in three sizes.
  log('variants, tingi, combo, modifiers');
  for (const [size, price, suffix] of [['Small 8oz', 110, 'S'], ['Regular 12oz', 130, 'R'], ['Large 16oz', 150, 'L']] as const) {
    await api('POST', `/items/${item('Caffe Latte')}/variants`, { attributes: { Size: size }, sku: `KAPE-LATTE-${suffix}`, priceOverride: price, imageUrl: null });
  }
  // Tingi: rice comes in a 25 kg sack but is sold by the quarter kilo.
  await api('PUT', `/items/${item('Jasmine Rice (per kg)')}/tingi-config`, { tingiMode: 2, packagedSize: 25, tingiIncrementStep: 0.25, allowedSizes: [] });
  await api('PUT', `/items/${item('Shampoo Sachet')}/low-stock-threshold`, { threshold: 150 });
  await api('PUT', `/items/${item('Toothpaste Sachet')}/low-stock-threshold`, { threshold: 160 });
  await api('PUT', `/items/${item('Mixed Nuts 40g')}/low-stock-threshold`, { threshold: 60 });

  // Combo meal: a silog and a drink.
  await api('POST', `/items/${item('Almusal Combo')}/combo-components`, { componentCategoryId: categories['Ulam'], slotLabel: 'Silog of your choice', quantity: 1, substitutionUpchargeAmount: null, choiceUpcharges: [{ itemId: item('Tapsilog'), amount: 0 }, { itemId: item('Longsilog'), amount: 0 }, { itemId: item('Sinigang na Baboy'), amount: 25 }] });
  await api('POST', `/items/${item('Almusal Combo')}/combo-components`, { componentCategoryId: categories['Kape'], slotLabel: 'Hot coffee', quantity: 1, substitutionUpchargeAmount: null, componentItemId: item('Kapeng Barako') });

  // Modifier groups.
  const sugar = await api<Created>('POST', '/modifier-groups', { name: 'Sugar level', allowMultipleSelection: false, isRequired: true });
  for (const name of ['0% (no sugar)', '25%', '50%', '75%', '100%']) await api('POST', `/modifier-groups/${sugar.id}/modifiers`, { name, priceDelta: 0 });
  const addons = await api<Created>('POST', '/modifier-groups', { name: 'Add-ons', allowMultipleSelection: true, isRequired: false });
  for (const [name, delta] of [['Extra shot', 40], ['Oat milk', 50], ['Whipped cream', 25], ['Sago pearls', 15]] as const) await api('POST', `/modifier-groups/${addons.id}/modifiers`, { name, priceDelta: delta });
  const rice = await api<Created>('POST', '/modifier-groups', { name: 'Ulam extras', allowMultipleSelection: true, isRequired: false });
  for (const [name, delta] of [['Extra egg', 15], ['Garlic rice upgrade', 20], ['Atsara', 10]] as const) await api('POST', `/modifier-groups/${rice.id}/modifiers`, { name, priceDelta: delta });
  const drinks = ['Kapeng Barako', 'Iced Americano', 'Caffe Latte', 'Spanish Latte', 'Iced Mocha', 'Ube Latte', 'Salted Caramel Macchiato'];
  await api('POST', `/modifier-groups/${sugar.id}/attach-items`, { itemIds: drinks.map(item) });
  await api('POST', `/modifier-groups/${addons.id}/attach-items`, { itemIds: ['Iced Americano', 'Caffe Latte', 'Spanish Latte', 'Iced Mocha', 'Ube Latte', 'Salted Caramel Macchiato'].map(item) });
  await api('POST', `/modifier-groups/${rice.id}/attach-items`, { itemIds: ['Tapsilog', 'Longsilog', 'Chicken Adobo Rice'].map(item) });

  const groups = await api<{ id: string; modifiers: { id: string; name: string }[] }[]>('GET', '/modifier-groups');
  const sugarIds = groups.find((g) => g.id === sugar.id)!.modifiers.map((m) => m.id);
  const addonIds = groups.find((g) => g.id === addons.id)!.modifiers.map((m) => m.id);
  const extraIds = groups.find((g) => g.id === rice.id)!.modifiers.map((m) => m.id);
  const latteVariants = await api<{ id: string }[]>('GET', `/items/${item('Caffe Latte')}/variants`);

  // ---- Ingredients, recipes ----------------------------------------------------------------------------------------
  log('ingredients and recipes');
  const invCat = await api<Created>('POST', '/inventory-categories', { name: 'Kitchen', sortOrder: 1 });
  const invCat2 = await api<Created>('POST', '/inventory-categories', { name: 'Coffee bar', sortOrder: 2 });
  const ingredientSpecs = [
    { name: 'Kapeng Barako beans', base: 'g', pack: 'kg', size: 1000, cost: 0.65, low: 3000, cat: invCat2.id, stock: 18000 },
    { name: 'Fresh milk', base: 'ml', pack: 'carton (1L)', size: 1000, cost: 0.075, low: 6000, cat: invCat2.id, stock: 24000 },
    { name: 'Asukal (brown sugar)', base: 'g', pack: 'kg', size: 1000, cost: 0.06, low: 2000, cat: invCat2.id, stock: 12000 },
    { name: 'Ube halaya', base: 'g', pack: 'tub (500g)', size: 500, cost: 0.2, low: 1500, cat: invCat2.id, stock: 4000 },
    { name: 'Caramel syrup', base: 'ml', pack: 'bottle (750ml)', size: 750, cost: 0.28, low: 1500, cat: invCat2.id, stock: 6000 },
    { name: 'Bigas (cooked rice supply)', base: 'g', pack: 'sack (25kg)', size: 25000, cost: 0.046, low: 10000, cat: invCat.id, stock: 75000 },
    { name: 'Itlog (kitchen)', base: 'pc', pack: 'tray (30)', size: 30, cost: 7, low: 60, cat: invCat.id, stock: 180 },
    { name: 'Beef tapa', base: 'g', pack: 'pack (500g)', size: 500, cost: 0.42, low: 3000, cat: invCat.id, stock: 9000 },
    { name: 'Longganisa', base: 'pc', pack: 'pack (12)', size: 12, cost: 14, low: 36, cat: invCat.id, stock: 120 },
    { name: 'Chicken adobo mix', base: 'g', pack: 'tray (2kg)', size: 2000, cost: 0.2, low: 4000, cat: invCat.id, stock: 7000 },
    { name: 'Cooking oil (kitchen)', base: 'ml', pack: 'jug (5L)', size: 5000, cost: 0.08, low: 3000, cat: invCat.id, stock: 15000 },
  ];
  const ing: Record<string, string> = {};
  for (const spec of ingredientSpecs) {
    const created = await api<Created>('POST', '/inventory-items', { name: spec.name, sku: null, baseUnit: spec.base, packagingUnit: spec.pack, packagingSize: spec.size, lowStockThreshold: spec.low, categoryId: spec.cat, unitCost: spec.cost });
    ing[spec.name] = created.id;
    for (const [branchId, share] of [[poblacion, 1], [riverside, 0.5]] as const) {
      await api('POST', '/inventory/movements', { itemId: null, inventoryItemId: created.id, branchId, type: 0, quantity: Math.round(spec.stock * share), note: 'Opening count', reasonCategory: null, photoUrl: null, supplierReference: null });
    }
  }
  const recipe = (name: string, lines: [string, number][]) =>
    api('PUT', `/items/${item(name)}/recipe`, { lines: lines.map(([ingredient, qty]) => ({ inventoryItemId: ing[ingredient], quantityPerOrder: qty })) });
  await recipe('Kapeng Barako', [['Kapeng Barako beans', 18], ['Asukal (brown sugar)', 8]]);
  await recipe('Iced Americano', [['Kapeng Barako beans', 18]]);
  await recipe('Caffe Latte', [['Kapeng Barako beans', 18], ['Fresh milk', 180]]);
  await recipe('Spanish Latte', [['Kapeng Barako beans', 18], ['Fresh milk', 160], ['Asukal (brown sugar)', 15]]);
  await recipe('Iced Mocha', [['Kapeng Barako beans', 18], ['Fresh milk', 160], ['Caramel syrup', 20]]);
  await recipe('Ube Latte', [['Kapeng Barako beans', 18], ['Fresh milk', 160], ['Ube halaya', 40]]);
  await recipe('Salted Caramel Macchiato', [['Kapeng Barako beans', 18], ['Fresh milk', 150], ['Caramel syrup', 30]]);
  await recipe('Tapsilog', [['Beef tapa', 100], ['Bigas (cooked rice supply)', 180], ['Itlog (kitchen)', 1], ['Cooking oil (kitchen)', 15]]);
  await recipe('Longsilog', [['Longganisa', 3], ['Bigas (cooked rice supply)', 180], ['Itlog (kitchen)', 1], ['Cooking oil (kitchen)', 15]]);
  await recipe('Chicken Adobo Rice', [['Chicken adobo mix', 150], ['Bigas (cooked rice supply)', 180]]);
  await recipe('Extra Rice', [['Bigas (cooked rice supply)', 150]]);

  // ---- Suppliers, purchasing ---------------------------------------------------------------------------------------
  log('suppliers and purchasing');
  const supplierSpecs = [
    { name: 'Batangas Barako Growers Coop', specialization: 'Coffee beans', address: 'Lipa City, Batangas', contactPerson: 'Aling Fely Magsino', number: '09170000201' },
    { name: 'Dairy Pilipinas Distribution', specialization: 'Milk and dairy', address: 'Calamba, Laguna', contactPerson: 'Ginoong Dante Cruz', number: '09170000202' },
    { name: 'Palengke Wholesale Trading', specialization: 'Grocery and dry goods', address: 'Batangas Public Market', contactPerson: 'Aling Cora Lim', number: '09170000203' },
  ];
  const suppliers: string[] = [];
  for (const s of supplierSpecs) {
    const created = await api<Created>('POST', '/suppliers', { name: s.name, specialization: s.specialization, address: s.address, tin: null, remarks: null, contacts: [{ contactPerson: s.contactPerson, modes: ['Call', 'Viber'], numbers: [s.number], emails: [] }], paymentAccountName: s.name, paymentAccountNumber: `GCash ${s.number}` });
    suppliers.push(created.id);
  }
  await api('PUT', `/inventory-items/${ing['Kapeng Barako beans']}/suppliers`, { suppliers: [{ supplierId: suppliers[0], isPreferred: true, unitCost: 0.65 }] });
  await api('PUT', `/inventory-items/${ing['Fresh milk']}/suppliers`, { suppliers: [{ supplierId: suppliers[1], isPreferred: true, unitCost: 0.075 }] });
  for (const name of ['Cooking Oil 1L', 'Canned Sardines 155g', 'Instant Noodles']) {
    await api('PUT', `/items/${item(name)}/suppliers`, { suppliers: [{ supplierId: suppliers[2], isPreferred: true, unitCost: specs.find((s) => s.name === name)!.cost }] });
  }

  const poLine = (name: string, qty: number) => ({ itemId: item(name), quantityOrdered: qty, expectedUnitCost: specs.find((s) => s.name === name)!.cost, unit: 'pc' });
  const poDraft = await api<{ id: string }>('POST', '/purchase-orders', { supplierId: suppliers[2], branchId: poblacion, lines: [poLine('Cooking Oil 1L', 24), poLine('Instant Noodles', 120), poLine('Canned Sardines 155g', 60)], vatInclusive: true, discountAmount: 0, remarks: 'Weekly grocery restock', dateNeeded: null });
  const poSent = await api<{ id: string }>('POST', '/purchase-orders', { supplierId: suppliers[2], branchId: riverside, lines: [poLine('Toyo 350ml', 36), poLine('Iodized Salt 500g', 48), poLine('Mixed Nuts 40g', 30)], vatInclusive: true, discountAmount: 50, remarks: 'For Riverside sari-sari shelf', dateNeeded: null });
  await api('POST', `/purchase-orders/${poSent.id}/mark-sent`);
  const poReceivedDraft = await api<{ id: string; lines: { itemId: string; quantityOrdered: number; expectedUnitCost: number }[] }>('POST', '/purchase-orders', { supplierId: suppliers[2], branchId: poblacion, lines: [poLine('Soft Drink 330ml', 48), poLine('Bottled Water 500ml', 72)], vatInclusive: true, discountAmount: 0, remarks: null, dateNeeded: null });
  await api('POST', `/purchase-orders/${poReceivedDraft.id}/mark-sent`);
  const receivedAt = new Date().toISOString().slice(0, 10);
  await api('POST', '/incoming-receiving', {
    purchaseOrderId: poReceivedDraft.id,
    supplierId: suppliers[2],
    branchId: poblacion,
    deliveryDate: receivedAt,
    remarks: 'Complete, in good order',
    lines: poReceivedDraft.lines.map((line) => ({ itemId: line.itemId, quantityReceived: line.quantityOrdered, uom: 'pc', unitPrice: line.expectedUnitCost, condition: 0, remark: 0 })),
  });
  // A receipt that arrived with no order.
  await api('POST', '/incoming-receiving', {
    purchaseOrderId: null,
    supplierId: suppliers[1],
    branchId: poblacion,
    deliveryDate: receivedAt,
    remarks: 'Walk-in delivery, one carton dented',
    lines: [
      { itemId: item('Pandesal (6 pcs)'), inventoryItemId: null, quantityReceived: 20, uom: 'pack', unitPrice: 16, condition: 0, remark: 0 },
      { itemId: item('Ensaymada'), inventoryItemId: null, quantityReceived: 4, uom: 'pc', unitPrice: 24, condition: 1, remark: 1 },
    ],
  });
  const request = await api<{ id: string }>('POST', '/purchase-requests', { branchId: riverside, dateNeeded: null, remarks: 'Running low before the weekend', lines: [{ itemId: item('Shampoo Sachet'), inventoryItemId: null, quantity: 200, unit: 'pc' }, { itemId: item('Toothpaste Sachet'), inventoryItemId: null, quantity: 120, unit: 'pc' }, { itemId: null, inventoryItemId: ing['Fresh milk'], quantity: 24, unit: 'carton (1L)' }] });
  await api('POST', `/purchase-requests/${request.id}/approve`);
  await api('POST', '/purchase-requests', { branchId: poblacion, dateNeeded: null, remarks: null, lines: [{ itemId: null, inventoryItemId: ing['Kapeng Barako beans'], quantity: 10, unit: 'kg' }] });

  // ---- Transfer, stock takes ---------------------------------------------------------------------------------------
  log('transfer and stock takes');
  const transfer = await api<{ id: string }>('POST', '/branch-transfers', { sourceBranchId: poblacion, destinationBranchId: riverside, lines: [{ itemId: item('Bottled Water 500ml'), inventoryItemId: null, quantity: 24 }, { itemId: null, inventoryItemId: ing['Fresh milk'], quantity: 6000 }] });
  await api('POST', `/branch-transfers/${transfer.id}/mark-in-transit`);
  await api('POST', '/branch-transfers', { sourceBranchId: poblacion, destinationBranchId: riverside, lines: [{ itemId: item('Canned Sardines 155g'), inventoryItemId: null, quantity: 24 }] });

  // ---- Promotions --------------------------------------------------------------------------------------------------
  log('promotions');
  const none = { startsAt: null, endsAt: null };
  await api('POST', '/promos/bogo', { name: 'Buy 2 Turon, get 1 free', triggerItemId: item('Turon'), triggerQuantity: 2, freeItemId: item('Turon'), freeQuantity: 1, ...none });
  await api('POST', '/promos/combos', { name: 'Kape at Ensaymada', itemAId: item('Kapeng Barako'), itemBId: item('Ensaymada'), comboPrice: 120, ...none });
  await api('POST', '/promos/item-discounts', { name: 'Pandesal Promo 10% off', itemId: item('Pandesal (6 pcs)'), discountType: 0, discountValue: 10, ...none });
  await api('POST', '/promo-codes', { code: 'KAPE20', discountType: 0, discountValue: 20, expiresAt: null });
  await api('POST', '/promo-codes', { code: 'ALINGNENA50', discountType: 1, discountValue: 50, expiresAt: null });

  // ---- Utang customers ---------------------------------------------------------------------------------------------
  log('utang customers');
  const customerSpecs = [
    { name: 'Aling Cora Dizon', phone: '09170000301', address: 'Purok 3, Poblacion', limit: 2000 },
    { name: 'Mang Berto Alcantara', phone: '09170000302', address: 'Rizal St., Poblacion', limit: 3000 },
    { name: 'Ate Jocelyn Ramos', phone: '09170000303', address: 'Mabini Ext., Poblacion', limit: 1500 },
    { name: 'Kuya Dennis Pascual', phone: '09170000304', address: 'Riverside Subd.', limit: 1000 },
    { name: 'Aling Marites Gonzales', phone: '09170000305', address: 'Ilog Road, Riverside', limit: 2500 },
    { name: 'Mang Tonyo Salazar', phone: '09170000306', address: 'Purok 1, Riverside', limit: 800 },
    { name: 'Ate Rowena Cabrera', phone: '09170000307', address: 'Sampaguita St., Poblacion', limit: 1200 },
  ];
  const customers: Record<string, string> = {};
  const due = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);
  for (const [index, c] of customerSpecs.entries()) {
    const created = await api<Created>('POST', '/credit-ledger', { customerFullName: c.name, customerPhoneNumber: c.phone, customerAddress: c.address, creditLimit: c.limit, dueDate: due([5, 12, -3, 20, 2, -8, 30][index]) });
    customers[c.name] = created.id;
  }

  // ---- Selling -----------------------------------------------------------------------------------------------------
  log('shifts and sales');
  const sellers = [
    { person: byKey.cashier, device: register, branchId: poblacion },
    { person: byKey.cashier2, device: register2, branchId: riverside },
  ];
  const sessions: Record<string, Tokens> = {};
  for (const seller of sellers) {
    sessions[seller.device.id] = await unlock(seller.device.credential, seller.person.membershipId, seller.person.pin);
    await call('POST', '/shifts/open', { token: sessions[seller.device.id].accessToken, body: { openingCashAmount: 3000 } });
  }
  const bossToken = (await unlock(register.credential, byKey.manager.membershipId, byKey.manager.pin)).accessToken;

  interface Cart {
    id: string;
    totalAmount: number;
    lines: { id: string }[];
  }
  const saleIds: string[] = [];
  const cafePool = ['Kapeng Barako', 'Kapeng Barako', 'Caffe Latte', 'Caffe Latte', 'Iced Americano', 'Spanish Latte', 'Spanish Latte', 'Iced Mocha', 'Ube Latte', 'Salted Caramel Macchiato', 'Pandesal (6 pcs)', 'Ensaymada', 'Ube Cheese Pandesal', 'Turon', 'Turon', 'Banana Cue', 'Puto Cuchinta', 'Bibingka', 'Siopao Asado', 'Tapsilog', 'Tapsilog', 'Longsilog', 'Chicken Adobo Rice', 'Sinigang na Baboy', 'Pancit Canton', 'Lumpiang Shanghai (6 pcs)', 'Extra Rice', 'Buko Juice', "Sago't Gulaman", 'Bottled Water 500ml', 'Calamansi Juice', 'Soft Drink 330ml'];
  const groceryPool = ['Jasmine Rice (per kg)', 'White Sugar (per kg)', 'Itlog (1 pc)', 'Canned Sardines 155g', 'Instant Noodles', 'Cooking Oil 1L', 'Toyo 350ml', 'Iodized Salt 500g', 'Shampoo Sachet', 'Laundry Powder Sachet', 'Candy (1 pc)', 'Chewing Gum', 'Mixed Nuts 40g', 'Toothpaste Sachet'];
  const customerIds = Object.values(customers);

  /** Rings up and pays one sale. Returns the completed sale's id. */
  async function sale(seller: (typeof sellers)[number], opts: { tender: 'cash' | 'gcash' | 'bank' | 'utang' | 'split'; discount?: 'line' | 'cart'; promo?: string; customer?: string }): Promise<{ id: string; total: number; receiptNumber: number | null }> {
    const token = sessions[seller.device.id].accessToken;
    const run = <T>(method: string, route: string, body?: unknown) => call<T>(method, route, { token, body });
    await run('GET', '/transactions/cart');
    const grocery = rand() < 0.3;
    const pool = grocery ? groceryPool : cafePool;
    const lines: { itemId: string; itemVariantId: string | null; quantity: number; selectedModifierIds?: string[] }[] = [];
    const picked = new Set<string>();
    for (let i = between(1, grocery ? 4 : 3); i > 0; i--) {
      const name = pick(pool);
      if (picked.has(name)) continue;
      picked.add(name);
      const weight = name.includes('per kg');
      const quantity = weight ? pick([0.5, 1, 1.5, 2]) : name.includes('Sachet') || name.includes('Candy') || name.includes('Gum') ? between(2, 6) : between(1, 2);
      const selectedModifierIds: string[] = [];
      if (drinks.includes(name)) {
        selectedModifierIds.push(pick(sugarIds));
        if (name !== 'Kapeng Barako' && rand() < 0.3) selectedModifierIds.push(pick(addonIds));
      } else if (['Tapsilog', 'Longsilog', 'Chicken Adobo Rice'].includes(name) && rand() < 0.3) selectedModifierIds.push(pick(extraIds));
      lines.push({ itemId: item(name), itemVariantId: name === 'Caffe Latte' ? pick(latteVariants).id : null, quantity, selectedModifierIds });
    }
    let cart = await run<Cart>('POST', '/transactions/cart/lines/batch', { batchId: crypto.randomUUID(), lines });
    if (opts.discount === 'line') cart = await run<Cart>('PUT', `/transactions/cart/lines/${cart.lines[0].id}/discount`, { kind: 'Percent', value: 10, reason: 'Suki discount', approverPin: byKey.manager.pin });
    if (opts.discount === 'cart') cart = await run<Cart>('PUT', '/transactions/cart/discount', { kind: 'Fixed', value: 20, reason: 'Birthday treat', approverPin: byKey.manager.pin });
    if (opts.promo) cart = await run<Cart>('PUT', '/transactions/cart/promo-code', { code: opts.promo });
    const total = cart.totalAmount;
    const paymentKey = crypto.randomUUID();
    let paid: { id: string; totalAmount: number; receiptNumber: number | null };
    const tendered = Math.ceil(total / 50) * 50 + (rand() < 0.3 ? 50 : 0);
    switch (opts.tender) {
      case 'cash':
        paid = await run('POST', '/transactions/cart/payments', { method: 0, amountTendered: tendered, expectedTotal: total, paymentKey });
        break;
      case 'gcash':
        paid = await run('POST', '/transactions/cart/payments', { method: 3, amountTendered: total, expectedTotal: total, paymentKey });
        break;
      case 'bank':
        paid = await run('POST', '/transactions/cart/payments', { method: 2, amountTendered: total, expectedTotal: total, paymentKey });
        break;
      case 'utang':
        paid = await run('POST', '/transactions/cart/payments', { method: 5, amountTendered: null, customerCreditLedgerId: opts.customer, expectedTotal: total, paymentKey });
        break;
      case 'split': {
        const half = Math.floor(total / 2);
        paid = await run('POST', '/transactions/cart/payments', {
          method: 6,
          amountTendered: null,
          expectedTotal: total,
          paymentKey,
          parts: [
            { method: 0, amount: half, amountTendered: Math.ceil(half / 50) * 50 },
            { method: 3, amount: Math.round((total - half) * 100) / 100 },
          ],
        });
        break;
      }
    }
    return { id: paid.id, total, receiptNumber: paid.receiptNumber };
  }

  // Today's rush plus two weeks of trade. Weekends are busier; mornings and the merienda hour peak.
  const salePlan: { dayOffset: number; hour: number; minute: number; id: string; branchId: string; tender: string; receiptNumber: number | null }[] = [];
  const utangBalance = new Map<string, number>();
  let utangRotation = 0;
  const discountsUsed = new Map<string, number>();
  for (let day = 13; day >= 0; day--) {
    const date = new Date(Date.now() - day * 86_400_000);
    const weekend = [0, 6].includes(date.getDay());
    const count = (weekend ? 11 : 8) + between(0, 3);
    for (let n = 0; n < count; n++) {
      const seller = rand() < 0.62 ? sellers[0] : sellers[1];
      const roll = rand();
      const tender = roll < 0.48 ? 'cash' : roll < 0.7 ? 'gcash' : roll < 0.76 ? 'bank' : roll < 0.88 ? 'utang' : 'split';
      let customer: string | undefined;
      if (tender === 'utang') {
        customer = customerIds[utangRotation++ % customerIds.length];
      }
      // Approver-gated calls are limited to 10 per 15 minutes per signed-in person, so only a few sales carry one.
      const wants = rand() < 0.1 ? 'line' : rand() < 0.07 ? 'cart' : undefined;
      const used = discountsUsed.get(seller.device.id) ?? 0;
      const discount = wants && used < 3 ? wants : undefined;
      if (discount) discountsUsed.set(seller.device.id, used + 1);
      try {
        const result = await sale(seller, { tender, discount, customer, promo: rand() < 0.05 ? 'KAPE20' : undefined });
        if (tender === 'utang' && customer) utangBalance.set(customer, (utangBalance.get(customer) ?? 0) + result.total);
        const hour = pick([7, 8, 8, 9, 9, 10, 11, 12, 12, 13, 14, 15, 15, 16, 16, 17, 18, 19]);
        salePlan.push({ dayOffset: day, hour, minute: between(0, 59), id: result.id, branchId: seller.branchId, tender, receiptNumber: result.receiptNumber });
        saleIds.push(result.id);
      } catch (error) {
        // An utang sale past a customer's limit is refused: that is the product working. Move on.
        log(`sale skipped: ${(error as Error).message.slice(0, 420)}`);
        const token = sessions[seller.device.id].accessToken;
        const leftover = await call<Cart>('GET', '/transactions/cart', { token });
        for (const line of leftover.lines) await call('DELETE', `/transactions/cart/lines/${line.id}`, { token });
      }
    }
  }
  log(`${saleIds.length} sales`);

  // A refund, a void, and a customer part-paying their tab.
  const refundTarget = salePlan.find((s) => s.dayOffset === 2 && s.branchId === poblacion && s.tender === 'cash')!;
  await call('POST', `/transactions/${refundTarget.id}/refund`, { token: bossToken, body: { reason: 'Customer returned the wrong order', approverPin: admin.pin } });
  // Carts the cashier started and then had a manager cancel: today's, and two from earlier in the fortnight.
  const voidToken = sessions[register.id].accessToken;
  const voids: { id: string; dayOffset: number; hour: number }[] = [];
  for (const [dayOffset, hour, name, qty] of [[0, 15, 'Tapsilog', 2], [4, 10, 'Sinigang na Baboy', 1], [9, 13, 'Spanish Latte', 2]] as const) {
    await call('GET', '/transactions/cart', { token: voidToken });
    const cart = await call<{ id: string }>('POST', '/transactions/cart/lines', { token: voidToken, body: { itemId: item(name), itemVariantId: null, quantity: qty, selectedModifierIds: drinks.includes(name) ? [sugarIds[1]] : [] } });
    await call('POST', '/transactions/cart/void', { token: voidToken, body: { approverPin: byKey.manager.pin } });
    if (dayOffset > 0) voids.push({ id: cart.id, dayOffset, hour });
  }
  for (const [id, balance] of utangBalance) {
    if (balance > 400) await call('POST', `/credit-ledger/${id}/payments`, { token: sessions[register.id].accessToken, body: { amount: Math.round(balance * 0.3), note: 'Partial payment' } });
  }

  // Some things run low or out, so the dashboards have something to say.
  const live = await api<{ id: string; countOnHand: number }[]>('GET', '/items');
  const stockOf = (name: string) => live.find((i) => i.id === item(name))!.countOnHand;
  const manual = (itemName: string, type: number, quantity: number, note: string, reason: string | null, ref: string | null = null) =>
    api('POST', '/inventory/movements', { itemId: item(itemName), branchId: poblacion, type, quantity, note, reasonCategory: reason, photoUrl: null, supplierReference: ref });
  await manual('Siopao Asado', 1, stockOf('Siopao Asado'), 'Sold out for the day', 'Sold out');
  await manual('Sinigang na Baboy', 1, stockOf('Sinigang na Baboy') - 4, 'Batch finished at lunch', 'Used in kitchen');
  await manual('Buko Juice', 3, 6, 'Left out overnight', 'Spoiled');
  await manual('Soft Drink 330ml', 4, 3, 'Dented cans from delivery', 'Damaged');
  await manual('Cooking Oil 1L', 0, 24, 'Delivery from Palengke Wholesale', null, 'DR-20413');
  await manual('Itlog (1 pc)', 3, 12, 'Cracked in the tray', 'Damaged');
  await api('POST', '/inventory/movements', { itemId: null, inventoryItemId: ing['Fresh milk'], branchId: poblacion, type: 3, quantity: 2000, note: 'Carton past its date', reasonCategory: 'Expired', photoUrl: null, supplierReference: null });
  for (const [name, threshold] of [['Shampoo Sachet', 500], ['Toothpaste Sachet', 220], ['Mixed Nuts 40g', 140], ['Sinigang na Baboy', 10], ['Siopao Asado', 10]] as const) {
    await api('PUT', `/items/${item(name)}/low-stock-threshold`, { threshold });
  }
  await api('PUT', `/inventory-items/${ing['Ube halaya']}`, { name: 'Ube halaya', sku: null, baseUnit: 'g', packagingUnit: 'tub (500g)', packagingSize: 500, lowStockThreshold: 6000, categoryId: invCat2.id, unitCost: 0.2, isActive: true });
  await api('PUT', `/inventory-items/${ing['Caramel syrup']}`, { name: 'Caramel syrup', sku: null, baseUnit: 'ml', packagingUnit: 'bottle (750ml)', packagingSize: 750, lowStockThreshold: 9000, categoryId: invCat2.id, unitCost: 0.28, isActive: true });

  // Stock take 1 stays in Counting (blind, half counted); take 2 is counted and waiting in Review.
  const counting = await api<{ id: string }>('POST', '/stock-takes', { name: 'Linggo-linggong bilang: Meryenda at Inumin', scopeKind: 1, blind: true, branchId: poblacion, categoryIds: [categories['Meryenda'], categories['Inumin']] });
  const startedCount = await api<{ lines: { id: string; name: string }[] }>('POST', `/stock-takes/${counting.id}/start`);
  for (const [index, line] of startedCount.lines.entries()) {
    const spec = specs.find((x) => x.name === line.name);
    if (index % 2 === 0 && spec?.stock) await api('PUT', `/stock-takes/${counting.id}/lines/${line.id}`, { counted: Math.max(0, Math.round(spec.stock * 1.6 * (0.65 + rand() * 0.3))), skip: false });
  }
  const review = await api<{ id: string }>('POST', '/stock-takes', { name: 'Buwanang bilang: Sari-sari', scopeKind: 1, blind: false, branchId: riverside, categoryIds: [categories['Sari-sari']] });
  await api('POST', `/stock-takes/${review.id}/start`);
  const reviewStarted = await api<{ lines: { id: string; itemId: string | null }[] }>('GET', `/stock-takes/${review.id}`);
  const onHand = await api<{ id: string; countOnHand: number }[]>('GET', '/items');
  for (const [index, line] of reviewStarted.lines.entries()) {
    const expected = onHand.find((i) => i.id === line.itemId)?.countOnHand ?? 100;
    await api('PUT', `/stock-takes/${review.id}/lines/${line.id}`, { counted: Math.max(0, expected - (index === 2 ? 14 : between(0, 5))), skip: false });
  }
  await api('POST', `/stock-takes/${review.id}/review`);

  const plainLine = (name: string, quantity: number) => ({ itemId: item(name), itemVariantId: null, quantity, selectedModifierIds: drinks.includes(name) ? [sugarIds[2]] : [] });

  // A parked cart on the Poblacion till, and two kiosk orders waiting to be claimed.
  const parkToken = sessions[register.id].accessToken;
  await call('GET', '/transactions/cart', { token: parkToken });
  await call('POST', '/transactions/cart/lines/batch', { token: parkToken, body: { batchId: crypto.randomUUID(), lines: [plainLine('Spanish Latte', 2), plainLine('Ensaymada', 2)] } });
  await call('POST', '/transactions/cart/park', { token: parkToken, body: { label: 'Table 4 - magbabayad pa' } });
  await call('GET', '/transactions/cart', { token: parkToken });
  await call('POST', '/transactions/cart/lines/batch', { token: parkToken, body: { batchId: crypto.randomUUID(), lines: [plainLine('Tapsilog', 1), plainLine('Buko Juice', 1), plainLine('Turon', 3)] } });
  await call('POST', '/transactions/cart/park', { token: parkToken, body: { label: 'Take-out ni Ate Joy' } });

  log('kiosk orders');
  const kioskSession = await deviceSession(devices.kiosk.credential);
  const placed: { id: string }[] = [];
  for (const [order, type] of [[['Spanish Latte', 'Bibingka'], 'Dine In'], [['Tapsilog', 'Buko Juice'], 'Take Out'], [['Iced Mocha'], 'Take Out']] as const) {
    placed.push(await call<{ id: string }>('POST', '/kiosk/cart/place-order', { token: kioskSession.accessToken, body: { orderId: crypto.randomUUID(), orderType: type, paymentPreference: 'Cash', lines: order.map((name) => plainLine(name, 1)) } }));
  }
  // The kitchen has started on the first order and finished the second.
  const kitchenSession = await deviceSession(devices.kitchen.credential);
  await call('PUT', `/kitchen-display/orders/${placed[0].id}/status`, { token: kitchenSession.accessToken, body: { kitchenStatus: 1 } });
  await call('PUT', `/kitchen-display/orders/${placed[1].id}/status`, { token: kitchenSession.accessToken, body: { kitchenStatus: 2 } });

  const lastReceipt = (tender: string): number => [...salePlan].reverse().find((s) => s.branchId === poblacion && s.tender === tender && s.id !== refundTarget.id && s.receiptNumber !== null)!.receiptNumber!;
  const seed: DemoSeed = {
    tenantName: 'Kapihan ni Aling Nena',
    branches: { poblacion, riverside },
    people: { admin: { ...admin, membershipId: boot.adminMembershipId }, manager: byKey.manager, cashier: byKey.cashier, cashier2: byKey.cashier2, warehouse: byKey.warehouse },
    devices,
    pendingPairing: { name: 'Kiosk Riverside', code: pending.pairingCode },
    pendingInvite,
    items,
    categories,
    ids: { poDraft: poDraft.id, poSent: poSent.id, poReceived: poReceivedDraft.id, request: request.id, transfer: transfer.id, countingTake: counting.id, reviewTake: review.id, sugarGroup: sugar.id, addonsGroup: addons.id, supplier: suppliers[0], refundedSale: refundTarget.id },
    customers,
    receipts: {
      cash: lastReceipt('cash'),
      split: lastReceipt('split'),
    },
  };

  backdate(salePlan, voids);

  mkdirSync(path.dirname(DEMO_FILE), { recursive: true });
  writeFileSync(DEMO_FILE, JSON.stringify(seed, null, 2));
  log('done');
  return seed;
}

/**
 * The API stamps every sale with the moment it is made, so a fortnight of trade would all land on one day. This moves each
 * sale (and what hangs off it: lines, payments, stock movements, utang entries, audit entries) back to its planned day and
 * hour, directly in the throwaway database. Today's sales are spread between the shift's opening and now.
 */
function backdate(plan: { dayOffset: number; hour: number; minute: number; id: string }[], voids: { id: string; dayOffset: number; hour: number }[]): void {
  const voidValues = voids.length > 0 ? voids.map((v) => `('${v.id}'::uuid, ${v.dayOffset}, ${v.hour})`).join(',') : `(NULL::uuid, 0, 0)`;
  const values = plan.map((s) => `('${s.id}'::uuid, ${s.dayOffset}, ${s.hour}, ${s.minute})`).join(',');
  const sql = `
    CREATE TEMP TABLE open_ts AS
      SELECT LEAST((date_trunc('day', now() AT TIME ZONE 'Asia/Manila') + interval '6 hours 30 minutes') AT TIME ZONE 'Asia/Manila', now() - interval '3 hours') AS ts;
    CREATE TEMP TABLE shift_map AS
      SELECT t."Id" AS id, t."CompletedAt" AS old_at,
        CASE WHEN p.day_offset = 0
          THEN open_ts.ts + (now() - interval '1 minute' - open_ts.ts) * LEAST(1, GREATEST(0, (p.h * 60 + p.m - 420)::float8 / 780))
          ELSE (date_trunc('day', now() AT TIME ZONE 'Asia/Manila') - (p.day_offset || ' days')::interval + (p.h || ' hours')::interval + (p.m || ' minutes')::interval) AT TIME ZONE 'Asia/Manila'
        END AS new_at
      FROM (VALUES ${values}) AS p(id, day_offset, h, m)
      JOIN "Transactions" t ON t."Id" = p.id, open_ts;
    UPDATE "Transactions" t SET "CreatedAt" = sm.new_at - interval '2 minutes', "CompletedAt" = sm.new_at, "UpdatedAt" = sm.new_at,
      "RefundedAt" = CASE WHEN t."RefundedAt" IS NULL THEN NULL ELSE sm.new_at + interval '45 minutes' END
      FROM shift_map sm WHERE t."Id" = sm.id;
    UPDATE "Transactions" t SET "CreatedAt" = v.at - interval '10 minutes', "UpdatedAt" = v.at
      FROM (SELECT id, (date_trunc('day', now() AT TIME ZONE 'Asia/Manila') - (day_offset || ' days')::interval + (h || ' hours')::interval) AT TIME ZONE 'Asia/Manila' AS at
            FROM (VALUES ${voidValues}) AS x(id, day_offset, h)) v WHERE t."Id" = v.id;
    UPDATE "AuditLogs" l SET "CreatedAt" = t."UpdatedAt" FROM "Transactions" t WHERE l."TargetEntityId" = t."Id" AND t."Status" = 3;
    UPDATE "TransactionLines" l SET "CreatedAt" = sm.new_at - interval '2 minutes' FROM shift_map sm WHERE l."TransactionId" = sm.id;
    UPDATE "Payments" p SET "CreatedAt" = sm.new_at FROM shift_map sm WHERE p."TransactionId" = sm.id;
    UPDATE "CreditTransactions" c SET "CreatedAt" = sm.new_at FROM shift_map sm WHERE c."TransactionId" = sm.id;
    UPDATE "Adjustments" a SET "CreatedAt" = sm.new_at + interval '45 minutes' FROM shift_map sm WHERE a."OriginalTransactionId" = sm.id;
    UPDATE "AuditLogs" l SET "CreatedAt" = sm.new_at + interval '1 minute' FROM shift_map sm WHERE l."TargetEntityId" = sm.id;
    UPDATE "InventoryMovements" m SET "CreatedAt" = near.new_at
      FROM (SELECT DISTINCT ON (im."Id") im."Id" AS id, sm.new_at FROM "InventoryMovements" im
            JOIN shift_map sm ON sm.old_at BETWEEN im."CreatedAt" - interval '10 seconds' AND im."CreatedAt" + interval '10 seconds'
            WHERE im."Type" = 8 ORDER BY im."Id", abs(extract(epoch FROM sm.old_at - im."CreatedAt"))) near
      WHERE m."Id" = near.id;
    UPDATE "InventoryMovements" SET "CreatedAt" = now() - interval '15 days' WHERE "Note" IN ('Opening stock', 'Opening count');
    UPDATE "InventoryMovements" SET "CreatedAt" = date_trunc('day', now()) - ((abs(hashtext("Id"::text)) % 12 + 1) || ' days')::interval + interval '9 hours 30 minutes'
      WHERE "Type" IN (1, 3, 4) OR ("Type" = 0 AND "SupplierReference" IS NOT NULL);
    -- The web client has no label yet for some audit action types (see the manual report), so they are not shown in the manual.
    DELETE FROM "AuditLogs" WHERE "ActionType" NOT IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 14, 15, 16, 17);
    UPDATE "Devices" SET "LastSeenAt" = now() - (random() * interval '40 minutes') WHERE "Status" = 0;
    UPDATE "Shifts" SET "OpenedAt" = (SELECT ts FROM open_ts), "CreatedAt" = (SELECT ts FROM open_ts) WHERE "Status" = 0;
  `;
  runSql(sql);
}
