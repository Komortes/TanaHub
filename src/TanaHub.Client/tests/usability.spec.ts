import { test, expect } from './fixtures';

for (const width of [320, 432, 768]) {
  test(`navigation labels and touch targets stay usable at ${width}px`, async ({ page, library }) => {
    await page.setViewportSize({ width, height: 800 });
    await page.goto('/');
    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    for (const name of ['Home', 'My library', 'Discover', 'Schedule', 'Recognize', 'Settings']) {
      const button = nav.getByRole('button', { name, exact: true });
      await expect(button.locator('span')).toBeVisible();
      const box = await button.boundingBox();
      expect(box!.width).toBeGreaterThanOrEqual(44);
      expect(box!.height).toBeGreaterThanOrEqual(44);
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
  });
}

test('first library visit includes anime and manga', async ({ page, library }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'All', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('button', { name: 'Open Berserk', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Watching / Reading', exact: true })).toBeVisible();
  expect(library.requests.find(request => request.method === 'library.list')?.params.type).toBeUndefined();
});

test('an empty library explains local use and opens the import controls directly', async ({ page, library }) => {
  library.entries.clear();
  await page.goto('/');
  await expect(page.getByText('No account required. Your library stays on this device.', { exact: true })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Sort library', exact: true })).not.toBeVisible();
  await expect(page.getByRole('button', { name: 'Watching / Reading', exact: true })).not.toBeVisible();
  await page.getByRole('button', { name: 'Import a list', exact: true }).click();
  const data = page.getByRole('region', { name: 'Your library data', exact: true });
  await expect(data.getByRole('heading', { name: 'Your library data' })).toBeFocused();
  await expect(data.getByLabel('Import MAL XML', { exact: true })).toBeVisible();
});

test('library controls appear after adding the first title', async ({ page, library }) => {
  library.entries.clear();
  await page.goto('/');
  await page.getByRole('button', { name: 'Discover titles', exact: true }).click();
  await page.getByRole('button', { name: "Add Frieren: Beyond Journey's End to library", exact: true }).click();
  await page.getByRole('button', { name: 'My library', exact: true }).click();
  await expect(page.getByRole('article')).toHaveCount(1);
  await expect(page.getByRole('combobox', { name: 'Sort library', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Watching / Reading', exact: true })).toBeVisible();
});

test('the import shortcut waits for settings and does not lose its destination', async ({ page, library }) => {
  library.entries.clear();
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  library.override = async request => { if (request.method === 'settings.get') await pending; return undefined; };
  await page.goto('/');
  await page.getByRole('button', { name: 'Import a list', exact: true }).click();
  await expect(page.getByText('Loading settings…', { exact: true })).toBeVisible();
  release();
  await expect(page.getByRole('heading', { name: 'Your library data', exact: true })).toBeFocused();
});

test('late settings do not pull focus back after leaving the import shortcut', async ({ page, library }) => {
  library.entries.clear();
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  library.override = async request => { if (request.method === 'settings.get') await pending; return undefined; };
  await page.goto('/');
  await page.getByRole('button', { name: 'Import a list', exact: true }).click();
  await page.getByRole('button', { name: 'Home', exact: true }).click();
  const response = page.waitForResponse(result => result.url().endsWith('/api/rpc') && result.request().postDataJSON().method === 'settings.get');
  release();
  await (await response).finished();
  await expect(page.getByRole('heading', { name: 'Home', exact: true })).toBeFocused();
});

test('first-use actions and import confirmation fit a 320px screen', async ({ page, library }) => {
  library.entries.clear();
  library.settings.isAniListConnected = true;
  await page.setViewportSize({ width: 320, height: 800 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Import a list', exact: true })).toBeVisible();
  await page.screenshot({ path: '/tmp/tanahub-oct19-empty-320.png' });
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
  await page.getByRole('button', { name: 'Import a list', exact: true }).click();
  await page.getByRole('button', { name: 'Import from AniList', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Import now', exact: true })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
});

test('library and catalog keep independent searches and media types across navigation', async ({ page, library }) => {
  await page.goto('/');
  await expect(page).toHaveTitle('My library · TanaHub');
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  await page.getByRole('searchbox', { name: 'Search your library' }).fill('Berserk');
  await expect(page.getByRole('button', { name: 'Open Berserk', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Discover', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Anime', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('searchbox', { name: 'Search the catalog' }).fill('Frieren');
  await expect(page.getByRole('button', { name: "Open Frieren: Beyond Journey's End", exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'My library', exact: true }).click();
  await expect(page.getByRole('searchbox', { name: 'Search your library' })).toHaveValue('Berserk');
  await expect(page.getByRole('button', { name: 'Manga', exact: true })).toHaveAttribute('aria-pressed', 'true');
  // The restored search is debounced; wait for its normal request before counting a no-op click.
  await expect(page.getByRole('button', { name: 'Open Berserk', exact: true })).toBeVisible();
  const count = library.requests.filter(request => request.method === 'library.list').length;
  await page.getByRole('button', { name: 'My library', exact: true }).click();
  await expect(page.getByRole('searchbox', { name: 'Search your library' })).toHaveValue('Berserk');
  expect(library.requests.filter(request => request.method === 'library.list')).toHaveLength(count);
  await page.getByRole('button', { name: 'Discover', exact: true }).click();
  await expect(page.getByRole('searchbox', { name: 'Search the catalog' })).toHaveValue('Frieren');
});

test('keyboard users can skip navigation and page changes focus the heading', async ({ page, library }) => {
  await page.goto('/');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Skip to content' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeFocused();
  await expect(page).toHaveURL(/#library$/);
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page).toHaveTitle('Settings · TanaHub');
  await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeFocused();
});

test('settings describe optional account setup and only offer actual catalog providers', async ({ page, library }) => {
  await page.goto('/#settings');
  await expect(page.getByText('An account is optional. You can browse and track titles without connecting AniList.', { exact: true })).toBeVisible();
  const source = page.getByRole('combobox', { name: 'Catalog source', exact: true });
  await expect(source.locator('option')).toHaveCount(2);
  await page.getByText('How to connect AniList', { exact: true }).click();
  await expect(page.getByRole('button', { name: 'Open AniList developer settings', exact: true })).toBeVisible();
  await expect(page.getByText('http://localhost:7575/callback/', { exact: true })).toBeVisible();
});

test('unrelated settings saves preserve AniList credential drafts', async ({ page, library }) => {
  library.settings.aniListClientId = 'saved-id';
  library.settings.hasAniListClientSecret = true;
  await page.goto('/#settings');
  const id = page.getByLabel('AniList Client ID', { exact: true });
  const secret = page.getByLabel('AniList Client secret', { exact: true });
  await expect(id).toHaveValue('saved-id');
  await id.fill('new-id');
  await secret.fill('draft-secret');
  await page.getByRole('combobox', { name: 'Theme', exact: true }).selectOption('High contrast');
  await expect.poll(() => library.settings.theme).toBe('High contrast');
  await expect(id).toHaveValue('new-id');
  await expect(secret).toHaveValue('draft-secret');
  expect(library.settings.aniListClientId).toBe('saved-id');
});

test('changing Client ID requires its own secret instead of reusing the previous one', async ({ page, library }) => {
  library.settings.aniListClientId = 'saved-id';
  library.settings.hasAniListClientSecret = true;
  await page.goto('/#settings');
  const id = page.getByLabel('AniList Client ID', { exact: true });
  const connect = page.getByRole('button', { name: 'Connect AniList', exact: true });
  await expect(connect).toBeEnabled();
  await id.fill('new-id');
  await expect(connect).toBeDisabled();
  await expect(page.getByText('Enter the secret for this Client ID. The saved secret belongs to your previous ID.', { exact: true })).toBeVisible();
  await page.getByLabel('AniList Client secret', { exact: true }).fill('   ');
  await expect(connect).toBeDisabled();
  await page.getByLabel('AniList Client secret', { exact: true }).fill('new-secret');
  await expect(connect).toBeEnabled();
  expect(library.requests.some(request => request.method === 'anilist.connect')).toBe(false);
});

test('settings selects retain usable targets and keyboard focus order', async ({ page, library }) => {
  await page.goto('/#settings');
  const theme = page.getByRole('combobox', { name: 'Theme', exact: true });
  await expect(theme).toBeEnabled();
  const box = await theme.boundingBox();
  expect(box!.height).toBeGreaterThanOrEqual(40);
  await theme.focus();
  await expect(theme).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('switch', { name: 'Notifications', exact: true })).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(theme).toBeFocused();
  await expect(theme).toHaveValue('Nebula dark');
  const source = page.getByRole('combobox', { name: 'Catalog source', exact: true });
  expect((await source.boundingBox())!.height).toBeGreaterThanOrEqual(40);
});

test('legacy MyAnimeList source is explained and not silently rewritten', async ({ page, library }) => {
  library.settings.preferredSyncSource = 'MyAnimeList';
  await page.goto('/#settings');
  const source = page.getByRole('combobox', { name: 'Catalog source', exact: true });
  await expect(source).toHaveValue('MyAnimeList');
  await expect(source.locator('option[value="MyAnimeList"]')).toHaveJSProperty('disabled', true);
  await expect(page.getByText('MyAnimeList uses the AniList catalog. You can still import and export MAL XML.', { exact: true })).toBeVisible();
  expect(library.requests.some(request => request.method === 'settings.save')).toBe(false);
  library.failNext('settings.save', 'Catalog save failed.');
  await source.selectOption('MangaDex');
  await expect(page.getByRole('alert')).toContainText('Catalog save failed.');
  await expect(source).toHaveValue('MyAnimeList');
  await source.selectOption('MangaDex');
  await expect.poll(() => library.settings.preferredSyncSource).toBe('MangaDex');
});

test('AniList import explains replacements, supports cancel, and keeps confirmation after failure', async ({ page, library }) => {
  library.settings.isAniListConnected = true;
  await page.goto('/#settings');
  const start = page.getByRole('button', { name: 'Import from AniList', exact: true });
  await start.click();
  const confirmation = page.getByRole('group', { name: 'Confirm AniList import' });
  await expect(confirmation).toContainText('progress, status, scores, notes and dates');
  expect(library.requests.some(request => request.method === 'anilist.sync')).toBe(false);
  await confirmation.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(start).toBeFocused();
  await start.click();
  library.failNext('anilist.sync', 'Import could not finish.');
  await confirmation.getByRole('button', { name: 'Import now', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Import could not finish.');
  await expect(confirmation).toBeVisible();
  await confirmation.getByRole('button', { name: 'Import now', exact: true }).click();
  await expect(confirmation).not.toBeVisible();
  await expect(page.getByRole('status')).toContainText('Imported 4 titles');
});
