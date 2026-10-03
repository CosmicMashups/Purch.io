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

  test('an admin invites a staff member, who opens the link and sets a password and PIN', async ({ page, signInAs, browser }) => {
    await signInAs('admin');
    const name = `Rina ${Date.now().toString(36)}`;

    await page.goto('/business/staff');
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Email').fill(`rina-${Date.now().toString(36)}@e2e.test`);
    await page.getByLabel('Cashier').check();
    await page.getByLabel('Main Branch').check();
    await page.getByRole('button', { name: 'Invite', exact: true }).click();

    const link = await page.getByTestId('invite-url').innerText();
    expect(link).toMatch(/\/enrol\//);

    // The person opens it on their own phone: a different browser with no session.
    const phone = await (await browser.newContext()).newPage();
    await phone.goto(link);
    await expect(phone.getByRole('heading', { name: /^Join / })).toBeVisible();
    await phone.getByLabel(/^Choose a password/).fill('Rina-password-1');
    await phone.getByLabel(/^Type it again/).fill('Rina-password-1');
    await phone.getByLabel(/^Choose a PIN/).fill('8642');
    await phone.getByRole('button', { name: 'Join' }).click();
    await expect(phone).toHaveURL(/\/sell$/);
    await phone.context().close();
  });

  test('a manager sees the staff list and can only invite staff', async ({ page, signInAs }) => {
    await signInAs('manager');
    await page.goto('/business/staff');
    await expect(page.getByText('Carlo Cashier')).toBeVisible();
    await expect(page.getByLabel('Role').locator('option')).toHaveText(['Staff']);
  });
});
