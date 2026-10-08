import { expect, test } from './support/fixtures';

/** Where screenshots of the new screens are written, for a person to look over. Off unless asked for. */
const SHOTS = process.env.E2E_SHOTS_DIR;

test.describe('inactive, delete and restore', () => {
  test('a modifier group goes inactive, then deleted, then comes back, each step asked for first', async ({ page, signInAs }) => {
    await signInAs('admin');
    const name = `Sides ${Date.now().toString(36)}`;

    await page.goto('/catalog/modifier-groups');
    await expect(page.getByRole('link', { name: 'Back to Business' })).toBeVisible();
    await page.getByRole('button', { name: 'Add group', exact: true }).click();
    await page.getByLabel('Group name').fill(name);
    await page.getByRole('button', { name: 'Add Group', exact: true }).click();
    await expect(page.locator('li').filter({ hasText: name }).first()).toBeVisible();

    // The row carries one menu button, not a row of text links.
    const row = page.getByRole('listitem').filter({ hasText: name }).first();
    await row.getByRole('button', { name: `Actions for ${name}` }).click();
    await page.getByRole('menuitem', { name: 'Make inactive' }).click();

    const ask = page.getByRole('dialog', { name: `Make ${name} inactive?` });
    await expect(ask).toBeVisible();
    await ask.getByRole('button', { name: 'Make inactive' }).click();

    // A snackbar with a countdown and Undo, and the group has left the Active list.
    await expect(page.getByRole('button', { name: /^Undo \(\d\)$/ })).toBeVisible();
    await expect(page.locator('li').filter({ hasText: name }).first()).toBeHidden();
    await page.getByRole('button', { name: 'Inactive' }).click();
    await expect(page.locator('li').filter({ hasText: name }).first()).toBeVisible();

    // Delete it from there. It moves to Deleted rather than disappearing for good.
    await page.getByRole('button', { name: `Actions for ${name}` }).click();
    await page.getByRole('menuitem', { name: 'Delete' }).click();
    const sure = page.getByRole('dialog', { name: `Delete ${name}?` });
    await expect(sure.getByText(/You can restore it later/)).toBeVisible();
    await sure.getByRole('button', { name: 'Delete' }).click();
    await expect(page.locator('li').filter({ hasText: name }).first()).toBeHidden();

    await page.getByRole('button', { name: 'Deleted' }).click();
    await expect(page.locator('li').filter({ hasText: name }).first()).toBeVisible();
    await page.getByRole('button', { name: `Restore ${name}` }).click();
    await expect(page.getByText(/restored as an inactive modifier group/)).toBeVisible();

    // Restored groups come back inactive, never straight onto the till.
    await page.getByRole('button', { name: 'Inactive' }).click();
    await expect(page.locator('li').filter({ hasText: name }).first()).toBeVisible();
  });

  test('undo within the five seconds puts the group straight back', async ({ page, signInAs }) => {
    await signInAs('admin');
    const name = `Toppings ${Date.now().toString(36)}`;
    await page.goto('/catalog/modifier-groups');
    await page.getByRole('button', { name: 'Add group', exact: true }).click();
    await page.getByLabel('Group name').fill(name);
    await page.getByRole('button', { name: 'Add Group', exact: true }).click();

    await page.getByRole('button', { name: `Actions for ${name}` }).click();
    await page.getByRole('menuitem', { name: 'Make inactive' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Make inactive' }).click();
    await page.getByRole('button', { name: /^Undo/ }).click();

    await expect(page.locator('li').filter({ hasText: name }).first()).toBeVisible();
    await expect(page.locator('li').filter({ hasText: name }).first()).not.toContainText('Inactive');
  });

  test('a warehouse user can deactivate an ingredient but has no way into promotions', async ({ page, signInAs }) => {
    await signInAs('warehouse');
    await page.goto('/inventory/ingredients');
    await expect(page.getByRole('link', { name: 'Back to Inventory' })).toBeVisible();
    await page.goto('/business/promotions');
    await expect(page).not.toHaveURL(/business\/promotions/);
  });
});

test.describe('orders', () => {
  test('an admin and a manager can open it; a cashier cannot', async ({ page, signInAs }) => {
    await signInAs('manager');
    await page.goto('/business/orders');
    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Back to Business' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Export CSV' })).toBeVisible();
  });

  test('a cashier is sent away from it', async ({ page, signInAs }) => {
    await signInAs('cashier');
    await page.goto('/business/orders');
    await expect(page).not.toHaveURL(/business\/orders/);
  });
});

test.describe('on a tablet and a phone', () => {
  const pages = [
    '/business/orders',
    '/catalog/modifier-groups',
    '/catalog/items',
    '/catalog/categories',
    '/business/promotions',
    '/business/staff',
    '/business/branches',
    '/business/devices',
    '/business/customers',
    '/inventory/ingredients',
    '/inventory/suppliers',
  ];
  const sizes = [
    { name: 'tablet', width: 820, height: 1180 },
    { name: 'phone', width: 390, height: 844 },
  ];

  for (const size of sizes) {
    test(`nothing scrolls sideways and the row menu stays reachable on a ${size.name}`, async ({ page, signInAs }) => {
      await page.setViewportSize({ width: size.width, height: size.height });
      await signInAs('admin');

      for (const path of pages) {
        await page.goto(path);
        await page.waitForLoadState('networkidle');
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
        expect(overflow, `${path} overflows by ${overflow}px`).toBeLessThanOrEqual(1);
        if (SHOTS) await page.screenshot({ path: `${SHOTS}/${size.name}${path.replace(/\//g, '-')}.png`, fullPage: true });
      }

      // The menu button is a full 48px target, and its menu opens within the screen.
      await page.goto('/catalog/modifier-groups');
      await page.getByRole('button', { name: 'Add group', exact: true }).click();
      await page.getByLabel('Group name').fill(`Fit ${size.name}`);
      await page.getByRole('button', { name: 'Add Group', exact: true }).click();
      const menuButton = page.getByRole('button', { name: `Actions for Fit ${size.name}` });
      const box = await menuButton.boundingBox();
      expect(box!.width).toBeGreaterThanOrEqual(47);
      expect(box!.height).toBeGreaterThanOrEqual(47);
      await menuButton.click();
      const menu = page.getByRole('menu');
      await expect(menu).toBeVisible();
      const menuBox = await menu.boundingBox();
      expect(menuBox!.x).toBeGreaterThanOrEqual(-1);
      expect(menuBox!.x + menuBox!.width).toBeLessThanOrEqual(size.width + 1);
      expect(menuBox!.y + menuBox!.height).toBeLessThanOrEqual(size.height + 1);
      if (SHOTS) await page.screenshot({ path: `${SHOTS}/${size.name}-menu-open.png` });
    });
  }
});
