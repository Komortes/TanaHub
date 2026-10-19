import { readFile } from 'node:fs/promises';
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { scheduleWindow } from '../src/lib/pages/schedule-time';

const frieren = "Frieren: Beyond Journey's End";
const frierenId = 'anilist:154587';
const tinyPng = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=';
const image = { name: 'Оцінка.png', mimeType: 'image/png', buffer: Buffer.from(tinyPng, 'base64') };

async function navigate(page: Page, name: string) {
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name, exact: true }).click();
  await expect(page.getByRole('heading', { name, exact: true, level: 1 })).toBeVisible();
}

test('Home statistics follow the selected media type and tracking actions refresh the dashboard', async ({ page, library }) => {
  await page.goto('/');
  await navigate(page, 'Home');
  await expect(page.getByText('4 titles tracked', { exact: true })).toBeVisible();
  const card = page.getByRole('region', { name: 'Continue tracking' }).getByRole('article', { name: frieren, exact: true });
  await card.getByRole('button', { name: `Add episode to ${frieren}`, exact: true }).click();
  await expect(card).toContainText('EP 4');
  expect(library.entries.get(frierenId)?.progress).toBe(4);
  await card.getByRole('button', { name: `Rate ${frieren}`, exact: true }).click();
  const rating = page.getByRole('dialog', { name: 'Your rating', exact: true });
  await rating.getByRole('slider', { name: 'Your rating out of 10', exact: true }).press('Home');
  await rating.getByRole('button', { name: 'Save rating' }).click();
  await expect(rating).not.toBeVisible();
  await expect(card.getByRole('button', { name: `Rate ${frieren}`, exact: true })).toContainText('0');
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Continue tracking' }).getByRole('article', { name: 'Berserk', exact: true })).toBeVisible();
  await expect(card).not.toBeVisible();
  expect(library.requests.filter(request => request.method === 'home.get').at(-1)?.params.type).toBe('Manga');
  await expect(page.getByRole('region', { name: 'Library statistics' })).toContainText('Chapters read');
});

test('Home loading errors can be retried without hiding the rest of the dashboard', async ({ page, library }) => {
  library.failNext('home.get', 'Dashboard storage is temporarily unavailable.');
  await page.goto('/');
  await navigate(page, 'Home');
  await expect(page.getByRole('alert')).toContainText('Dashboard storage is temporarily unavailable.');
  await expect(page.getByRole('region', { name: 'Recommendations' })).toBeVisible();
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByText('4 titles tracked', { exact: true })).toBeVisible();
  await expect(page.getByRole('alert')).not.toBeVisible();
});

test('failed Home progress keeps the saved count and allows retry', async ({ page, library }) => {
  library.failNext('library.progress', 'Your progress could not be saved.');
  await page.goto('/');
  await navigate(page, 'Home');
  const card = page.getByRole('region', { name: 'Continue tracking' }).getByRole('article', { name: frieren, exact: true });
  const progress = card.getByRole('button', { name: `Add episode to ${frieren}`, exact: true });
  await progress.click();
  await expect(page.getByRole('alert')).toContainText('Your progress could not be saved.');
  await expect(card).toContainText('EP 3');
  expect(library.entries.get(frierenId)?.progress).toBe(3);
  await progress.click();
  await expect(card).toContainText('EP 4');
  await expect(page.getByRole('alert')).not.toBeVisible();
});

test('a delayed Home response cannot replace the latest media type', async ({ page, library }) => {
  let release!: () => void;
  const ready = new Promise<void>(resolve => { release = resolve; });
  library.override = async ({ method, params }) => {
    if (method !== 'home.get' || params.type !== 'Anime') return undefined;
    await ready;
    return { insights: { totalEntries: 999, completedEntries: 0, backlogEntries: 0, droppedEntries: 0, completionRate: 0, droppedRatio: 0, averageScore: null, episodesWatched: 0, chaptersRead: 0, estimatedWatchMinutes: 0, byMediaType: [], byStatus: [] }, continueItems: [], recentItems: [] };
  };
  await page.goto('/');
  await navigate(page, 'Home');
  await expect(page.getByText('4 titles tracked', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Anime', exact: true }).click();
  await expect.poll(() => library.requests.some(request => request.method === 'home.get' && request.params.type === 'Anime')).toBe(true);
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
  const stale = page.waitForResponse(response => response.url().endsWith('/api/rpc') && response.request().postDataJSON().method === 'home.get' && response.request().postDataJSON().params.type === 'Anime');
  release();
  await (await stale).finished();
  await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
  await expect(page.getByText('999 titles tracked', { exact: true })).not.toBeVisible();
});

test('Schedule ranges respect daylight-saving time, selected timezone, and library membership', async ({ page, library }) => {
  await page.clock.install({ time: new Date('2026-10-24T10:00:00Z') });
  await page.goto('/');
  await navigate(page, 'Schedule');
  await page.getByRole('combobox', { name: 'Schedule timezone' }).selectOption('Europe/Prague');
  await page.getByRole('button', { name: 'Tomorrow', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Open Odd Taxi, episode 5', exact: true })).toBeVisible();
  const prague = library.requests.filter(request => request.method === 'schedule.list').at(-1)!.params;
  expect(prague.from).toBe('2026-10-24T22:00:00.000Z');
  expect(prague.to).toBe('2026-10-25T23:00:00.000Z');
  await page.getByRole('checkbox', { name: 'My library only', exact: true }).check();
  await expect(page.getByRole('button', { name: 'Open Odd Taxi, episode 5', exact: true })).not.toBeVisible();
  await expect(page.getByRole('button', { name: `Open ${frieren}, episode 4`, exact: true })).toBeVisible();
  expect(library.requests.filter(request => request.method === 'schedule.list').at(-1)?.params.libraryOnly).toBe(true);
  await page.getByRole('combobox', { name: 'Schedule timezone' }).selectOption('Asia/Tokyo');
  await expect(page.getByRole('button', { name: `Open ${frieren}, episode 4`, exact: true })).toContainText('01:00');
  const tokyo = library.requests.filter(request => request.method === 'schedule.list').at(-1)!.params;
  expect(tokyo.from).toBe('2026-10-24T15:00:00.000Z');
  expect(tokyo.to).toBe('2026-10-25T15:00:00.000Z');
});

test('the spring daylight-saving day has a 23-hour schedule window', () => {
  const window = scheduleWindow('Tomorrow', 'Europe/Prague', new Date('2026-03-28T10:00:00Z'));
  expect(window.from).toBe('2026-03-28T23:00:00.000Z');
  expect(window.to).toBe('2026-03-29T22:00:00.000Z');
});

test('Schedule error retries load readable episode rows', async ({ page, library }) => {
  library.failNext('schedule.list', 'The airing provider is unavailable.');
  await page.goto('/');
  await navigate(page, 'Schedule');
  await expect(page.getByRole('alert')).toContainText('The airing provider is unavailable.');
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByRole('alert')).not.toBeVisible();
  await page.getByRole('button', { name: 'This week', exact: true }).click();
  await expect(page.getByRole('button', { name: `Open ${frieren}, episode 4`, exact: true })).toBeVisible();
});

test('recognition sends bounded image data, displays episode confidence, and saves trace history', async ({ page, library }) => {
  await page.goto('/');
  await navigate(page, 'Recognize');
  await page.getByLabel('Choose recognition image').setInputFiles(image);
  await expect(page.getByRole('img', { name: 'Selected source for recognition' })).toBeVisible();
  await page.getByRole('button', { name: 'Find matches with trace.moe', exact: true }).click();
  const results = page.getByRole('region', { name: 'Recognition results', exact: true });
  await expect(results.getByRole('article', { name: `Match ${frieren}`, exact: true })).toContainText('96.0%');
  await expect(results).toContainText('EP 4');
  const request = library.requests.find(record => record.method === 'recognition.run')!;
  expect(request.params).toEqual({ provider: 'trace', imageBase64: tinyPng, mimeType: 'image/png', sourceName: image.name, uploadConfirmed: true });
  await expect(page.getByRole('region', { name: 'Recognition history', exact: true }).getByRole('article', { name: `Recognition history ${frieren}`, exact: true })).toBeVisible();
  expect(library.recognitionHistory.size).toBe(1);
  await results.getByRole('button', { name: 'Open title', exact: true }).click();
  await expect(page.getByRole('heading', { name: frieren, exact: true, level: 1 })).toBeVisible();
});

test('unsupported and oversized recognition files are rejected before contacting a provider', async ({ page, library }) => {
  await page.goto('/');
  await navigate(page, 'Recognize');
  await page.getByLabel('Choose recognition image').setInputFiles({ name: 'not-an-image.txt', mimeType: 'text/plain', buffer: Buffer.from('not an image') });
  await expect(page.getByRole('alert')).toContainText('Choose a JPG, PNG, or WebP image.');
  await page.getByLabel('Choose recognition image').setInputFiles({ name: 'too-large.png', mimeType: 'image/png', buffer: Buffer.alloc(5 * 1024 * 1024 + 1) });
  await expect(page.getByRole('alert')).toContainText('Choose an image under 5 MB.');
  expect(library.requests.some(request => request.method === 'recognition.run')).toBe(false);
  await expect(page.getByRole('button', { name: 'Find matches with trace.moe', exact: true })).toBeDisabled();
});

test('recognition failure retains the preview and supports retry', async ({ page, library }) => {
  library.failNext('recognition.run', 'The recognition provider could not respond.');
  await page.goto('/');
  await navigate(page, 'Recognize');
  await page.getByLabel('Choose recognition image').setInputFiles(image);
  const search = page.getByRole('button', { name: 'Find matches with trace.moe', exact: true });
  await search.click();
  await expect(page.getByRole('alert')).toContainText('The recognition provider could not respond.');
  await expect(page.getByRole('img', { name: 'Selected source for recognition' })).toBeVisible();
  await expect(search).toBeEnabled();
  await search.click();
  await expect(page.getByRole('region', { name: 'Recognition results', exact: true })).toContainText('96.0%');
  expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(2);
});

test('a failed recognition add reports the failure without claiming the title was added', async ({ page, library }) => {
  await page.goto('/');
  await navigate(page, 'Recognize');
  await page.getByLabel('Choose recognition image').setInputFiles(image);
  await page.getByRole('button', { name: 'Find matches with trace.moe', exact: true }).click();
  const match = page.getByRole('region', { name: 'Recognition results', exact: true }).getByRole('article', { name: `Match ${frieren}`, exact: true });
  library.failNext('library.add', 'This title could not be added.');
  await match.getByRole('button', { name: `Add ${frieren} to library`, exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('This title could not be added.');
  await expect(page.getByText('Added to your library.', { exact: true })).not.toBeVisible();
  await match.getByRole('button', { name: `Add ${frieren} to library`, exact: true }).click();
  await expect(page.getByText('Added to your library.', { exact: true })).toBeVisible();
});

test('art recognition opens the provided source and can save a match to history', async ({ page, library }) => {
  await page.addInitScript(() => { (window as any).openedSources = []; window.open = ((url: string) => { (window as any).openedSources.push(url); return null; }) as typeof window.open; });
  await page.goto('/');
  await navigate(page, 'Recognize');
  await page.getByRole('button', { name: /Artwork & manga/ }).click();
  await page.getByLabel('Choose recognition image').setInputFiles(image);
  await page.getByRole('button', { name: 'Find matches with SauceNAO', exact: true }).click();
  const match = page.getByRole('region', { name: 'Recognition results', exact: true }).getByRole('article', { name: `Match ${frieren}`, exact: true });
  await expect(match).toContainText('Fixture artist');
  await match.getByRole('button', { name: 'View source', exact: true }).click();
  expect(await page.evaluate(() => (window as any).openedSources)).toEqual(['https://example.test/source']);
  expect(library.recognitionHistory.size).toBe(0);
  await match.getByRole('button', { name: `Save ${frieren} to recognition history`, exact: true }).click();
  await expect(match.getByRole('button', { name: `${frieren} saved to history`, exact: true })).toBeDisabled();
  expect(library.recognitionHistory.size).toBe(1);
  expect(library.requests.find(request => request.method === 'recognition.run')?.params.provider).toBe('art');
  await page.getByRole('article', { name: `Recognition history ${frieren}`, exact: true }).getByRole('button', { name: `Remove recognition ${frieren}`, exact: true }).click();
  await expect(match.getByRole('button', { name: `Save ${frieren} to recognition history`, exact: true })).toBeEnabled();
  expect(library.recognitionHistory.size).toBe(0);
  await match.getByRole('button', { name: `Save ${frieren} to recognition history`, exact: true }).click();
  await expect(match.getByRole('button', { name: `${frieren} saved to history`, exact: true })).toBeDisabled();
  expect(library.recognitionHistory.size).toBe(1);
});

test('recognition history removal preserves the row on failure and removes it after retry', async ({ page, library }) => {
  library.recognitionHistory.set('existing', { id: 'existing', title: 'Odd Taxi', mediaId: 'anilist:128547', provider: 'trace.moe', sourceName: 'Screenshot.png', createdAt: '2026-10-03T08:00:00Z', romajiTitle: null, nativeTitle: null, episode: '5', similarity: .98, thumbnailUrl: library.catalog.get('anilist:128547')!.posterUrl });
  library.failNext('recognition.remove', 'Could not remove this history item.');
  await page.goto('/');
  await navigate(page, 'Recognize');
  const record = page.getByRole('article', { name: 'Recognition history Odd Taxi', exact: true });
  await record.getByRole('button', { name: 'Remove recognition Odd Taxi', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Could not remove this history item.');
  await expect(record).toBeVisible();
  expect(library.recognitionHistory.size).toBe(1);
  await record.getByRole('button', { name: 'Remove recognition Odd Taxi', exact: true }).click();
  await expect(record).not.toBeVisible();
  expect(library.recognitionHistory.size).toBe(0);
});

test('recognition works with legacy disabled settings and uploads only after the explicit provider action', async ({ page, library }) => {
  library.settings.recognitionServicesEnabled = false;
  await page.goto('/');
  await navigate(page, 'Recognize');
  await expect(page.getByText('Image recognition is turned off', { exact: true })).not.toBeVisible();
  expect(library.requests.some(request => request.method === 'recognition.run')).toBe(false);
  await page.getByLabel('Choose recognition image').setInputFiles(image);
  const traceSearch = page.getByRole('button', { name: 'Find matches with trace.moe', exact: true });
  await expect(traceSearch).toBeEnabled();
  await expect(traceSearch).toHaveAccessibleDescription(/sends this image to trace.moe/);
  expect(library.requests.some(request => request.method === 'recognition.run')).toBe(false);
  await page.getByRole('button', { name: /Artwork & manga/ }).click();
  const artSearch = page.getByRole('button', { name: 'Find matches with SauceNAO', exact: true });
  await expect(artSearch).toHaveAccessibleDescription(/sends this image to SauceNAO/);
  expect(library.requests.some(request => request.method === 'recognition.run')).toBe(false);
  await artSearch.click();
  await expect(page.getByRole('region', { name: 'Recognition results', exact: true })).toContainText('96.0%');
  const searches = library.requests.filter(request => request.method === 'recognition.run');
  expect(searches).toHaveLength(1);
  expect(searches[0].params).toMatchObject({ provider: 'art', uploadConfirmed: true });
  expect(library.settings.recognitionServicesEnabled).toBe(false);
  expect(library.requests.some(request => request.method === 'settings.save')).toBe(false);
  await page.getByRole('button', { name: 'Recognition settings', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Settings', level: 1, exact: true })).toBeVisible();
  await expect(page.getByRole('switch', { name: 'Image recognition services', exact: true })).toHaveCount(0);
  await expect(page.getByLabel('SauceNAO API key', { exact: true })).toBeVisible();
});

test('Settings saves the theme and clears saved secret inputs after connecting and syncing', async ({ page, library }) => {
  await page.goto('/');
  await navigate(page, 'Settings');
  await page.getByRole('combobox', { name: 'Theme', exact: true }).selectOption('High contrast');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'High contrast');
  expect(library.settings.theme).toBe('High contrast');
  await page.getByRole('textbox', { name: 'AniList Client ID', exact: true }).fill('1234');
  await page.getByLabel('AniList Client secret', { exact: true }).fill('test-only-secret');
  await page.getByRole('button', { name: 'Connect AniList', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Import from AniList', exact: true })).toBeVisible();
  await expect(page.getByLabel('AniList Client secret', { exact: true })).toHaveValue('');
  expect(library.settings.hasAniListClientSecret).toBe(true);
  await page.getByRole('button', { name: 'Import from AniList', exact: true }).click();
  await page.getByRole('button', { name: 'Import now', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Imported 4 titles' })).toBeVisible();
  await page.getByRole('button', { name: 'Disconnect', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Import from AniList', exact: true })).not.toBeVisible();
  expect(library.settings.isAniListConnected).toBe(false);
});

test('a failed setting save restores the control to its persisted value', async ({ page, library }) => {
  library.failNext('settings.save', 'Settings could not be saved.');
  await page.goto('/');
  await navigate(page, 'Settings');
  const notifications = page.getByRole('switch', { name: 'Notifications', exact: true });
  // A fast failed save can restore the checkbox before Playwright's uncheck postcondition.
  await notifications.click();
  await expect(page.getByRole('alert')).toContainText('Settings could not be saved.');
  await expect(notifications).toBeChecked();
  expect(library.settings.notificationsEnabled).toBe(true);
  await notifications.uncheck();
  await expect(notifications).not.toBeChecked();
  await expect.poll(() => library.settings.notificationsEnabled).toBe(false);
});

test('failed theme and catalog-source saves restore their persisted selections', async ({ page, library }) => {
  await page.goto('/');
  await navigate(page, 'Settings');
  const theme = page.getByRole('combobox', { name: 'Theme', exact: true });
  library.failNext('settings.save', 'The theme could not be saved.');
  await theme.selectOption('High contrast');
  await expect(page.getByRole('alert')).toContainText('The theme could not be saved.');
  await expect(theme).toHaveValue('Nebula dark');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Nebula dark');
  const source = page.getByRole('combobox', { name: 'Catalog source', exact: true });
  library.failNext('settings.save', 'The catalog source could not be saved.');
  await source.selectOption('MangaDex');
  await expect(page.getByRole('alert')).toContainText('The catalog source could not be saved.');
  await expect(source).toHaveValue('AniList');
  expect(library.settings.preferredSyncSource).toBe('AniList');
  await source.selectOption('MangaDex');
  await expect.poll(() => library.settings.preferredSyncSource).toBe('MangaDex');
});

test('Settings exports a usable CSV and previews XML before the explicit import action', async ({ page, library }) => {
  await page.goto('/');
  await navigate(page, 'Settings');
  const downloaded = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export CSV', exact: true }).click();
  const download = await downloaded;
  expect(download.suggestedFilename()).toBe('library.csv');
  expect(await readFile((await download.path())!, 'utf8')).toContain('title,progress');
  const xml = '<myanimelist><anime><series_animedb_id>52991</series_animedb_id><series_title>Оцінка 🌙</series_title></anime></myanimelist>';
  await page.getByLabel('Import MAL XML', { exact: true }).setInputFiles({ name: 'my-list.xml', mimeType: 'application/xml', buffer: Buffer.from(xml) });
  await expect(page.getByText('1 title in my-list.xml', { exact: true })).toBeVisible();
  expect(library.requests.some(request => request.method === 'library.import.apply')).toBe(false);
  expect(library.requests.find(request => request.method === 'library.import.preview')?.params.xml).toBe(xml);
  await page.getByRole('button', { name: 'Import 1 title', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Imported 1 title' })).toBeVisible();
  expect(library.requests.some(request => request.method === 'library.import.apply')).toBe(true);
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Version 0.2.0 is available' })).toBeVisible();
});

for (const name of ['Home', 'Schedule', 'Recognize', 'Settings']) {
  test(`${name} remains usable at narrow, tablet, and desktop widths without horizontal overflow`, async ({ page, library }) => {
    for (const width of [320, 360, 768, 1024]) {
      await page.setViewportSize({ width, height: 800 });
      await page.goto('/');
      await navigate(page, name);
      if (name === 'Home') await expect(page.getByRole('region', { name: 'Library statistics' })).toBeVisible();
      if (name === 'Schedule') { await page.getByRole('button', { name: 'This week', exact: true }).click(); await expect(page.getByRole('button', { name: `Open ${frieren}, episode 4`, exact: true })).toBeVisible(); }
      if (name === 'Recognize') { await page.getByLabel('Choose recognition image').setInputFiles(image); await page.getByRole('button', { name: 'Find matches with trace.moe', exact: true }).click(); await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible(); }
      if (name === 'Settings') await expect(page.getByRole('switch', { name: 'Notifications', exact: true })).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth), `No horizontal overflow at ${width}px`).toBeLessThanOrEqual(1);
    }
  });
}
