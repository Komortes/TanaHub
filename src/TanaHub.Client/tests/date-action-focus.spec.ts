import { test, expect } from './fixtures';

const title = "Frieren: Beyond Journey's End";

test('clearing a new dates draft keeps focus in the date editor', async ({ page, library }) => {
  await page.goto('/#library');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Add dates', exact: true }).click();
  const start = page.getByLabel('Started on', { exact: true });
  await start.fill('2026-01-02');
  const clear = page.getByRole('button', { name: 'Clear dates', exact: true });
  await clear.focus(); await clear.press('Enter');

  await expect(start).toHaveValue('');
  await expect(page.getByLabel('Completed on', { exact: true })).toHaveValue('');
  await expect(clear).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Save dates', exact: true })).toBeDisabled();
  await expect(start).toBeFocused();
  expect(library.requests.some(request => request.method === 'library.dates')).toBe(false);
});
