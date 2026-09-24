/* oxlint-disable react-hooks/rules-of-hooks, no-empty-pattern -- Playwright fixtures: `use` is its own callback, not a React hook, and it requires a destructured first argument. */
import { test as base, expect, type Browser, type Page } from '@playwright/test';
import { devicePair, freshIp, pinLogin, type Tokens } from './api';
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

const PAIR_PATH = { kiosk: '/kiosk/session', kitchen: '/kitchen-display/session', orderBoard: '/order-board/session' } as const;

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
      const tokens = await pinLogin(seed.register.code, seed.pins[person], ip);
      await seedSession(context, tokens);
      return tokens;
    });
  },

  pairDevice: async ({ context, seed, ip }, use) => {
    await use(async (kind) => {
      const device = seed[kind];
      const tokens = await devicePair(PAIR_PATH[kind], device.code, device.pin, ip);
      await seedSession(context, tokens);
      return tokens;
    });
  },

  actor: async ({ browser, seed }, use) => {
    const opened: Page[] = [];
    await use(async (who) => {
      const ip = freshIp();
      const tokens = who === 'kiosk' || who === 'kitchen' || who === 'orderBoard' ? await devicePair(PAIR_PATH[who], seed[who].code, seed[who].pin, ip) : await pinLogin(seed.register.code, seed.pins[who], ip);
      const page = await newActor(browser, ip, tokens);
      opened.push(page);
      return page;
    });
    await Promise.all(opened.map((p) => p.context().close()));
  },
});

async function newActor(browser: Browser, ip: string, tokens: Tokens): Promise<Page> {
  const context = await browser.newContext();
  await context.route('**/api/**', (route) => route.continue({ headers: { ...route.request().headers(), 'x-forwarded-for': ip } }));
  await seedSession(context, tokens);
  return context.newPage();
}

/** Writes the session before the app starts, once per tab, so a later refresh rotation is never overwritten. */
async function seedSession(context: import('@playwright/test').BrowserContext, tokens: Tokens): Promise<void> {
  await context.addInitScript(
    ({ accessToken, refreshToken }) => {
      if (sessionStorage.getItem('e2e-seeded')) return;
      localStorage.setItem('purch.accessToken', accessToken);
      localStorage.setItem('purch.refreshToken', refreshToken);
      sessionStorage.setItem('e2e-seeded', '1');
    },
    tokens,
  );
}

/** Text of the toast region, for asserting on friendly errors. */
export const toastText = (page: Page) => page.getByRole('status').or(page.getByRole('alert'));

export { expect };
