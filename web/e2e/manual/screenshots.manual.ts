import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import type { BrowserContext, Page } from '@playwright/test';
import { call, deviceSession, unlock, signIn, type Tokens } from '../support/api';
import { expect, test } from '../support/fixtures';
import { DEMO_FILE, seedDemo, type DemoDevice, type DemoPerson, type DemoSeed } from './seed-demo';

/**
 * Photographs the web client for the tenant user manual. Seeds the demo business first (see seed-demo.ts), then signs in
 * as each role the way a person would and saves PNGs to docs/manual/images. Run with `npm run manual:shots`.
 * Set MANUAL_SKIP_SEED=1 to reuse the last seed while iterating on the shots.
 */

const here = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.resolve(here, '..', '..', '..', 'docs', 'manual', 'images');
const INDEX_JSON = path.join(path.dirname(DEMO_FILE), 'manual-index.json');

const DESKTOP = { width: 1440, height: 900 };
const PHONE = { width: 390, height: 844 };

type Who = 'admin' | 'manager' | 'cashier' | 'cashier2' | 'warehouse';
type Unattended = 'kiosk' | 'kitchen' | 'orderBoard' | 'customerDisplay';

interface IndexEntry {
  file: string;
  role: string;
  route: string;
  caption: string;
}

const entries = new Map<string, IndexEntry>();

let demo: DemoSeed;

test.use({ viewport: DESKTOP, colorScheme: 'light', locale: 'en-PH', timezoneId: 'Asia/Manila' });
test.describe.configure({ mode: 'serial' });

test.beforeAll(async () => {
  test.setTimeout(600_000);
  if (process.env.MANUAL_SKIP_SEED === '1' && existsSync(DEMO_FILE)) {
    demo = JSON.parse(readFileSync(DEMO_FILE, 'utf8')) as DemoSeed;
  } else {
    demo = await seedDemo();
  }
  mkdirSync(OUT, { recursive: true });
});

test.afterAll(() => {
  writeIndex();
});

// ---- Helpers -----------------------------------------------------------------------------------------------------

/** Waits for the page to stop loading: network quiet, no skeletons, animations finished. */
async function settle(page: Page): Promise<void> {
  await page.waitForLoadState('domcontentloaded');
  await page.waitForLoadState('networkidle', { timeout: 8_000 }).catch(() => undefined);
  await expect(page.locator('.animate-pulse')).toHaveCount(0, { timeout: 15_000 });
  await page.waitForTimeout(700);
}

async function dismissToasts(page: Page): Promise<void> {
  const buttons = page.getByRole('button', { name: 'Dismiss message' });
  while ((await buttons.count()) > 0) {
    await buttons.first().click();
    await page.waitForTimeout(100);
  }
}

interface ShotOptions {
  role: string;
  caption: string;
  /** The address the manual should give; defaults to the page's own path. */
  route?: string;
  fullPage?: boolean;
  /** Leave the page scrolled where the test put it (the default is back to the top). */
  keepScroll?: boolean;
}

/** Saves docs/manual/images/<name>.png after the page has settled and toasts are gone. */
async function shot(page: Page, name: string, { role, caption, route, fullPage, keepScroll }: ShotOptions): Promise<void> {
  await settle(page);
  await dismissToasts(page);
  if (!keepScroll) await page.evaluate(() => window.scrollTo(0, 0));
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: fullPage ?? false });
  const url = new URL(page.url());
  entries.set(name, { file: `${name}.png`, role, route: route ?? url.pathname, caption });
}

/** Mobile versions are photographed in a phone-sized viewport and put back. */
async function phoneShot(page: Page, name: string, options: ShotOptions): Promise<void> {
  await page.setViewportSize(PHONE);
  await page.waitForTimeout(400);
  await shot(page, name, options);
  await page.setViewportSize(DESKTOP);
}

async function storeSession(context: BrowserContext, tokens: Tokens, credential: string | null): Promise<void> {
  await context.addInitScript(
    ({ accessToken, refreshToken, credential }) => {
      if (sessionStorage.getItem('manual-seeded')) return;
      localStorage.setItem('purch.accessToken', accessToken);
      localStorage.setItem('purch.refreshToken', refreshToken);
      if (credential) localStorage.setItem('purch.deviceCredential', credential);
      sessionStorage.setItem('manual-seeded', '1');
    },
    { ...tokens, credential },
  );
}

/** Signs in the way each person really does: email and password for the owner and manager, device and PIN for the rest. */
async function signInAs(context: BrowserContext, who: Who, device?: DemoDevice): Promise<Tokens> {
  const person = demo.people[who];
  let tokens: Tokens;
  let credential: string | null = null;
  if (who === 'admin' || who === 'manager') {
    tokens = await signIn(person.email, person.password);
  } else {
    const till = device ?? (who === 'cashier2' ? demo.devices.register2 : who === 'warehouse' ? demo.devices.warehouse : demo.devices.register);
    tokens = await unlock(till.credential, person.membershipId, person.pin);
    credential = till.credential;
  }
  await storeSession(context, tokens, credential);
  return tokens;
}

/** Empties the register's open cart (a shot that failed half way leaves one behind). A void needs another manager's PIN. */
async function clearCart(): Promise<void> {
  const till = demo.devices.register;
  const cashier = demo.people.cashier;
  const own = await unlock(till.credential, cashier.membershipId, cashier.pin);
  const cart = await call<{ lines: { id: string }[]; manualDiscountAmount?: number }>('GET', '/transactions/cart', { token: own.accessToken });
  if (cart.lines.length > 0 || (cart.manualDiscountAmount ?? 0) > 0) {
    const boss = await unlock(till.credential, demo.people.manager.membershipId, demo.people.manager.pin);
    await call('POST', '/transactions/cart/void', { token: boss.accessToken, body: { approverPin: demo.people.admin.pin } });
  }
}

async function pairAs(context: BrowserContext, kind: Unattended): Promise<void> {
  const device: DemoDevice = demo.devices[kind];
  const tokens = await deviceSession(device.credential);
  await storeSession(context, tokens, device.credential);
}

async function typePin(page: Page, pin: string): Promise<void> {
  for (const digit of pin) await page.getByRole('button', { name: digit, exact: true }).click();
}

function writeIndex(): void {
  let known: Record<string, IndexEntry> = {};
  try {
    known = JSON.parse(readFileSync(INDEX_JSON, 'utf8')) as Record<string, IndexEntry>;
  } catch {
    // First run.
  }
  for (const [name, entry] of entries) known[name] = entry;
  mkdirSync(path.dirname(INDEX_JSON), { recursive: true });
  writeFileSync(INDEX_JSON, JSON.stringify(known, null, 2));
  const rows = Object.keys(known)
    .sort()
    .map((name) => `| ${known[name].file} | ${known[name].role} | \`${known[name].route}\` | ${known[name].caption} |`);
  const text = [
    '# Manual screenshots',
    '',
    'Produced by `npm run manual:shots` in `web/` against a seeded demo business ("Kapihan ni Aling Nena"). Do not edit by hand.',
    '',
    '| File | Role | Route | Caption |',
    '| --- | --- | --- | --- |',
    ...rows,
    '',
  ].join('\n');
  mkdirSync(OUT, { recursive: true });
  writeFileSync(path.join(OUT, 'INDEX.md'), text);
}

const person = (who: Who): DemoPerson => demo.people[who];

// ---- Signing in and setting up -----------------------------------------------------------------------------------

test.describe('before anyone is signed in', () => {
  test('login, onboarding, unlock, enrol, pair', async ({ page }) => {
    await page.goto('/login');
    await shot(page, '01-login', { role: 'Anyone (signed out)', caption: 'The sign-in page: email and password for owners and managers.' });
    await phoneShot(page, '01-login-mobile', { role: 'Anyone (signed out)', caption: 'The sign-in page on a phone.' });

    await page.goto('/onboarding');
    await page.getByLabel('Business name').fill('Kapihan ni Aling Nena');
    await page.getByLabel('What kind of business is it?').selectOption({ label: 'Sari-sari store' });
    await shot(page, '02-onboarding-business', { role: 'Anyone (signed out)', caption: 'Setup, step 1: the business name and what kind of business it is.' });

    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByLabel('Branch name').fill('Poblacion');
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByLabel('Your name').fill('Nena Villanueva');
    await page.getByLabel('Choose a PIN').fill('482915');
    await page.getByLabel('Email').fill('nena@example.com');
    await page.getByLabel('Password', { exact: true }).fill('not-a-real-password');
    await page.getByRole('checkbox').check();
    await shot(page, '03-onboarding-admin', { role: 'Anyone (signed out)', caption: 'Setup, last step: the owner picks a name, a PIN for registers, and an email and password.' });

    // A paired register shows who can unlock it.
    await page.evaluate((credential) => localStorage.setItem('purch.deviceCredential', credential), demo.devices.register.credential);
    await page.goto('/unlock');
    await page.getByRole('button', { name: person('cashier').name }).click();
    await typePin(page, '482');
    await shot(page, '04-unlock-pin', { role: 'Cashier (on a paired register)', caption: 'Unlocking a register: choose your name, then type your PIN on the keypad.' });
  });

  test('enrol and pair', async ({ page }) => {
    await page.goto(`/enrol/${demo.pendingInvite.token}`);
    await expect(page.getByText(demo.pendingInvite.name).first()).toBeVisible();
    await shot(page, '05-enrol-invite', { role: 'New staff member (invite link)', route: '/enrol/<invite token>', caption: 'Opening an invite link: the new person checks their details and sets a password and PIN.' });

    await page.goto('/pair');
    await page.getByLabel('Pairing code').fill(demo.pendingPairing.code);
    await shot(page, '06-pair-device', { role: 'Device being set up', caption: 'Pairing a new device with the one-time code from Business, Devices.' });
  });
});

// ---- The register (cashier) --------------------------------------------------------------------------------------

test.describe('at the register', () => {
  test('selling', async ({ page, context }) => {
    await clearCart();
    await signInAs(context, 'cashier');
    await page.goto('/sell');
    await expect(page.getByRole('button', { name: /Spanish Latte/ })).toBeVisible();
    await shot(page, '11-sell-grid', { role: 'Cashier', caption: 'The register: categories on the left, items to tap in the middle, the cart on the right.' });
    await phoneShot(page, '11-sell-grid-mobile', { role: 'Cashier', caption: 'The register on a phone: the same sale, stacked to fit a narrow screen.' });

    // An item with choices opens its options before it joins the cart.
    await page.getByRole('button', { name: /Spanish Latte/ }).click();
    const options = page.getByRole('dialog');
    await expect(options).toBeVisible();
    await options.getByText('50%', { exact: true }).click();
    await options.getByText('Extra shot').click();
    await shot(page, '13-options-dialog', { role: 'Cashier', caption: 'Choosing options (sugar level and add-ons) for a drink before it goes in the cart.' });
    await options.getByRole('button', { name: /^Add/ }).click();

    await page.getByRole('button', { name: /Ensaymada/ }).click();
    await expect(page.getByRole('region', { name: 'Cart' }).getByText('Ensaymada')).toBeVisible();
    await page.waitForTimeout(800);
    await page.getByRole('button', { name: /Kapeng Barako/ }).click();
    await page.getByRole('dialog').getByText('25%', { exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: /^Add/ }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await page.getByRole('button', { name: 'Dine In' }).click();
    await shot(page, '12-sell-cart-with-items', { role: 'Cashier', caption: 'A sale in progress: lines, quantities, order type and the total to charge.' });

    // Something that is not in the catalogue is rung up as an open item.
    await page.getByRole('button', { name: 'Open item' }).first().click();
    const open = page.getByRole('dialog');
    await open.getByLabel('Name').fill('Yelo (bag of ice)');
    await open.getByLabel('Price').fill('30');
    await shot(page, '14-open-item-dialog', { role: 'Cashier', caption: 'Open item: ring up something that is not in the catalogue by typing a name and price.' });
    await open.getByRole('button', { name: 'Add to cart' }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);

    // Any discount needs a manager or admin PIN, whoever is signed in.
    await page.getByRole('button', { name: 'Add discount' }).click();
    const discount = page.getByRole('dialog');
    await discount.getByLabel('Percent off').fill('10');
    await discount.getByRole('button', { name: 'Loyal customer' }).click();
    await discount.getByRole('button', { name: 'Apply discount' }).click();
    const pin = page.getByRole('dialog').filter({ hasText: /PIN/ }).last();
    await pin.getByLabel(/PIN/).fill(person('manager').pin);
    await shot(page, '15-discount-dialog-pin', { role: 'Cashier (approved by a manager PIN)', caption: 'A manual discount asks for a manager or admin PIN before it is applied.' });
    await pin.getByRole('button').filter({ hasText: /Approve|Confirm|Apply|Submit/ }).last().click();
    await expect(page.getByRole('dialog')).toHaveCount(0);

    // Setting the cart aside shows it in the parked carts drawer.
    await page.getByRole('button', { name: /^Parked carts/ }).click();
    await expect(page.getByRole('list', { name: 'Parked carts list' })).toBeVisible();
    await shot(page, '16-park-drawer', { role: 'Cashier', caption: 'Parked carts: sales set aside to finish later, with resume and discard.' });
    await page.getByRole('button', { name: 'Close parked carts' }).click();

    // Payment: cash first, then the same sale as a split between cash and GCash.
    await page.getByRole('button', { name: /^Charge/ }).click();
    await expect(page).toHaveURL(/\/sell\/payment$/);
    await page.getByLabel('Cash received').fill('500');
    await shot(page, '17-payment-cash', { role: 'Cashier', caption: 'Paying in cash: the keypad and quick amounts, with the change worked out for the cashier.' });

    await page.getByRole('button', { name: 'Split payment' }).click();
    await page.getByLabel('Amount for this part').fill('150');
    await page.getByRole('button', { name: 'Add payment' }).click();
    await page.getByRole('radio', { name: /GCash/ }).click();
    await shot(page, '18-payment-split', { role: 'Cashier', caption: 'Split payment: part in cash, the rest by GCash, until the parts add up to the total.' });
    await page.getByRole('button', { name: 'Back to single payment' }).click();

    await page.getByLabel('Cash received').fill('500');
    await page.getByRole('button', { name: /^Confirm/ }).click();
    await expect(page.getByRole('article', { name: 'Receipt' })).toBeVisible();
    await shot(page, '19-receipt', { role: 'Cashier', caption: 'The receipt after a sale, with Print, Exchange and Refund.' });

    await page.getByRole('button', { name: 'Refund' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await page.getByRole('dialog').getByLabel('Reason').fill('Customer ordered the wrong drink');
    await page.getByRole('dialog').getByLabel('Manager or admin PIN').fill(person('manager').pin);
    await shot(page, '20-refund-dialog', { role: 'Cashier (needs a manager PIN)', caption: 'Refunding a sale: a reason and a manager or admin PIN are required.' });
    await page.getByRole('dialog').getByRole('button', { name: 'Cancel' }).click();
  });
});

test.describe('at the register, after a sale', () => {
  test('find a sale and exchange', async ({ page, context }) => {
    await clearCart();
    await signInAs(context, 'cashier');
    await page.goto('/sell/find-sale');
    await page.getByLabel('Receipt number').fill(String(demo.receipts.cash));
    await shot(page, '21-find-sale', { role: 'Cashier', caption: 'Find a sale: type the receipt number to refund or exchange it.' });
    await page.getByRole('button', { name: 'Find sale' }).click();
    await expect(page.getByRole('article', { name: 'Receipt' })).toBeVisible();

    await page.getByRole('button', { name: 'Exchange' }).click();
    await expect(page).toHaveURL(/\/sell\/exchange$/);
    await page.getByRole('group', { name: /^Return quantity for/ }).first().getByRole('button', { name: 'Increase' }).click();
    await page.getByLabel('Find an item').fill('Ensaymada');
    await page.getByRole('button', { name: /Ensaymada/ }).first().click();
    await shot(page, '22-exchange', { role: 'Cashier (needs a manager PIN)', caption: 'Exchange: choose what comes back and what the customer takes instead; the difference is settled.' });
  });

  test('shifts, kiosk orders and hardware', async ({ page, context }) => {
    // A till that has no shift yet asks for the opening cash count.
    await signInAs(context, 'cashier', demo.devices.register3);
    await page.goto('/sell/shift');
    await expect(page.getByLabel('Opening cash count (PHP)')).toBeVisible();
    await page.getByLabel('Opening cash count (PHP)').fill('3000');
    await shot(page, '23-shift-open', { role: 'Cashier', caption: 'Opening a shift: count the cash in the drawer before the first sale.' });
  });

  test('open shift, kiosk orders and hardware', async ({ page, context }) => {
    await signInAs(context, 'cashier');
    await page.goto('/sell/shift');
    await page.getByLabel('Closing cash count (PHP)').fill('4650');
    await shot(page, '24-shift-close-x-reading', { role: 'Cashier', caption: 'An open shift: the running summary, and the closing cash count with a manager PIN.' });

    await page.goto('/sell/kiosk-orders');
    await expect(page.getByRole('button', { name: 'Take this order' }).first()).toBeVisible();
    await shot(page, '25-kiosk-orders', { role: 'Cashier', caption: 'Orders placed at the kiosk, waiting for a cashier to take them and collect payment.' });

    await page.goto('/sell/hardware');
    await shot(page, '26-hardware', { role: 'Cashier', caption: 'Hardware: the receipt printer, cash drawer and barcode scanner for this register.' });
  });
});

// ---- Owner: catalogue, promotions -------------------------------------------------------------------------------------

test.describe('as the owner: catalogue', () => {
  test('home, items and promotions', async ({ page, context }) => {
    await signInAs(context, 'admin');

    await page.goto('/');
    await shot(page, '10-home-dashboard', { role: 'Admin', caption: 'The home dashboard: today and the last 30 days of revenue, busiest days and top sellers.' });
    await phoneShot(page, '10-home-dashboard-mobile', { role: 'Admin', caption: 'The home dashboard on a phone.' });

    await page.goto('/catalog/items');
    await expect(page.getByText('Spanish Latte').first()).toBeVisible();
    await shot(page, '30-items-list', { role: 'Admin', caption: 'The item list, grouped by category, with price, pricing type and stock.' });
    await phoneShot(page, '30-items-list-mobile', { role: 'Admin', caption: 'The item list on a phone.' });

    await page.getByRole('button', { name: 'Actions for Spanish Latte' }).click();
    await page.getByRole('menuitem', { name: 'Edit' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await shot(page, '31-item-dialog', { role: 'Admin', caption: 'Editing an item: name, SKU, barcode, category, price, cost and picture.' });
    await dialog.getByText(/Ingredients|Recipe/i).first().scrollIntoViewIfNeeded();
    await shot(page, '35-item-recipe', { role: 'Admin', caption: 'The ingredients a drink uses, so each sale takes them out of stock.' });
    await dialog.getByRole('button', { name: /Cancel|Close/ }).first().click();

    await page.goto('/catalog/categories');
    await shot(page, '32-categories', { role: 'Admin', caption: 'Categories: the groups items are sorted into at the register and on the kiosk.' });

    await page.goto('/catalog/modifier-groups');
    await shot(page, '33-modifier-groups', { role: 'Admin', caption: 'Modifier groups: choices like sugar level and add-ons, with their price changes.' });

    await page.goto(`/catalog/items/${demo.items['Caffe Latte'].id}/variants`);
    await expect(page.getByText('Regular 12oz')).toBeVisible();
    await shot(page, '34-item-variants', { role: 'Admin', caption: 'Variants of one item, such as the latte in three sizes with their own prices.' });

    await page.goto('/business/promotions');
    await expect(page.getByText('Buy 2 Turon, get 1 Turon free')).toBeVisible();
    await shot(page, '37-promotions-bogo', { role: 'Admin', caption: 'Promotions: a Buy 1 Take 1 deal. Combo deals, item discounts and promo codes have their own tabs.' });
    await page.getByRole('tab', { name: 'Promo codes' }).click();
    await expect(page.getByText('KAPE20')).toBeVisible();
    await shot(page, '36-promotions-codes', { role: 'Admin', caption: 'Promo codes a cashier can type at the register.' });
  });
});

// ---- Owner: inventory -------------------------------------------------------------------------------------------------

test.describe('as the owner: inventory', () => {
  test('stock, purchasing and counts', async ({ page, context }) => {
    await signInAs(context, 'admin');

    await page.goto('/inventory');
    await expect(page.getByText('Restock first')).toBeVisible();
    await shot(page, '40-inventory-home', { role: 'Admin', caption: 'Inventory home: stock health, what to restock first and the shortcuts for purchasing and counts.' });

    await page.goto('/inventory/movements');
    await expect(page.getByRole('row').nth(2)).toBeVisible();
    await shot(page, '41-movements', { role: 'Admin', caption: 'The stock movement log: every sale, delivery, spoilage and count, with who and when.' });

    await page.getByRole('button', { name: 'Record movement' }).click();
    const move = page.getByRole('dialog');
    await expect(move).toBeVisible();
    await move.getByPlaceholder('Search items and ingredients').fill('Buko');
    await page.getByRole('option', { name: /Buko Juice/ }).first().click();
    await move.getByLabel('Branch').selectOption({ label: 'Poblacion' });
    await move.getByLabel('Movement type').selectOption({ label: 'Spoiled' }).catch(() => move.getByLabel('Movement type').selectOption({ index: 3 }));
    await move.getByLabel('Quantity').fill('6');
    await move.getByLabel('Reason category').fill('Expired');
    await move.getByLabel('Note (optional)').fill('Left out overnight');
    await shot(page, '42-record-movement', { role: 'Admin', caption: 'Recording a stock movement by hand: stock in, spoilage, damage or a correction.' });
    await move.getByRole('button', { name: 'Cancel' }).click();

    await page.goto('/inventory/ingredients');
    await expect(page.getByText('Beef tapa')).toBeVisible();
    await shot(page, '43-ingredients', { role: 'Admin', caption: 'Ingredients: what is bought in bulk and used in recipes, with packaging and low-stock alerts.' });

    await page.goto('/inventory/suppliers');
    await expect(page.getByText('Palengke Wholesale Trading')).toBeVisible();
    await shot(page, '44-suppliers', { role: 'Admin', caption: 'Suppliers, with contacts and what each one supplies.' });

    await page.goto('/inventory/purchase-requests');
    await expect(page.getByText('POB-PR-00001')).toBeVisible();
    await shot(page, '45-purchase-requests', { role: 'Admin', caption: 'Purchase requests: what to buy, waiting for approval or approved and ready to turn into orders.' });

    await page.goto('/inventory/purchase-orders');
    await expect(page.getByText(/-PO-0000/).first()).toBeVisible();
    await shot(page, '46b-purchase-orders', { role: 'Admin', caption: 'Purchase orders in Draft, Submitted and Received states.' });
    await page.goto(`/inventory/purchase-orders/${demo.ids.poSent}`);
    await expect(page.getByText('Items ordered')).toBeVisible();
    await shot(page, '46-purchase-order-detail', { role: 'Admin', caption: 'One purchase order: supplier, items ordered and delivered, totals, and Record delivery.' });

    await page.goto('/inventory/incoming-receiving');
    await expect(page.getByText('POB-IRR-00001')).toBeVisible();
    await shot(page, '47-incoming-receiving', { role: 'Admin', caption: 'Incoming receiving: deliveries checked in, linked to their purchase orders.' });

    await page.goto('/inventory/transfers');
    await expect(page.getByText('In transit')).toBeVisible();
    await shot(page, '48-transfers', { role: 'Admin', caption: 'Stock transfers between branches, from pending to in transit to received.' });

    await page.goto('/inventory/import');
    const csv = path.join(tmpdir(), 'katalogo-update.csv');
    writeFileSync(
      csv,
      [
        'type,sku,barcode,name,category,price,cost,reorder_threshold,unit,active',
        'item,MER-901,4807009001,Taho (cup),Meryenda,30,12,,,true',
        'item,MER-902,4807009002,Kalamansi Muffin,Meryenda,48,19,,,true',
        'item,SAR-901,4807009003,Pomelo Candy (1 pc),Sari-sari,2,1,,,true',
        'item,MER-013,4807001012,Banana Cue,Meryenda,22,8,,,true',
        '',
      ].join('\n'),
    );
    await page.locator('input[type="file"]').setInputFiles(csv);
    await page.getByRole('button', { name: 'Upload and review' }).click();
    await expect(page.getByText(/will create|Will create|New/i).first()).toBeVisible({ timeout: 15_000 });
    await shot(page, '51-import-wizard', { role: 'Admin', caption: 'Importing a CSV: the file is checked first, and you see what will be created or changed before anything is saved.' });

    await page.goto(`/inventory/stock-takes/${demo.ids.countingTake}`);
    await expect(page.getByText('Blind')).toBeVisible();
    await shot(page, '49-stock-take-counting', { role: 'Admin', caption: 'A blind stock take in progress: the counter types what they see, without the expected quantity.' });

    await page.goto(`/inventory/stock-takes/${demo.ids.reviewTake}`);
    await expect(page.getByText('Value impact')).toBeVisible();
    await shot(page, '50-stock-take-review', { role: 'Admin', caption: 'Reviewing a stock take: expected, counted, variance and its value before the counts are applied.' });
  });
});

// ---- Owner: reports ---------------------------------------------------------------------------------------------------

test.describe('as the owner: reports', () => {
  test('the report tabs', async ({ page, context }) => {
    await signInAs(context, 'admin');
    const tabs: [string, string, string, string][] = [
      ['60-reports-sales-mix', 'sales', 'Gross sales', 'Sales mix: gross sales by payment type, category and hour of day over the chosen period.'],
      ['61-reports-tax', 'tax', 'VAT', 'The tax report: VATable, VAT-exempt and zero-rated sales and the VAT collected.'],
      ['62-reports-profit', 'profit', 'Profit', 'The profit report: sales less the cost of what was sold, by item and by category.'],
      ['63-reports-discounts', 'discounts', 'Discount', 'Discounts: promotions and manual discounts given, and who approved them.'],
      ['64-reports-voids', 'voids', 'Void', 'Voids and refunds: what was cancelled or returned, by whom and why.'],
      ['65-reports-x-z', 'bir', 'reading', 'X and Z readings: the end-of-shift and end-of-day summaries.'],
      ['66-reports-valuation-cogs', 'valuation', 'valuation', 'Stock valuation: what the stock on hand is worth at cost.'],
      ['67-reports-exports', 'exports', 'Export', 'Exports: CSV files of sales, discounts, voids and refunds to download.'],
    ];
    for (const [name, tab, , caption] of tabs) {
      await page.goto(`/reports?tab=${tab}`);
      await expect(page.getByRole('tabpanel')).toBeVisible();
      if (tab === 'bir') await page.getByRole('button', { name: 'Counter 1', exact: true }).click();
      if (tab === 'bir') {
        await page.getByRole('button', { name: 'Take X-reading' }).click();
        await expect(page.getByText(/Gross|Net sales|Cash/i).first()).toBeVisible({ timeout: 15_000 });
      }
      await shot(page, name, { role: 'Admin', route: `/reports?tab=${tab}`, caption });
    }
  });
});

// ---- Owner: business --------------------------------------------------------------------------------------------------

test.describe('as the owner: business', () => {
  test('hub, people, places and settings', async ({ page, context }) => {
    await signInAs(context, 'admin');

    await page.goto('/business');
    await expect(page.getByText('Money owed')).toBeVisible();
    await shot(page, '70-business-hub', { role: 'Admin', caption: 'The Business hub: what needs attention today, money owed, the team, and links to every setup page.' });

    await page.goto('/business/staff');
    await expect(page.getByText('Waiting to join')).toBeVisible();
    await shot(page, '71-staff', { role: 'Admin', caption: 'Staff: who can work, their role and branches, and invitations still waiting to be opened.' });

    await page.getByRole('button', { name: 'Invite someone' }).click();
    const invite = page.getByRole('dialog');
    await invite.getByLabel('Name').fill('Rico Fernandez');
    await invite.getByLabel('Email').fill('rico@kapihan.test');
    await invite.getByLabel('Cashier').check();
    await invite.getByLabel('Riverside').check();
    await invite.getByRole('button', { name: 'Invite', exact: true }).click();
    await expect(page.getByRole('dialog').getByText(/enrol|link/i).first()).toBeVisible();
    await shot(page, '72-invite-link', { role: 'Admin', caption: 'After inviting someone: the one-time link and QR code to hand to them. No email is sent.' });
    await page.keyboard.press('Escape');

    await page.goto('/business/branches');
    await expect(page.getByText('Riverside').first()).toBeVisible();
    await shot(page, '73-branches', { role: 'Admin', caption: 'Branches: each place the business sells from, with its code and address.' });
    await page.getByRole('button', { name: 'Actions for Poblacion' }).click();
    await page.getByRole('menuitem', { name: 'Settings' }).click();
    await expect(page.getByRole('form', { name: 'Manual GCash QR' })).toBeVisible();
    await page.getByRole('form', { name: 'Manual GCash QR' }).scrollIntoViewIfNeeded();
    await shot(page, '74-branch-hardware-gcash', { role: 'Admin', caption: 'Branch settings: receipt printer, cash drawer and the GCash QR customers scan to pay.' });
    await page.keyboard.press('Escape');

    await page.goto('/business/devices');
    await expect(page.getByText('Kiosk sa Pinto')).toBeVisible();
    await shot(page, '75-devices', { role: 'Admin', caption: 'Devices: every register, kiosk and display paired to a branch, and when each was last seen.' });

    await page.goto('/business/settings');
    await expect(page.getByRole('heading', { name: 'Look and feel' })).toBeVisible();
    await shot(page, '76-settings-branding', { role: 'Admin', caption: 'Business settings: your colours, logo and font across the app.' });
    await page.locator('#options-heading').scrollIntoViewIfNeeded();
    await shot(page, '77-settings-options', { role: 'Admin', caption: 'Business settings: how the business sells, tracks ingredients and handles utang.', keepScroll: true });

    await page.goto('/business/customers');
    await expect(page.getByText('Payments due soon')).toBeVisible();
    await shot(page, '78-customers-utang', { role: 'Admin', caption: 'Customers with utang: balances against credit limits, and payments coming due.' });

    await page.goto('/business/orders');
    await page.getByLabel('From').fill(new Date(Date.now() - 13 * 86_400_000).toISOString().slice(0, 10));
    await expect(page.getByText(/\d+ orders/)).toBeVisible();
    await shot(page, '79-orders', { role: 'Admin', caption: 'Orders: every sale and its receipt, with filters for branch, cashier, status and payment.' });

    await page.goto('/business/audit-log');
    await expect(page.getByRole('cell', { name: 'Sale voided' }).first()).toBeVisible();
    await shot(page, '80-audit-log', { role: 'Admin', caption: 'The audit log: sensitive actions such as voids, refunds and discount overrides, and who did them.' });

    await page.goto('/business/sync-conflicts');
    await shot(page, '81-sync-conflicts', { role: 'Admin', caption: 'Sync conflicts: changes from two devices on the same record, kept for review.' });
  });
});

// ---- Devices: kiosk, kitchen, order board, customer display ---------------------------------------------------------

test.describe('devices and screens', () => {
  test('the kiosk', async ({ page, context }) => {
    await pairAs(context, 'kiosk');
    await page.goto('/kiosk');
    await expect(page.getByRole('link', { name: /Tap anywhere to begin/ })).toBeVisible();
    await shot(page, '90-kiosk-welcome', { role: 'Kiosk (paired device)', caption: 'The kiosk welcome screen customers see while it waits.' });

    await page.getByRole('link', { name: /Tap anywhere to begin/ }).click();
    await expect(page.getByRole('button', { name: /Bibingka/ })).toBeVisible();
    await shot(page, '91-kiosk-menu', { role: 'Kiosk (paired device)', caption: 'The kiosk menu: pictures and prices by category for the customer to tap.' });

    await page.getByRole('button', { name: /Bibingka/ }).click();
    await page.getByRole('button', { name: 'Add to order' }).click();
    await page.getByRole('link', { name: 'Complete Order' }).click({ timeout: 15_000 });
    await expect(page.getByRole('heading', { name: 'Your order' })).toBeVisible();
    await shot(page, '92-kiosk-cart', { role: 'Kiosk (paired device)', caption: 'The customer reviews their order before choosing how to pay.' });

    await page.getByRole('link', { name: 'Proceed to payment' }).click();
    await page.getByRole('button', { name: /Take Out/ }).click();
    await page.getByRole('button', { name: /^Cash/ }).click();
    await page.getByRole('button', { name: 'Place order' }).click();
    await expect(page.getByLabel(/Order number \d+/)).toBeVisible({ timeout: 20_000 });
    await shot(page, '93-kiosk-done', { role: 'Kiosk (paired device)', caption: 'The order is placed: the customer gets an order number and pays at the counter.' });
  });

  test('kitchen display and order board', async ({ page, context, browser }) => {
    await pairAs(context, 'kitchen');
    await page.goto('/kitchen');
    await shot(page, '94-kitchen-display', { role: 'Kitchen display (paired device)', caption: 'The kitchen display: orders waiting to be made, oldest first, to tap through to ready.' });

    const board = await browser.newContext({ viewport: DESKTOP, colorScheme: 'light', locale: 'en-PH', timezoneId: 'Asia/Manila' });
    await storeSession(board, await deviceSession(demo.devices.orderBoard.credential), demo.devices.orderBoard.credential);
    const boardPage = await board.newPage();
    await boardPage.goto('/order-board');
    await shot(boardPage, '95-order-board', { role: 'Order board (paired device)', caption: 'The order board customers watch to see when their number is ready.' });
    await board.close();
  });

  test('customer display follows the register', async ({ page, context, browser }) => {
    await clearCart();
    const tokens = await signInAs(context, 'cashier');
    await page.goto('/sell');
    await expect(page.getByRole('button', { name: /Ensaymada/ })).toBeVisible();
    await page.getByRole('button', { name: /Ensaymada/ }).click();
    await expect(page.getByRole('region', { name: 'Cart' }).getByText('Ensaymada')).toBeVisible();
    await page.waitForTimeout(800);
    await page.getByRole('button', { name: /^Turon/ }).click();
    await expect(page.getByRole('region', { name: 'Cart' }).getByText('Turon')).toBeVisible();
    await page.waitForTimeout(1500);

    const display = await browser.newContext({ viewport: DESKTOP, colorScheme: 'light', locale: 'en-PH', timezoneId: 'Asia/Manila' });
    await storeSession(display, await deviceSession(demo.devices.customerDisplay.credential), demo.devices.customerDisplay.credential);
    const displayPage = await display.newPage();
    await displayPage.goto('/customer-display');
    await expect(displayPage.getByText('Ensaymada')).toBeVisible({ timeout: 20_000 });
    await shot(displayPage, '96-customer-display', { role: 'Customer display (paired device)', caption: 'The customer display: it follows the register and shows each item and the total as it is rung up.' });
    await display.close();
    void tokens;
    await clearCart();
  });

  test('offline banner and the lock button', async ({ page, context }) => {
    await clearCart();
    await signInAs(context, 'cashier');
    await page.goto('/sell');
    await expect(page.getByRole('button', { name: /Ensaymada/ })).toBeVisible();
    await page.getByRole('button', { name: 'Lock' }).hover();
    await shot(page, '98-lock-button', { role: 'Cashier', caption: 'The Lock button: a cashier locks the register when stepping away; the next person unlocks with their PIN.' });

    await context.setOffline(true);
    await page.evaluate(() => window.dispatchEvent(new Event('offline')));
    await expect(page.getByText(/offline/i).first()).toBeVisible({ timeout: 10_000 });
    await shot(page, '97-offline-banner', { role: 'Cashier', caption: 'When the connection drops, a banner says so; selling continues from what the register already has.' });
    await context.setOffline(false);
  });
});

