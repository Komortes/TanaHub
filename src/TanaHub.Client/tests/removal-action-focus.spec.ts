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

for (const width of [320, 360]) {
  test(`the safe removal action stays visible after expanding confirmation at ${width}px`, async ({ page, library }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/#library');
    await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
    await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toBeEnabled();
    await page.getByRole('button', { name: 'Add dates', exact: true }).click();
    await page.getByLabel('Started on', { exact: true }).fill('2026-01-02');
    await page.getByRole('button', { name: 'Clear dates', exact: true }).click();
    const remove = page.getByRole('button', { name: 'Remove from library', exact: true });
    await remove.click();
    const keep = page.getByRole('button', { name: 'Keep title', exact: true });
    await expect(keep).toBeFocused();
    await page.screenshot({ path: testInfo.outputPath('safe-removal-action.png') });
    await expect(keep).toBeInViewport({ ratio: 1 });
    await keep.press('Enter');
    await expect(remove).toBeFocused();
    await expect(remove).toBeInViewport({ ratio: 1 });
    expect(library.requests.some(request => request.method === 'library.remove')).toBe(false);
  });
}
