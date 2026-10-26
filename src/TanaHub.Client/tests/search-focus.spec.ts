import { test, expect } from './fixtures';

const restoredTitle = "Frieren: Beyond Journey's End";
const matchingTitle = 'Demon Slayer: Kimetsu no Yaiba';

for (const destination of [
  { route: 'library', searchLabel: 'Search your library', region: 'Your library titles' },
  { route: 'discover', searchLabel: 'Search the catalog', region: 'Catalog titles' },
] as const) {
  for (const activation of ['click', 'keyboard Enter'] as const) {
    test(`${destination.route} search clear restores input focus and results after ${activation}`, async ({ page, library }) => {
      await page.goto(`/#${destination.route}`);
      const search = page.getByRole('searchbox', { name: destination.searchLabel, exact: true });
      const collection = page.getByRole('region', { name: destination.region, exact: true });
      const titles = collection.getByRole('article');
      const restored = collection.getByRole('button', { name: `Open ${restoredTitle}`, exact: true });
      await expect(restored).toBeVisible();
      await expect(collection).toHaveAttribute('aria-busy', 'false');
      const initialCount = await titles.count();

      await search.fill('Demon');
      await expect(titles).toHaveCount(1);
      await expect(collection.getByRole('button', { name: `Open ${matchingTitle}`, exact: true })).toBeVisible();
      await expect(restored).toHaveCount(0);
      const clear = page.getByRole('button', { name: 'Clear search', exact: true });
      if (activation === 'click') await clear.click();
      else {
        await search.press('Tab');
        await expect(clear).toBeFocused();
        await page.keyboard.press('Enter');
      }

      await expect(search).toHaveValue('');
      await expect(clear).toHaveCount(0);
      await expect(titles).toHaveCount(initialCount);
      await expect(restored).toBeVisible();
      await expect(search).toBeFocused();
    });
  }
}
