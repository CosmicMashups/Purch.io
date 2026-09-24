import { expect, test } from './support/fixtures';

test('the stack is up: the seeded owner reaches Home', async ({ page, signInAs }) => {
  await signInAs('admin');
  await page.goto('/');
  await expect(page.getByText('Signed in as Admin')).toBeVisible();
});
