import { expect, test } from './support/fixtures';

test.describe('business forms', () => {
  test('customers, staff, devices, branches and promotions open their forms in a dialog, not on the page', async ({ page, signInAs }) => {
    await signInAs('admin');
    const pages: [string, string, string][] = [
      ['/business/customers', 'Add customer', 'Credit limit (PHP)'],
      ['/business/staff', 'Invite someone', 'Email'],
      ['/business/devices', 'Add device', 'Type'],
      ['/business/branches', 'Add branch', 'Address (optional)'],
      ['/business/promotions?type=codes', 'Add promo code', 'Code'],
    ];
    for (const [path, button, field] of pages) {
      await page.goto(path);
      await expect(page.getByLabel(field, { exact: true })).toHaveCount(0);
      await page.getByRole('button', { name: button }).click();
      await expect(page.getByRole('dialog').getByLabel(field, { exact: true })).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toBeHidden();
    }
  });
});
