import { test, expect, media } from './fixtures';
import type { Locator, Page } from '@playwright/test';

const frieren = "Frieren: Beyond Journey's End";
const frierenId = 'anilist:154587';

async function openJournal(detail: Locator, editor: 'notes' | 'review' | 'organization') {
  const toggle = detail.locator(`button[aria-controls="${editor}-editor"]`);
  if (await toggle.getAttribute('aria-expanded') !== 'true') await toggle.click();
}

async function openDisplay(page: Page) {
  const toggle = page.getByRole('button', { name: /^(Display options|Filters and display)/ });
  if (await toggle.getAttribute('aria-expanded') !== 'true') await toggle.click();
}

test.beforeEach(async ({ page, library }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: `Open ${frieren}`, exact: true })).toBeVisible();
});

test('zero is a real rating and clearing sends null; both persist after a reload', async ({ page, library }) => {
  const rate = page.getByRole('button', { name: `Rate ${frieren}`, exact: true });
  await rate.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  const slider = dialog.getByRole('slider', { name: 'Your rating out of 10' });
  await slider.focus();
  await page.keyboard.press('Home');
  await expect(slider).toHaveValue('0');
  await expect(dialog.getByText('Terrible', { exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: 'Save rating' }).click();
  await expect(dialog).not.toBeVisible();
  await expect(rate).toContainText('0');
  expect(library.entries.get(frierenId)?.score).toBe(0);
  expect(library.requests.filter((request) => request.method === 'library.score').at(-1)?.params.score).toBe(0);
  await page.reload();
  await expect(rate).toContainText('0');
  await rate.click();
  await dialog.getByRole('button', { name: 'Clear', exact: true }).click();
  await expect(dialog.getByText('Not rated yet', { exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: 'Save rating' }).click();
  await expect(dialog).not.toBeVisible();
  expect(library.entries.get(frierenId)?.score).toBeNull();
  expect(library.requests.filter((request) => request.method === 'library.score').at(-1)?.params.score).toBeNull();
  await page.reload();
  await expect(rate).toContainText('—');
});

test('an unrated title can choose zero without confusing it with clearing', async ({ page, library }) => {
  const title = 'Demon Slayer: Kimetsu no Yaiba';
  const rate = page.getByRole('button', { name: `Rate ${title}`, exact: true });
  await rate.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  await expect(dialog.getByText('Not rated yet', { exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: 'Decrease rating', exact: true }).click();
  await expect(dialog.getByRole('slider', { name: 'Your rating out of 10' })).toHaveAttribute('aria-valuetext', '0 out of 10, Terrible');
  await dialog.getByRole('button', { name: 'Save rating' }).click();
  await expect(dialog).not.toBeVisible();
  await expect(rate).toContainText('0');
  expect(library.entries.get('anilist:101922')?.score).toBe(0);
});

test('failed rating keeps the dialog open and the saved value unchanged, then supports retry', async ({ page, library }) => {
  library.failNext('library.score', 'The library could not save this rating.');
  const rate = page.getByRole('button', { name: `Rate ${frieren}`, exact: true });
  await rate.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  await dialog.getByRole('slider', { name: 'Your rating out of 10' }).press('ArrowLeft');
  await dialog.getByRole('button', { name: 'Save rating' }).click();
  await expect(dialog.getByRole('alert')).toHaveText('The library could not save this rating.');
  expect(library.entries.get(frierenId)?.score).toBe(9);
  await expect(rate).toContainText('9');
  await dialog.getByRole('button', { name: 'Save rating' }).click();
  await expect(dialog).not.toBeVisible();
  await expect(rate).toContainText('8');
  expect(library.entries.get(frierenId)?.score).toBe(8);
});

test('rating dialog traps keyboard focus and Escape restores the trigger', async ({ page }) => {
  const rate = page.getByRole('button', { name: `Rate ${frieren}`, exact: true });
  await rate.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  await expect(dialog).toBeVisible();
  for (let index = 0; index < 12; index += 1) {
    await page.keyboard.press('Tab');
    expect(await dialog.evaluate((node) => node.contains(document.activeElement)), 'Tab must stay inside the modal').toBe(true);
  }
  for (let index = 0; index < 12; index += 1) {
    await page.keyboard.press('Shift+Tab');
    expect(await dialog.evaluate((node) => node.contains(document.activeElement)), 'Shift+Tab must stay inside the modal').toBe(true);
  }
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(rate).toBeFocused();
});

test('title rating modal restores its trigger and Back restores the collection trigger', async ({ page, library }) => {
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  const rate = detail.getByRole('button', { name: `Rate ${frieren}`, exact: true });
  await rate.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  await dialog.getByRole('slider', { name: 'Your rating out of 10' }).press('ArrowLeft');
  expect(library.entries.get(frierenId)?.score).toBe(9);
  await dialog.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(rate).toContainText('8');
  await expect(rate).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(detail).toBeVisible();
  await detail.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(detail).not.toBeVisible();
  await expect(page.getByRole('button', { name: `Open ${frieren}`, exact: true })).toBeFocused();
});

test('progress is reflected in the card and persisted after a reload', async ({ page, library }) => {
  const card = page.getByRole('article', { name: frieren, exact: true });
  await expect(card).toContainText('EP 3');
  await page.getByRole('button', { name: `Add episode to ${frieren}` }).click();
  await expect(card).toContainText('EP 4');
  expect(library.entries.get(frierenId)?.progress).toBe(4);
  await page.reload();
  await expect(card).toContainText('EP 4');
});

test('Unicode personal notes save intact and remain searchable after a reload', async ({ page, library }) => {
  const notes = 'Оцінка історії: дуже приємна 🌙\n次のエピソード — Praha, čtvrtek.';
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await openJournal(detail, 'notes');
  await detail.getByRole('textbox', { name: 'Personal notes' }).fill(notes);
  await detail.getByRole('button', { name: 'Save notes', exact: true }).click();
  await expect(detail.getByRole('button', { name: 'Save notes', exact: true })).toBeDisabled();
  expect(library.entries.get(frierenId)?.notes).toBe(notes);
  expect(library.requests.filter((request) => request.method === 'library.notes').at(-1)?.params.notes).toBe(notes);
  await detail.getByRole('button', { name: 'Back', exact: true }).click();
  await page.reload();
  await page.getByRole('searchbox', { name: 'Search your library' }).fill('🌙');
  await expect(page.getByRole('button', { name: `Open ${frieren}`, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Open Demon Slayer: Kimetsu no Yaiba', exact: true })).not.toBeVisible();
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  await openJournal(detail, 'notes');
  await expect(detail.getByRole('textbox', { name: 'Personal notes' })).toHaveValue(notes);
});

test('a failed status change restores the saved selection and leaves details usable', async ({ page, library }) => {
  library.failNext('library.status', 'Could not save the new status.');
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  const status = detail.getByRole('combobox', { name: 'Library status' });
  await expect(status).toBeEnabled();
  await status.selectOption('Completed');
  await expect(detail.getByRole('alert')).toContainText('Could not save the new status.');
  await expect(status).toHaveValue('Watching');
  expect(library.entries.get(frierenId)?.status).toBe('Watching');
  await status.selectOption('Completed');
  await expect(status).toHaveValue('Completed');
  expect(library.entries.get(frierenId)?.status).toBe('Completed');
});

test('media type switches show manga entries with reading and chapter controls', async ({ page, library }) => {
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Open Berserk', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: `Open ${frieren}`, exact: true })).not.toBeVisible();
  await expect(page.getByRole('button', { name: 'Reading', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Add chapter to Berserk' })).toBeVisible();
  expect(library.requests.filter((request) => request.method === 'library.list').at(-1)?.params.type).toBe('Manga');
});

test('catalog adds a new title to the library', async ({ page, library }) => {
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Discover' }).click();
  await expect(page.getByRole('heading', { name: 'Discover', exact: true })).toBeVisible();
  await page.getByRole('searchbox', { name: 'Search the catalog' }).fill('Odd Taxi');
  await expect(page.getByRole('button', { name: 'Add Odd Taxi to library' })).toBeVisible();
  await page.getByRole('button', { name: 'Add Odd Taxi to library' }).click();
  await expect.poll(() => library.entries.get('anilist:128547')?.status).toBe('Planning');
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'My library' }).click();
  await expect(page.getByRole('button', { name: 'Open Odd Taxi', exact: true })).toBeVisible();
  await expect(page.getByRole('article', { name: 'Odd Taxi', exact: true })).toContainText('Planning');
});

test('catalog tracking includes a saved title outside the first library page', async ({ page, library }) => {
  const seed = library.entries.get(frierenId)!;
  for (let index = 0; index < 50; index += 1) {
    const item = media(`anilist:lookup-${index}`, `Lookup Story ${index}`);
    library.catalog.set(item.id, item);
    library.entries.set(item.id, { ...seed, mediaId: item.id, media: item });
  }
  const oddTaxi = library.catalog.get('anilist:128547')!;
  library.entries.set(oddTaxi.id, { ...seed, mediaId: oddTaxi.id, media: oddTaxi, status: 'Completed', progress: 13, score: 7, updatedAt: '2026-10-01T12:00:00Z' });
  await page.reload();
  await expect(page.getByRole('article')).toHaveCount(48);
  await expect(page.getByRole('button', { name: 'Open Odd Taxi', exact: true })).not.toBeVisible();
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Discover', exact: true }).click();
  const card = page.getByRole('article', { name: 'Odd Taxi', exact: true });
  await expect(card.getByRole('button', { name: 'Rate Odd Taxi', exact: true })).toContainText('7');
  await expect(card.locator('.status-badge')).toHaveText('Completed');
  await expect(card.locator('.progress-text')).toHaveText(/^EP 13\s*\/\s*13$/);
  await expect(card.getByRole('button', { name: 'Add Odd Taxi to library', exact: true })).not.toBeVisible();
  expect(library.requests.filter(request => request.method === 'library.lookup').at(-1)?.params.mediaIds).toContain(oddTaxi.id);
});

for (const transfer of ['sync', 'import'] as const) {
  test(`Settings ${transfer} refreshes existing catalog tracking before returning to Discover`, async ({ page, library }) => {
    library.settings = { ...library.settings, isAniListConnected: true, aniListUsername: 'Fixture user' };
    const updated = { ...library.entries.get(frierenId)!, score: 6, progress: 12, status: 'Paused' as const, updatedAt: '2026-10-03T09:00:00Z' };
    library.override = async ({ method }) => {
      if (method === 'library.import.preview' && transfer === 'import') return { items: [updated], count: 1 };
      if (method !== (transfer === 'sync' ? 'anilist.sync' : 'library.import.apply')) return undefined;
      library.entries.set(frierenId, updated);
      library.settings = { ...library.settings, aniListLastSyncAt: updated.updatedAt };
      return transfer === 'sync' ? { importedCount: 1, settings: library.settings } : { importedCount: 1 };
    };
    await page.reload();
    const navigation = page.getByRole('navigation', { name: 'Main navigation' });
    await navigation.getByRole('button', { name: 'Discover', exact: true }).click();
    const card = page.getByRole('article', { name: frieren, exact: true });
    await expect(card.getByRole('button', { name: `Rate ${frieren}`, exact: true })).toContainText('9');
    await expect(card.locator('.progress-text')).toHaveText(/^EP 3\s*\/\s*28$/);
    await expect(card.locator('.status-badge')).toHaveText('Watching');
    await navigation.getByRole('button', { name: 'Settings', exact: true }).click();
    if (transfer === 'sync') {
      await page.getByRole('button', { name: 'Import from AniList', exact: true }).click();
      await page.getByRole('button', { name: 'Import now', exact: true }).click();
    }
    else {
      await page.getByLabel('Import MAL XML', { exact: true }).setInputFiles({ name: 'updated-list.xml', mimeType: 'application/xml', buffer: Buffer.from('<myanimelist></myanimelist>') });
      await page.getByRole('button', { name: 'Import 1 titles', exact: true }).click();
    }
    await expect(page.getByRole('status').filter({ hasText: 'Imported 1 titles' })).toBeVisible();
    const lookupsBeforeReturn = library.requests.filter(request => request.method === 'library.lookup').length;
    await navigation.getByRole('button', { name: 'Discover', exact: true }).click();
    await expect(card.getByRole('button', { name: `Rate ${frieren}`, exact: true })).toContainText('6');
    await expect(card.locator('.progress-text')).toHaveText(/^EP 12\s*\/\s*28$/);
    await expect(card.locator('.status-badge')).toHaveText('Paused');
    expect(library.requests.filter(request => request.method === 'library.lookup').length).toBeGreaterThan(lookupsBeforeReturn);
  });
}

test('a confirmed title removal finishing after navigation keeps the current Settings page', async ({ page, library }) => {
  let finishRemoval!: () => void;
  const removalReady = new Promise<void>(resolve => { finishRemoval = resolve; });
  library.override = async ({ method, params }) => {
    if (method !== 'library.remove') return undefined;
    await removalReady;
    return { removed: library.entries.delete(String(params.mediaId)) };
  };
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await detail.getByRole('button', { name: 'Remove from library', exact: true }).click();
  expect(library.requests.some(request => request.method === 'library.remove')).toBe(false);
  await detail.getByRole('button', { name: 'Keep title', exact: true }).click();
  await expect(detail.getByRole('button', { name: 'Remove title', exact: true })).not.toBeVisible();
  await detail.getByRole('button', { name: 'Remove from library', exact: true }).click();
  await detail.getByRole('button', { name: 'Remove title', exact: true }).click();
  await expect.poll(() => library.requests.some(request => request.method === 'library.remove')).toBe(true);
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
  finishRemoval();
  await expect(page.getByRole('status').filter({ hasText: 'Removed from your library' })).toBeVisible();
  await expect(page).toHaveURL(/#settings$/);
  await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
  await expect(detail).not.toBeVisible();
  expect(library.entries.has(frierenId)).toBe(false);
});

test('catalog search coalesces queued queries and only the latest result wins', async ({ page, library }) => {
  let releaseOld!: () => void;
  const oldReady = new Promise<void>((resolve) => { releaseOld = resolve; });
  library.override = async ({ method, params }) => {
    if (method !== 'catalog.search' || params.search !== 'old') return undefined;
    await oldReady;
    return { items: [media('anilist:10001', 'Old search result')], page: 1, pageSize: 24, totalCount: 1, hasNextPage: false };
  };
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Discover' }).click();
  const search = page.getByRole('searchbox', { name: 'Search the catalog' });
  await expect(page.getByRole('button', { name: 'Open Odd Taxi', exact: true })).toBeVisible();
  await page.clock.install();
  await search.fill('old');
  await page.clock.runFor(300);
  await expect.poll(() => library.requests.some((request) => request.method === 'catalog.search' && request.params.search === 'old')).toBe(true);
  await search.fill('intermediate');
  await page.clock.runFor(300);
  expect(library.requests.some((request) => request.method === 'catalog.search' && request.params.search === 'intermediate')).toBe(false);
  await search.fill('Odd');
  await page.clock.runFor(300);
  expect(library.requests.some((request) => request.method === 'catalog.search' && request.params.search === 'Odd')).toBe(false);
  const staleResponse = page.waitForResponse((response) => response.url().endsWith('/api/rpc') && response.request().postDataJSON().params.search === 'old');
  releaseOld();
  await (await staleResponse).finished();
  await expect(page.getByRole('button', { name: 'Open Odd Taxi', exact: true })).toBeVisible();
  // Let fetch and Svelte finish rendering the old response before asserting it was ignored.
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(page.getByRole('button', { name: 'Open Odd Taxi', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Open Old search result', exact: true })).not.toBeVisible();
  expect(library.requests.filter((request) => request.method === 'catalog.search' && request.params.search).map((request) => request.params.search)).toEqual(['old', 'Odd']);
});

test('changing a filtered title status refills the page and pagination keeps every other title', async ({ page, library }) => {
  const seed = library.entries.get(frierenId)!;
  for (let index = 0; index < 52; index += 1) {
    const item = media(`anilist:fixture-${index}`, `Fixture Story ${index}`);
    library.catalog.set(item.id, item);
    library.entries.set(item.id, { ...seed, mediaId: item.id, media: item, score: null });
  }
  await page.reload();
  await expect(page.getByRole('article')).toHaveCount(48);
  await page.getByRole('button', { name: 'Watching / Reading', exact: true }).click();
  await expect(page.getByRole('article')).toHaveCount(48);
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await detail.getByRole('combobox', { name: 'Library status' }).selectOption('Completed');
  await expect(detail.getByRole('combobox', { name: 'Library status' })).toHaveValue('Completed');
  await detail.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByRole('button', { name: `Open ${frieren}`, exact: true })).not.toBeVisible();
  await expect(page.getByRole('article')).toHaveCount(48);
  await page.getByRole('button', { name: 'Load more titles', exact: true }).click();
  await expect(page.getByRole('article')).toHaveCount(55);
  const titles = await page.getByRole('article').evaluateAll((cards) => cards.map((card) => card.getAttribute('aria-label')));
  expect(new Set(titles).size).toBe(55);
  await expect(page.getByRole('button', { name: 'Load more titles', exact: true })).not.toBeVisible();
});

test('editing matching notes refreshes search membership without skipping the next page', async ({ page, library }) => {
  const seed = { ...library.entries.get(frierenId)!, notes: 'shared phrase' };
  library.entries.set(frierenId, seed);
  for (let index = 0; index < 52; index += 1) {
    const item = media(`anilist:notes-${index}`, `Notes Story ${index}`);
    library.catalog.set(item.id, item);
    library.entries.set(item.id, { ...seed, mediaId: item.id, media: item, score: null });
  }
  await page.reload();
  await page.getByRole('searchbox', { name: 'Search your library' }).fill('shared phrase');
  await expect(page.getByRole('article')).toHaveCount(48);
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await openJournal(detail, 'notes');
  await detail.getByRole('textbox', { name: 'Personal notes' }).fill('A different note');
  await detail.getByRole('button', { name: 'Save notes', exact: true }).click();
  await expect(detail.getByRole('button', { name: 'Save notes', exact: true })).toBeDisabled();
  await detail.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByRole('button', { name: `Open ${frieren}`, exact: true })).not.toBeVisible();
  await expect(page.getByRole('article')).toHaveCount(48);
  await page.getByRole('button', { name: 'Load more titles', exact: true }).click();
  await expect(page.getByRole('article')).toHaveCount(52);
  const titles = await page.getByRole('article').evaluateAll((cards) => cards.map((card) => card.getAttribute('aria-label')));
  expect(new Set(titles).size).toBe(52);
});

test('a failed saved cover recovers when catalog details provide a refreshed poster', async ({ page, library }) => {
  const saved = library.entries.get(frierenId)!;
  library.entries.set(frierenId, { ...saved, media: { ...saved.media, posterUrl: 'data:image/png;base64,invalid' } });
  let releaseDetails!: () => void;
  const detailsReady = new Promise<void>((resolve) => { releaseDetails = resolve; });
  library.override = async ({ method, params }) => {
    if (method !== 'media.detail' || params.mediaId !== frierenId) return undefined;
    await detailsReady;
    return library.catalog.get(frierenId);
  };
  await page.reload();
  const card = page.getByRole('article', { name: frieren, exact: true });
  await expect(card).toBeVisible();
  await expect(card.getByRole('img', { name: `Cover of ${frieren}` })).toHaveCount(0);
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await expect(detail).toBeVisible();
  await expect(detail.getByRole('img', { name: `Cover of ${frieren}` })).toHaveCount(0);
  releaseDetails();
  const refreshedCover = detail.getByRole('img', { name: `Cover of ${frieren}` });
  await expect(refreshedCover).toBeVisible();
  await expect.poll(() => refreshedCover.evaluate((image) => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
});

test('cover size increases poster dimensions and survives a reload', async ({ page }) => {
  const cover = page.getByRole('button', { name: `Open ${frieren}`, exact: true });
  await openDisplay(page);
  const slider = page.getByRole('slider', { name: 'Cover size', exact: true });
  await slider.focus();
  await page.keyboard.press('Home');
  await expect(slider).toHaveValue('140');
  const smallWidth = (await cover.boundingBox())?.width ?? 0;
  await page.keyboard.press('End');
  await expect(slider).toHaveValue('220');
  await expect.poll(async () => (await cover.boundingBox())?.width ?? 0).toBeGreaterThan(smallWidth + 30);
  await page.reload();
  await openDisplay(page);
  await expect(slider).toHaveValue('220');
});

test('list cover size changes poster dimensions on desktop and a 360px window', async ({ page }) => {
  await page.getByRole('button', { name: 'List view', exact: true }).click();
  const cover = page.getByRole('button', { name: `Open ${frieren}`, exact: true });
  await openDisplay(page);
  const slider = page.getByRole('slider', { name: 'Cover size', exact: true });
  for (const width of [1440, 360]) {
    await page.setViewportSize({ width, height: 1000 });
    await slider.focus();
    await page.keyboard.press('Home');
    await expect(slider).toHaveValue('140');
    const smallWidth = (await cover.boundingBox())?.width ?? 0;
    await page.keyboard.press('End');
    await expect(slider).toHaveValue('220');
    await expect.poll(async () => (await cover.boundingBox())?.width ?? 0).toBeGreaterThan(smallWidth + 20);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth), 'Sized list covers must not overflow the page').toBeLessThanOrEqual(1);
  }
});

test('legacy cover preferences migrate once into compact version 2 preferences', async ({ page }) => {
  await page.evaluate(() => localStorage.setItem('tanahub-view', JSON.stringify({ layout: 'list', coverSize: 300 })));
  await page.reload();
  await openDisplay(page);
  const slider = page.getByRole('slider', { name: 'Cover size', exact: true });
  await expect(slider).toHaveValue('220');
  await expect(page.getByRole('button', { name: 'List view', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect.poll(() => page.evaluate(() => JSON.parse(localStorage.getItem('tanahub-view') ?? '{}').version)).toBe(2);
  await slider.focus();
  await page.keyboard.press('Home');
  await expect(slider).toHaveValue('140');
  await page.reload();
  await openDisplay(page);
  await expect(slider).toHaveValue('140');
});

test('full title tracking supports absolute progress, maximum, reset, review and organization with reload persistence', async ({ page, library }) => {
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await expect(detail.getByRole('heading', { name: frieren, exact: true })).toBeFocused();
  await expect(detail.getByRole('heading', { name: 'Characters', exact: true })).toBeVisible();
  await expect(detail.getByText('MADHOUSE', { exact: true })).toBeVisible();
  const progress = detail.getByRole('spinbutton', { name: 'Set progress', exact: true });
  await progress.fill('11');
  await detail.getByRole('button', { name: 'Set progress', exact: true }).click();
  await expect.poll(() => library.entries.get(frierenId)?.progress).toBe(11);
  await detail.getByText('More progress actions', { exact: true }).click();
  await detail.getByRole('button', { name: 'Set to 28', exact: true }).click();
  await expect(progress).toHaveValue('28');
  await expect(detail.getByRole('button', { name: '+1 episode', exact: true })).toBeDisabled();
  await detail.getByRole('button', { name: 'Reset', exact: true }).click();
  await detail.getByRole('button', { name: 'Reset progress', exact: true }).click();
  await expect(progress).toHaveValue('0');
  await detail.getByRole('button', { name: '+1 episode', exact: true }).click();
  await expect(progress).toHaveValue('1');
  const review = 'Прекрасна історія 🌙 — また見たい。';
  await openJournal(detail, 'review');
  await detail.getByRole('textbox', { name: 'Review', exact: true }).fill(review);
  await detail.getByRole('button', { name: 'Save review', exact: true }).click();
  await expect(detail.getByRole('button', { name: 'Save review', exact: true })).toBeDisabled();
  await openJournal(detail, 'organization');
  await detail.getByRole('textbox', { name: 'Tags', exact: true }).fill('comfort, weekend, comfort');
  await openJournal(detail, 'organization');
  await detail.getByRole('textbox', { name: 'Custom lists', exact: true }).fill('Friday queue, Favourites');
  await detail.getByRole('button', { name: 'Save organization', exact: true }).click();
  await expect(detail.getByRole('button', { name: 'Save organization', exact: true })).toBeDisabled();
  expect(library.entries.get(frierenId)?.tags).toEqual(['comfort', 'weekend']);
  expect(library.entries.get(frierenId)?.customLists).toEqual(['Friday queue', 'Favourites']);
  await page.reload();
  await expect(progress).toHaveValue('1');
  await openJournal(detail, 'review');
  await expect(detail.getByRole('textbox', { name: 'Review', exact: true })).toHaveValue(review);
  await openJournal(detail, 'organization');
  await expect(detail.getByRole('textbox', { name: 'Tags', exact: true })).toHaveValue('comfort, weekend');
  await expect(detail.getByRole('textbox', { name: 'Custom lists', exact: true })).toHaveValue('Friday queue, Favourites');
  await detail.getByRole('button', { name: 'Back', exact: true }).click();
  await openDisplay(page);
  await page.getByRole('combobox', { name: 'Filter by tag', exact: true }).selectOption('weekend');
  await expect(page.getByRole('article')).toHaveCount(1);
  await expect(page.getByRole('article', { name: frieren, exact: true })).toBeVisible();
});

test('failed absolute progress retains the edit for retry without changing saved progress', async ({ page, library }) => {
  library.failNext('library.progress.set', 'Progress could not be saved.');
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  const input = detail.getByRole('spinbutton', { name: 'Set progress', exact: true });
  await input.fill('7');
  await detail.getByRole('button', { name: 'Set progress', exact: true }).click();
  await expect(detail.getByRole('alert')).toContainText('Progress could not be saved.');
  await expect(input).toHaveValue('7');
  expect(library.entries.get(frierenId)?.progress).toBe(3);
  await detail.getByRole('button', { name: 'Set progress', exact: true }).click();
  await expect.poll(() => library.entries.get(frierenId)?.progress).toBe(7);
});

test('failed review and organization saves preserve drafts and allow retry', async ({ page, library }) => {
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  library.failNext('library.review', 'Review is still unsaved.');
  await openJournal(detail, 'review');
  const review = detail.getByRole('textbox', { name: 'Review', exact: true });
  await review.fill('Remember this ending.');
  await detail.getByRole('button', { name: 'Save review', exact: true }).click();
  await expect(detail.getByRole('alert')).toHaveText('Review is still unsaved.');
  await expect(review).toHaveValue('Remember this ending.');
  expect(library.entries.get(frierenId)?.review).toBeNull();
  await detail.getByRole('button', { name: 'Save review', exact: true }).click();
  await expect.poll(() => library.entries.get(frierenId)?.review).toBe('Remember this ending.');
  library.failNext('library.organization', 'Lists could not be saved.');
  await openJournal(detail, 'organization');
  await detail.getByRole('textbox', { name: 'Custom lists', exact: true }).fill('Weekend');
  await detail.getByRole('button', { name: 'Save organization', exact: true }).click();
  await expect(detail.getByRole('alert')).toHaveText('Lists could not be saved.');
  await expect(detail.getByRole('textbox', { name: 'Custom lists', exact: true })).toHaveValue('Weekend');
  expect(library.entries.get(frierenId)?.customLists ?? []).toEqual([]);
  await detail.getByRole('button', { name: 'Save organization', exact: true }).click();
  await expect.poll(() => library.entries.get(frierenId)?.customLists).toEqual(['Weekend']);
});

test('relations wait for full details, retain cached cards after failure and retry before related navigation', async ({ page, library }) => {
  let releaseDetails!: () => void;
  const ready = new Promise<void>(resolve => { releaseDetails = resolve; });
  library.override = async ({ method, params }) => {
    if (method !== 'media.detail' || params.mediaId !== frierenId) return undefined;
    await ready;
    return library.catalog.get(frierenId);
  };
  library.relations.set(frierenId, { ...library.relations.get(frierenId)!, notice: 'Showing saved connections.', isStale: true });
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await expect(detail.getByText('Loading full details…', { exact: true })).toBeVisible();
  expect(library.requests.some(request => request.method === 'media.related')).toBe(false);
  releaseDetails();
  const related = detail.getByRole('button', { name: /^Open Frieren: The Next Journey,/ });
  await expect(related).toBeVisible();
  await expect(detail.getByText('Showing saved connections.', { exact: true })).toBeVisible();
  library.failNext('media.related', 'Relations are temporarily unavailable.');
  await detail.getByRole('button', { name: 'Refresh related titles', exact: true }).click();
  await expect(detail.getByRole('alert')).toContainText('Relations are temporarily unavailable.');
  await expect(related).toBeVisible();
  library.relations.set(frierenId, { ...library.relations.get(frierenId)!, notice: null, isStale: false });
  await detail.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(detail.getByText('Showing saved connections.', { exact: true })).not.toBeVisible();
  expect(library.requests.filter(request => request.method === 'media.related').at(-1)?.params.forceRefresh).toBe(true);
  await related.click();
  await expect(page.getByRole('heading', { name: 'Frieren: The Next Journey', exact: true })).toBeFocused();
  await expect(page).toHaveURL(/#title\/anilist%3Afixture-sequel$/);
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(detail).toBeVisible();
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeVisible();
});

test('large relations groups start collapsed and personal entries stay ahead of related titles', async ({ page, library }) => {
  const relation = library.relations.get(frierenId)!.titles[0];
  library.relations.set(frierenId, { titles: Array.from({ length: 7 }, (_, index) => ({ ...relation, mediaId: `anilist:parts-${index}`, title: `Next Journey ${index}` })), notice: null, isStale: false });
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  const group = detail.locator('details').filter({ has: page.locator('summary', { hasText: 'Previous & next parts' }) });
  await expect(group).toHaveJSProperty('open', false);
  await expect(detail.getByRole('button', { name: /^Open Next Journey 0,/ })).not.toBeVisible();
  const journal = await detail.getByRole('heading', { name: 'Your space', exact: true }).boundingBox();
  const relations = await detail.getByRole('heading', { name: 'Related titles', exact: true }).boundingBox();
  expect(journal!.y).toBeLessThan(relations!.y);
  await group.locator('summary').click();
  await expect(detail.getByRole('button', { name: /^Open Next Journey 0,/ })).toBeVisible();
});

test('a direct title route survives reload and Back returns to the library', async ({ page }) => {
  await page.goto(`/#title/${encodeURIComponent(frierenId)}`);
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await expect(detail.getByRole('heading', { name: frieren, exact: true })).toBeFocused();
  await page.reload();
  await expect(detail).toBeVisible();
  await expect(detail.getByRole('spinbutton', { name: 'Set progress', exact: true })).toHaveValue('3');
  await detail.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page).toHaveURL(/#library$/);
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeVisible();
});

test('a full title uses compact responsive posters and displays saved start and completion dates', async ({ page, library }) => {
  library.entries.set(frierenId, { ...library.entries.get(frierenId)!, startedAt: '2026-09-01T12:00:00Z', completedAt: '2026-10-01T12:00:00Z' });
  await page.getByRole('button', { name: `Open ${frieren}`, exact: true }).click();
  const detail = page.getByRole('article', { name: frieren, exact: true }).filter({ has: page.getByRole('heading', { name: frieren, exact: true }) });
  await expect(detail.locator('.dates')).toContainText('Started');
  await expect(detail.locator('.dates')).toContainText('Completed');
  await expect(detail.locator('.dates')).toContainText('2026');
  for (const width of [1440, 360]) {
    await page.setViewportSize({ width, height: 1000 });
    const poster = await detail.locator('.title-poster').boundingBox();
    expect(poster?.width).toBeCloseTo(width === 360 ? 100 : 140, 0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(1);
    await expect(detail.getByRole('spinbutton', { name: 'Set progress', exact: true })).toBeEnabled();
  }
});

for (const layout of ['Grid view', 'List view']) {
  test(`360px layout: ${layout} has readable covers, usable rating, and no page overflow`, async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 800 });
    await page.getByRole('button', { name: layout, exact: true }).click();
    const cover = page.getByRole('button', { name: `Open ${frieren}`, exact: true });
    const bounds = await cover.boundingBox();
    expect(bounds?.width).toBeGreaterThanOrEqual(layout === 'Grid view' ? 140 : 56);
    expect(bounds?.height).toBeGreaterThanOrEqual(layout === 'Grid view' ? 210 : 84);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth), 'No horizontal page overflow').toBeLessThanOrEqual(1);
    await page.getByRole('button', { name: `Rate ${frieren}`, exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
    const dialogBounds = await dialog.boundingBox();
    expect(dialogBounds?.x).toBeGreaterThanOrEqual(0);
    expect((dialogBounds?.x ?? 0) + (dialogBounds?.width ?? 0)).toBeLessThanOrEqual(360);
    await dialog.getByRole('slider', { name: 'Your rating out of 10' }).press('ArrowLeft');
    await dialog.getByRole('button', { name: 'Save rating' }).click();
    await expect(dialog).not.toBeVisible();
    await expect(page.getByRole('button', { name: `Rate ${frieren}`, exact: true })).toContainText('8');
  });
}
