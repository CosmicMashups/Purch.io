import { expect, test } from './support/fixtures';

test.describe('running the business', () => {
  test('an admin adds a new item and it appears in the list', async ({ page, signInAs }) => {
    await signInAs('admin');
    const name = `Espresso ${Date.now().toString(36)}`;

    await page.goto('/catalog/items/new');
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Base Price').fill('95');
    await page.getByRole('button', { name: 'Save Item' }).click();

    await expect(page).toHaveURL(/\/catalog\/items$/);
    await expect(page.getByText(name)).toBeVisible();
  });

  test('an admin adds a staff member, who can then sign in with the new PIN', async ({ page, signInAs, seed }) => {
    await signInAs('admin');
    const name = `Rina ${Date.now().toString(36)}`;

    await page.goto('/business/staff');
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Role').selectOption({ label: 'Cashier' });
    await page.getByLabel('Can work in').selectOption({ index: 1 });
    await page.getByLabel('Branch').selectOption({ index: 1 });
    await page.getByLabel('PIN').fill('8642');
    await page.getByRole('button', { name: 'Add', exact: true }).click();
    await expect(page.getByText(name)).toBeVisible();

    await page.getByRole('button', { name: 'Sign out' }).click();
    await page.getByLabel(/device code/i).fill(seed.register.code);
    await page.getByLabel(/pin/i).fill('8642');
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByText('Signed in as Cashier')).toBeVisible();
  });

  test('a PIN somebody already uses is refused with a plain message', async ({ page, signInAs, seed }) => {
    await signInAs('admin');
    await page.goto('/business/staff');
    await page.getByLabel('Name').fill('Copycat');
    await page.getByLabel('Role').selectOption({ label: 'Cashier' });
    await page.getByLabel('Can work in').selectOption({ index: 1 });
    await page.getByLabel('Branch').selectOption({ index: 1 });
    await page.getByLabel('PIN').fill(seed.pins.cashier);
    await page.getByRole('button', { name: 'Add', exact: true }).click();
    await expect(page.getByText('Copycat')).toHaveCount(0);
    await expect(page.getByRole('status').or(page.getByRole('alert')).first()).toBeVisible();
  });

  test('a manager can see the staff list but has no form to change it', async ({ page, signInAs }) => {
    await signInAs('manager');
    await page.goto('/business/staff');
    await expect(page.getByText('Carlo Cashier')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'New staff member' })).toHaveCount(0);
  });
});
