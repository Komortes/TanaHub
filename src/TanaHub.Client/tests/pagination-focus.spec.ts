import type { Locator } from '@playwright/test';
import { test, expect, media, type MockLibrary } from './fixtures';

function seed(library: MockLibrary, collection: 'library' | 'discover') {
  const template = [...library.entries.values()][0];
  const count = collection === 'library' ? 50 : 30;
  for (let index = 0; index < count; index += 1) {
    const item = media(`anilist:pagination-focus-${index}`, `Extra title ${index}`);
    library.catalog.set(item.id, item);
    if (collection === 'library') library.entries.set(item.id, { ...template, mediaId: item.id, media: item });
  }
  return collection === 'library' ? library.entries.size : [...library.catalog.values()].filter(item => item.type === 'Anime').length;
}
async function activate(control: Locator, method: 'click' | 'Enter') {
  if (method === 'click') await control.click();
  else { await control.focus(); await control.press('Enter'); }
}

for (const collection of ['library', 'discover'] as const) {
  for (const activation of ['click', 'Enter'] as const) {
    test(`the final ${collection} page focuses its first new title after ${activation}`, async ({ page, library }) => {
      const total = seed(library, collection);
      await page.goto(`/#${collection}`);
      const cards = page.getByRole('article');
      await expect(cards).toHaveCount(collection === 'library' ? 48 : 24);
      const original = new Set(await cards.evaluateAll(items => items.map(item => item.getAttribute('aria-label'))));
      await activate(page.getByRole('button', { name: 'Load more titles', exact: true }), activation);
      await expect(cards).toHaveCount(total);
      const firstNew = (await cards.evaluateAll(items => items.map(item => item.getAttribute('aria-label')))).find(title => !original.has(title));
      expect(firstNew).toBeTruthy();
      await expect(page.getByRole('button', { name: `Open ${firstNew}`, exact: true })).toBeFocused();
      await expect(page.getByRole('button', { name: 'Load more titles', exact: true })).toHaveCount(0);
    });
  }
}

for (const nextAction of ['search', 'navigate'] as const) {
  test(`pagination preserves a later deliberate ${nextAction} choice`, async ({ page, library }) => {
    const total = seed(library, 'library');
    let entered = false;
    let release!: () => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    library.override = async ({ method, params }) => {
      if (method === 'library.list' && params.page === 2) { entered = true; await gate; }
      return undefined;
    };
    try {
      await page.goto('/#library');
      await expect(page.getByRole('article')).toHaveCount(48);
      await page.getByRole('button', { name: 'Load more titles', exact: true }).click();
      await expect.poll(() => entered).toBe(true);
      const target = nextAction === 'search'
        ? page.getByRole('searchbox', { name: 'Search your library', exact: true })
        : page.getByRole('heading', { name: 'Settings', level: 1, exact: true });
      if (nextAction === 'navigate') await page.getByRole('navigation').getByRole('button', { name: 'Settings', exact: true }).click();
      else await target.focus();
      await expect(target).toBeFocused();
      const response = page.waitForResponse(response => response.url().endsWith('/api/rpc') && response.request().postDataJSON().method === 'library.list' && response.request().postDataJSON().params.page === 2);
      release(); await (await response).finished();
      if (nextAction === 'search') await expect(page.getByRole('article')).toHaveCount(total);
      await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
      await expect(target).toBeFocused();
    } finally { release(); }
  });
}

test('an intermediate page focuses the new record even when its title duplicates earlier cards', async ({ page, library }) => {
  seed(library, 'library');
  const template = [...library.entries.values()][0];
  for (let index = 0; index < 50; index += 1) {
    const item = media(`anilist:another-page-${index}`, 'Repeated title');
    library.entries.set(item.id, { ...template, mediaId: item.id, media: item });
  }
  for (const [id, entry] of library.entries) library.entries.set(id, { ...entry, media: { ...entry.media, title: 'Repeated title' } });
  await page.goto('/#library');
  const cards = page.getByRole('article');
  await expect(cards).toHaveCount(48);
  await page.getByRole('button', { name: 'Load more titles', exact: true }).click();
  await expect(cards).toHaveCount(96);
  await expect(cards.nth(48).getByRole('button', { name: 'Open Repeated title', exact: true })).toBeFocused();
  await expect(cards.first().getByRole('button', { name: 'Open Repeated title', exact: true })).not.toBeFocused();
  await expect(page.getByRole('button', { name: 'Load more titles', exact: true })).toBeEnabled();
});

test('retrying a failed final page focuses the first new title without duplicating previous cards', async ({ page, library }) => {
  const total = seed(library, 'library');
  await page.goto('/#library');
  const cards = page.getByRole('article');
  await expect(cards).toHaveCount(48);
  library.failNext('library.list', 'The last page is temporarily unavailable.');
  const loadMore = page.getByRole('button', { name: 'Load more titles', exact: true });
  await loadMore.click();
  await expect(page.getByRole('alert')).toContainText('The last page is temporarily unavailable.');
  await expect(loadMore).toBeFocused();
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(cards).toHaveCount(total);
  await expect(cards.nth(48).getByRole('button', { name: /^Open / })).toBeFocused();
  expect(library.requests.filter(request => request.method === 'library.list').map(request => request.params.page)).toEqual([1, 2, 2]);
});

for (const responseKind of ['empty', 'overlapping'] as const) {
  test(`a final ${responseKind} page preserves focus on the last retained title`, async ({ page, library }) => {
    seed(library, 'library');
    library.override = async ({ method, params }) => method === 'library.list' && params.page === 2
      ? { items: responseKind === 'overlapping' ? [...library.entries.values()].slice(0, 48) : [], page: 2, pageSize: 48, totalCount: 48, hasNextPage: false }
      : undefined;
    await page.goto('/#library');
    const cards = page.getByRole('article');
    await expect(cards).toHaveCount(48);
    await page.getByRole('button', { name: 'Load more titles', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Load more titles', exact: true })).toHaveCount(0);
    await expect(cards).toHaveCount(48);
    await expect(cards.last().getByRole('button', { name: /^Open / })).toBeFocused();
  });
}
