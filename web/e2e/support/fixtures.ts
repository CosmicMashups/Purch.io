/* oxlint-disable react-hooks/rules-of-hooks, no-empty-pattern -- Playwright fixtures: `use` is its own callback, not a React hook, and it requires a destructured first argument. */
import { test as base, expect, type Browser, type Page } from '@playwright/test';
import { deviceSession, freshIp, unlockOnTill, type Tokens } from './api';
import { readSeed, type Seed } from './seed';

export type Person = 'admin' | 'manager' | 'cashier' | 'warehouse';

interface Fixtures {
  seed: Seed;
  /** This test's own client address, so sign-in limits are never shared between tests. */
  ip: string;
  /** Signs in through the API and hands the session to the next page load, skipping the sign-in screen. */
  signInAs: (person: Person) => Promise<Tokens>;
  /** Pairs a kiosk, kitchen display or order board through the API and hands over its session. */
  pairDevice: (kind: 'kiosk' | 'kitchen' | 'orderBoard') => Promise<Tokens>;
  /** A second person on their own screen (their own browser profile), for flows that span devices. */
  actor: (who: Person | 'kiosk' | 'kitchen' | 'orderBoard') => Promise<Page>;
}

export const test = base.extend<Fixtures>({
  seed: async ({}, use) => use(readSeed()),

  ip: [async ({}, use) => use(freshIp()), { scope: 'test' }],

  // Every browser call to the API carries this test's own address.
  context: async ({ context, ip }, use) => {
    await context.route('**/api/**', (route) => route.continue({ headers: { ...route.request().headers(), 'x-forwarded-for': ip } }));
    await use(context);
  },

  signInAs: async ({ context, seed, ip }, use) => {
    await use(async (person) => {
      const tokens = await unlockOnTill(seed, person, ip);
      await seedSession(context, tokens, person === 'warehouse' ? seed.warehouse.credential : seed.register.credential);
      return tokens;
    });
  },

  pairDevice: async ({ context, seed, ip }, use) => {
    await use(async (kind) => {
      const device = seed[kind];
      const tokens = await deviceSession(device.credential, ip);
      await seedSession(context, tokens, device.credential);
      return tokens;
    });
  },

  actor: async ({ browser, seed }, use) => {
    const opened: Page[] = [];
    await use(async (who) => {
      const ip = freshIp();
      const unattended = who === 'kiosk' || who === 'kitchen' || who === 'orderBoard';
      const credential = unattended ? seed[who].credential : who === 'warehouse' ? seed.warehouse.credential : seed.register.credential;
      const tokens = unattended ? await deviceSession(credential, ip) : await unlockOnTill(seed, who, ip);
      const page = await newActor(browser, ip, tokens, credential);
      opened.push(page);
      return page;
    });
    await Promise.all(opened.map((p) => p.context().close()));
  },
});

async function newActor(browser: Browser, ip: string, tokens: Tokens, credential: string): Promise<Page> {
  const context = await browser.newContext();
  await context.route('**/api/**', (route) => route.continue({ headers: { ...route.request().headers(), 'x-forwarded-for': ip } }));
  await seedSession(context, tokens, credential);
  return context.newPage();
}

/** Writes the session before the app starts, once per tab, so a later refresh rotation is never overwritten. */
async function seedSession(context: import('@playwright/test').BrowserContext, tokens: Tokens, credential: string): Promise<void> {
  await context.addInitScript(
    ({ accessToken, refreshToken, credential }) => {
      if (sessionStorage.getItem('e2e-seeded')) return;
      localStorage.setItem('purch.accessToken', accessToken);
      localStorage.setItem('purch.refreshToken', refreshToken);
      // A paired device keeps its credential, which is also what makes the app treat this browser as one.
      localStorage.setItem('purch.deviceCredential', credential);
      sessionStorage.setItem('e2e-seeded', '1');
    },
    { ...tokens, credential },
  );
}

/** Text of the toast region, for asserting on friendly errors. */
export const toastText = (page: Page) => page.getByRole('status').or(page.getByRole('alert'));

export { expect };
