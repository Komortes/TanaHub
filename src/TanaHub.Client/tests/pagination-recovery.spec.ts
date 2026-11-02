import type { Locator, Page } from '@playwright/test';
import { test, expect, media, type MockLibrary } from './fixtures';

function seed(library: MockLibrary, collection: 'library' | 'discover') {
  const template = [...library.entries.values()][0];
  library.entries.clear();
  library.catalog.clear();
  for (let index = 0; index < 54; index++) {
    const item = media(`anilist:footer-recovery-${index}`, `Recovery title ${index}`);
    library.catalog.set(item.id, item);
    if (collection === 'library') library.entries.set(item.id, { ...template, mediaId: item.id, media: item });
  }
}

async function expectFullyVisible(page: Page, control: Locator) {
  const rect = await control.boundingBox();
  expect(rect).not.toBeNull();
  const viewport = page.viewportSize()!;
  expect.soft(rect!.y, 'The complete footer error should be visible above the retry action').toBeGreaterThanOrEqual(-1);
  expect.soft(rect!.y + rect!.height).toBeLessThanOrEqual(viewport.height + 1);
  expect.soft(rect!.x).toBeGreaterThanOrEqual(-1);
  expect.soft(rect!.x + rect!.width).toBeLessThanOrEqual(viewport.width + 1);
}

for (const scenario of [
  { collection: 'library', width: 1440, pageSize: 48, method: 'library.list' },
  { collection: 'discover', width: 320, pageSize: 24, method: 'catalog.search' },
] as const) {
  test(`${scenario.collection} pagination failures stay visible at ${scenario.width}px and retry the same page`, async ({ page, library }, testInfo) => {
    await page.setViewportSize({ width: scenario.width, height: 900 });
    seed(library, scenario.collection);
    await page.goto(`/#${scenario.collection}`);
    const cards = page.getByRole('article');
    await expect(cards).toHaveCount(scenario.pageSize);
    const retained = await cards.evaluateAll(items => items.map(item => item.getAttribute('data-media-id')));
    const loadMore = page.getByRole('button', { name: 'Load more titles', exact: true });
    await loadMore.scrollIntoViewIfNeeded();
    library.failNext(scenario.method, 'The next page is temporarily unavailable.');
    await loadMore.click();

    const alert = page.getByRole('alert');
    const retry = page.getByRole('button', { name: 'Try again', exact: true });
    await expect(alert).toContainText('The next page is temporarily unavailable.');
    await expect(cards).toHaveCount(scenario.pageSize);
    expect(await cards.evaluateAll(items => items.map(item => item.getAttribute('data-media-id')))).toEqual(retained);
    await page.screenshot({ path: testInfo.outputPath('failed-page-footer.png') });
    await expectFullyVisible(page, alert);
    await expect.soft(retry).toBeFocused();
    await expect(retry).toHaveCount(1);
    await expect.soft(loadMore).toHaveCount(0);

    library.failNext(scenario.method, 'The same page is still unavailable.');
    await retry.press('Enter');
    await expect(alert).toContainText('The same page is still unavailable.');
    await expect(cards).toHaveCount(scenario.pageSize);
    await expect(retry).toHaveCount(1);
    await expect(retry).toBeFocused();
    await expectFullyVisible(page, alert);
    expect(library.requests.filter(request => request.method === scenario.method).map(request => request.params.page)).toEqual([1, 2, 2]);

    await retry.click();
    await expect(cards).toHaveCount(Math.min(54, scenario.pageSize * 2));
    await expect(alert).toHaveCount(0);
    await expect(cards.nth(scenario.pageSize).getByRole('button', { name: /^Open / })).toBeFocused();
    const allIds = await cards.evaluateAll(items => items.map(item => item.getAttribute('data-media-id')));
    expect(new Set(allIds).size).toBe(allIds.length);
    expect(allIds.slice(0, scenario.pageSize)).toEqual(retained);
    expect(library.requests.filter(request => request.method === scenario.method).map(request => request.params.page)).toEqual([1, 2, 2, 2]);
  });
}
