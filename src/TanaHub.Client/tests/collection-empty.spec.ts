import { test, expect } from './fixtures';

for (const emptyType of ['Anime', 'Manga'] as const) {
  test(`an empty ${emptyType} filter explains matching results and can return to all saved titles`, async ({ page, library }, testInfo) => {
    for (const [id, entry] of library.entries) if (entry.mediaType === emptyType) library.entries.delete(id);
    const remaining = [...library.entries.values()][0];
    await page.goto('/#library');
    await expect(page.getByRole('button', { name: `Open ${remaining.media.title}`, exact: true })).toBeVisible();
    await page.getByRole('button', { name: emptyType, exact: true }).click();
    await expect(page.getByRole('article')).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath('type-filter-empty.png'), fullPage: true });
    await expect(page.getByRole('heading', { name: 'No matching titles', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Your library is empty', exact: true })).toHaveCount(0);
    await page.getByRole('button', { name: 'Reset filters', exact: true }).click();
    await expect(page.getByRole('button', { name: `Open ${remaining.media.title}`, exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'All', exact: true })).toHaveAttribute('aria-pressed', 'true');
    expect(library.requests.filter(request => request.method === 'library.list').at(-1)?.params.type).toBeUndefined();
  });
}

test('a genuinely empty unfiltered library retains its first-use actions', async ({ page, library }) => {
  library.entries.clear();
  await page.goto('/#library');
  await expect(page.getByRole('heading', { name: 'Your library is empty', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Discover titles', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Import a list', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Reset filters', exact: true })).toHaveCount(0);
});
