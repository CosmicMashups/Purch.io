import { resetRegister } from './support/api';
import { expect, test } from './support/fixtures';

test.describe('split payment', () => {
  test('takes part cash and part GCash for one sale, and the receipt lists both', async ({ page, signInAs, ip, seed }) => {
    await signInAs('cashier');
    await resetRegister(seed, ip);
    await page.goto('/sell');

    await page.getByRole('button', { name: /Iced Latte/ }).click();
    await page.getByRole('button', { name: /Mocha/ }).click();
    await expect(page.getByText('₱320.00').first()).toBeVisible();
    await page.getByRole('button', { name: /Charge/ }).click();
    await expect(page).toHaveURL(/\/sell\/payment$/);

    await page.getByRole('button', { name: 'Split payment' }).click();
    await page.getByLabel('Amount for this part').fill('120');
    await page.getByLabel('Cash received').fill('150');
    await expect(page.getByText('Change: ₱30.00')).toBeVisible();
    await page.getByRole('button', { name: 'Add payment' }).click();
    await expect(page.getByTestId('split-remaining')).toHaveText('₱200.00');

    await page.getByRole('radiogroup', { name: 'Method for this part' }).getByRole('radio', { name: 'GCash' }).click();
    await expect(page.getByLabel('Amount for this part')).toHaveValue('200');
    await page.getByLabel('Reference (optional)').fill('GC-778812');
    await page.getByRole('button', { name: 'Add payment' }).click();
    await expect(page.getByTestId('split-remaining')).toHaveText('₱0.00');
    await expect(page.getByTestId('split-change')).toHaveText('₱30.00');

    await page.getByRole('button', { name: /Complete sale/ }).click();
    await expect(page).toHaveURL(/\/sell\/receipt$/);
    const receipt = page.getByRole('article', { name: 'Receipt' });
    await expect(receipt).toContainText('₱320.00');
    await expect(receipt).toContainText('Cash (tendered ₱150.00)');
    await expect(receipt).toContainText('₱120.00');
    await expect(receipt).toContainText('GCash ref GC-778812');
    await expect(receipt).toContainText('₱200.00');
    await expect(receipt).toContainText('₱30.00');
  });
});
