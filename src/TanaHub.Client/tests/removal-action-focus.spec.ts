import { test, expect } from './fixtures';

const title = "Frieren: Beyond Journey's End";

const id = 'anilist:154587';

test('opening removal confirmation focuses Keep title and cancelling restores its trigger', async ({ page, library }) => {
  await page.goto('/#library');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toBeEnabled();
  const remove = page.getByRole('button', { name: 'Remove from library', exact: true });
  await remove.focus(); await remove.press('Enter');
  const keep = page.getByRole('button', { name: 'Keep title', exact: true });
  await expect(keep).toBeFocused();
  await keep.press('Enter');
  await expect(remove).toBeFocused();
  expect(library.requests.some(request => request.method === 'library.remove')).toBe(false);
  expect(library.entries.has(id)).toBe(true);
});
