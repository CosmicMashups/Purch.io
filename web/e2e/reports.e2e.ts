import { expect, test } from './support/fixtures';

const TABS = [
  { tab: 'sales', heading: 'Sales by payment type' },
  { tab: 'tax', heading: 'Tax by day' },
  { tab: 'profit', heading: 'Profit by day' },
  { tab: 'discounts', heading: 'Discounts by cashier' },
  { tab: 'voids', heading: 'Voids' },
];

test.describe('the new reports', () => {
  for (const { tab, heading } of TABS) {
    test(`the ${tab} tab loads with no error and offers CSV and PDF`, async ({ page, signInAs }) => {
      await signInAs('admin');
      await page.goto(`/business/reports?tab=${tab}`);
      await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Export CSV' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Save as PDF' })).toBeVisible();
      await page.waitForLoadState('networkidle');
      await expect(page.getByText(/is unavailable|something went wrong/i)).toHaveCount(0);
    });
  }

  test('the Home dashboard shows the three executive cards once scrolled to', async ({ page, signInAs }) => {
    await signInAs('admin');
    await page.goto('/');
    await page.getByRole('heading', { name: 'Profit and leakage' }).scrollIntoViewIfNeeded();
    await expect(page.getByRole('heading', { name: 'Gross margin' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Revenue leakage' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'What earns the money' })).toBeVisible();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/is unavailable|something went wrong/i)).toHaveCount(0);
  });

  test('a manager can read the reports but the Admin-only listing downloads are hidden', async ({ page, signInAs }) => {
    await signInAs('manager');
    await page.goto('/business/reports?tab=voids');
    await expect(page.getByRole('heading', { name: 'Voids', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: /Download all voids CSV/ })).toHaveCount(0);
  });
});
