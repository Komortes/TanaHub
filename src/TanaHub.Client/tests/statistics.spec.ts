import type { Locator, Page } from '@playwright/test';
import { test, expect } from './fixtures';
import type { HomeSummary } from '../src/lib/feature-types';

const frieren = "Frieren: Beyond Journey's End";
const navigation = (page: Page) => page.getByRole('navigation', { name: 'Main navigation', exact: true });
const statistics = (page: Page) => page.getByRole('region', { name: 'Library statistics', exact: true });
const metric = (page: Page, name: string) => statistics(page).getByRole('term').filter({ hasText: name }).locator('..').getByRole('definition');
const settle = (page: Page) => page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
async function activate(control: Locator, method: 'click' | 'keyboard Enter') {
  if (method === 'click') await control.click();
  else { await control.focus(); await control.press('Enter'); }
}

for (const activation of ['click', 'keyboard Enter'] as const) {
  test(`Statistics is a Home subpage and Back restores its entry point after ${activation}`, async ({ page, library }) => {
    await page.goto('/#home');
    await page.getByRole('button', { name: 'Manga', exact: true }).click();
    await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
    const open = page.getByRole('button', { name: 'Statistics', exact: true });
    await expect(open).toBeVisible();
    await expect(statistics(page)).not.toBeVisible();
    await activate(open, activation);
    await expect(page).toHaveURL(/#home\/statistics$/);
    await expect(page).toHaveTitle('Statistics · TanaHub');
    await expect(page.getByRole('heading', { name: 'Statistics', level: 1, exact: true })).toBeFocused();
    await expect(page.getByRole('button', { name: 'Manga', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(statistics(page)).toBeVisible();
    await expect(metric(page, 'Chapters read')).toContainText('3');
    await expect(page.getByRole('region', { name: 'Continue tracking', exact: true })).not.toBeVisible();
    await activate(page.getByRole('button', { name: 'Back to Home', exact: true }), activation);
    await expect(page).toHaveURL(/#home$/);
    await expect(open).toBeFocused();
    await expect(page.getByRole('button', { name: 'Manga', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('button', { name: 'Open Berserk', exact: true })).toBeVisible();
    await expect(statistics(page)).not.toBeVisible();
    expect(library.requests.filter(request => request.method === 'home.get').at(-1)?.params.type).toBe('Manga');
  });
}

test('a direct Statistics route reloads, uses only local summary data, and has a safe Home fallback', async ({ page, library }) => {
  await page.goto('/#home/statistics');
  await expect(page.getByRole('heading', { name: 'Statistics', level: 1, exact: true })).toBeVisible();
  await expect(statistics(page)).toBeVisible();
  await expect(navigation(page).getByRole('button')).toHaveCount(6);
  await expect(navigation(page).getByRole('button', { name: 'Home', exact: true })).toHaveAttribute('aria-current', 'page');
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Statistics', level: 1, exact: true })).toBeVisible();
  await expect(statistics(page)).toBeVisible();
  await settle(page);
  expect(library.requests.some(request => ['home.recommendations', 'schedule.list', 'schedule.notify'].includes(request.method))).toBe(false);
  await page.getByRole('button', { name: 'Back to Home', exact: true }).click();
  await expect(page).toHaveURL(/#home$/);
  await expect(page.getByRole('heading', { name: 'Home', level: 1, exact: true })).toBeFocused();
});

test('Statistics follows browser Back and Forward and the Home navigation item exits its subpage', async ({ page }) => {
  await page.goto('/#home');
  const open = page.getByRole('button', { name: 'Statistics', exact: true });
  await open.click();
  await expect(statistics(page)).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(/#home$/);
  await expect(open).toBeFocused();
  await page.goForward();
  await expect(page).toHaveURL(/#home\/statistics$/);
  await expect(page.getByRole('heading', { name: 'Statistics', level: 1, exact: true })).toBeFocused();
  await navigation(page).getByRole('button', { name: 'Home', exact: true }).click();
  await expect(page).toHaveURL(/#home$/);
  await expect(page.getByRole('heading', { name: 'Home', level: 1, exact: true })).toBeFocused();
  await expect(statistics(page)).not.toBeVisible();
});

test('Statistics media type is shared with Home and ignores an older response', async ({ page, library }) => {
  let release!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  const stale: HomeSummary = { insights: { totalEntries: 999, completedEntries: 0, backlogEntries: 0, droppedEntries: 0, completionRate: 0, droppedRatio: 0, averageScore: null, episodesWatched: 0, chaptersRead: 0, estimatedWatchMinutes: 0, byMediaType: [], byStatus: [] }, continueItems: [], recentItems: [] };
  library.override = async ({ method, params }) => {
    if (method !== 'home.get' || params.type !== 'Anime') return undefined;
    await held;
    return stale;
  };
  await page.goto('/#home/statistics');
  await expect(page.getByText('4 titles tracked', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Anime', exact: true }).click();
  await expect.poll(() => library.requests.some(request => request.method === 'home.get' && request.params.type === 'Anime')).toBe(true);
  await expect(page.getByText('4 titles tracked', { exact: true })).not.toBeVisible();
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
  const oldResponse = page.waitForResponse(response => response.url().endsWith('/api/rpc') && response.request().postDataJSON().method === 'home.get' && response.request().postDataJSON().params.type === 'Anime');
  release();
  await (await oldResponse).finished();
  await settle(page);
  await expect(page.getByText('999 titles tracked', { exact: true })).not.toBeVisible();
  await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Back to Home', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Manga', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('button', { name: 'Open Berserk', exact: true })).toBeVisible();
});

for (const activation of ['click', 'keyboard Enter'] as const) {
  test(`Statistics Retry keeps focus on its heading after ${activation}`, async ({ page, library }) => {
    library.failNext('home.get', 'Your library statistics are temporarily unavailable.');
    await page.goto('/#home/statistics');
    await expect(page.getByRole('alert')).toContainText('Your library statistics are temporarily unavailable.');
    await expect(statistics(page)).not.toBeVisible();
    await activate(page.getByRole('button', { name: 'Try again', exact: true }), activation);
    await expect(page.getByRole('heading', { name: 'Statistics', level: 1, exact: true })).toBeFocused();
    await expect(statistics(page)).toBeVisible();
    await expect(page.getByRole('alert')).not.toBeVisible();
    expect(library.requests.filter(request => request.method === 'home.get')).toHaveLength(2);
  });
}

test('a Statistics response finishing after navigation cannot take focus or expose stale content', async ({ page, library }) => {
  let release!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  library.override = async ({ method }) => { if (method === 'home.get') await held; return undefined; };
  await page.goto('/#home/statistics');
  await expect(page.getByRole('heading', { name: 'Statistics', level: 1, exact: true })).toBeVisible();
  await expect.poll(() => library.requests.filter(request => request.method === 'home.get').length).toBe(1);
  await navigation(page).getByRole('button', { name: 'Discover', exact: true }).click();
  const search = page.getByRole('searchbox', { name: 'Search the catalog', exact: true });
  await search.focus();
  const oldResponse = page.waitForResponse(response => response.url().endsWith('/api/rpc') && response.request().postDataJSON().method === 'home.get');
  release();
  await (await oldResponse).finished();
  await settle(page);
  await expect(search).toBeFocused();
  await expect(page.getByRole('heading', { name: 'Discover', level: 1, exact: true })).toBeVisible();
  await expect(statistics(page)).not.toBeVisible();
});

test('Statistics reflects progress recorded from Home', async ({ page, library }) => {
  await page.goto('/#home');
  const card = page.getByRole('region', { name: 'Continue tracking', exact: true }).getByRole('article', { name: frieren, exact: true });
  await card.getByRole('button', { name: `Add episode to ${frieren}`, exact: true }).click();
  await expect(card).toContainText('EP 4');
  expect(library.entries.get('anilist:154587')?.progress).toBe(4);
  await page.getByRole('button', { name: 'Statistics', exact: true }).click();
  await expect(metric(page, 'Watch time')).toContainText('10 episodes watched');
  await expect(metric(page, 'Watch time')).toContainText('4h 0m');
});

test('empty Statistics shows real zero values, absent ratings and both chart empty states', async ({ page, library }) => {
  library.entries.clear();
  await page.goto('/#home/statistics');
  await expect(statistics(page)).toBeVisible();
  await expect(statistics(page).getByRole('term')).toHaveCount(6);
  await expect(metric(page, 'Completion')).toContainText('0%');
  await expect(metric(page, 'Average rating')).toContainText('—');
  await expect(metric(page, 'Watch time')).toContainText('0m');
  await expect(page.getByRole('region', { name: 'By media type', exact: true })).toContainText('Add titles to see your library take shape.');
  await expect(page.getByRole('region', { name: 'By library status', exact: true })).toContainText('Add titles to see your library take shape.');
  await expect(statistics(page)).not.toContainText(/NaN|Infinity/);
});

test('large saved progress remains readable in narrow Statistics', async ({ page, library }) => {
  for (const [id, entry] of library.entries) library.entries.set(id, { ...entry, progress: 2_147_483_647 });
  await page.setViewportSize({ width: 320, height: 800 });
  await page.goto('/#home/statistics');
  await expect(metric(page, 'Watch time')).toContainText('6,442,450,941 episodes watched');
  await expect(metric(page, 'Watch time')).toContainText('2,576,980,376h 24m');
  await expect(metric(page, 'Chapters read')).toContainText('2,147,483,647');
  expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
});

for (const theme of ['Nebula dark', 'High contrast']) {
  test(`Statistics keeps six navigation targets and fits compact through wide layouts in ${theme}`, async ({ page, library }) => {
    library.settings.theme = theme;
    for (const width of [320, 360, 768, 1024, 1440]) {
      await page.setViewportSize({ width, height: 800 });
      await page.goto('/#home/statistics');
      await expect(statistics(page)).toBeVisible();
      await expect(navigation(page).getByRole('button')).toHaveCount(6);
      for (const name of ['Home', 'My library', 'Discover', 'Schedule', 'Recognize', 'Settings']) {
        const button = navigation(page).getByRole('button', { name, exact: true });
        await expect(button.locator('span')).toBeVisible();
        const box = await button.boundingBox();
        expect(box!.width).toBeGreaterThanOrEqual(44);
        expect(box!.height).toBeGreaterThanOrEqual(44);
      }
      for (const name of ['Back to Home', 'All titles', 'Anime', 'Manga']) {
        const control = page.getByRole('button', { name, exact: true });
        const box = await control.boundingBox();
        expect(box!.height).toBeGreaterThanOrEqual(44);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
    }
  });
}
