import { resetRegister } from './support/api';
import { expect, test } from './support/fixtures';

test('the customer display in a second window follows the sale on the till', async ({ page, context, signInAs, ip, seed }) => {
  await signInAs('cashier');
  await resetRegister(seed.register.code, seed.pins.manager, ip);

  await page.goto('/sell');
  await expect(page.getByRole('button', { name: /Iced Latte/ })).toBeVisible();

  const display = await context.newPage();
  await display.goto('/customer-display');
  await expect(display.getByRole('heading', { name: 'Welcome!' })).toBeVisible();

  await page.getByRole('button', { name: /Iced Latte/ }).click();
  await expect(display.getByText('Iced Latte')).toBeVisible();
  await expect(display.getByText('₱150.00').first()).toBeVisible();

  await page.getByRole('button', { name: /Mocha/ }).click();
  await expect(display.getByText('Mocha')).toBeVisible();
  await expect(display.getByText('₱320.00').first()).toBeVisible();
});

test('a display window opened after the order started still shows it', async ({ page, context, signInAs, ip, seed }) => {
  await signInAs('cashier');
  await resetRegister(seed.register.code, seed.pins.manager, ip);

  await page.goto('/sell');
  await page.getByRole('button', { name: /Iced Latte/ }).click();
  await expect(page.getByText('₱150.00').first()).toBeVisible();

  const display = await context.newPage();
  await display.goto('/customer-display');
  await expect(display.getByText('Iced Latte')).toBeVisible();
});
