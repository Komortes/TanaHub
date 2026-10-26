import { test, expect } from './fixtures';

const title = "Frieren: Beyond Journey's End";
const pages = [
  { route: 'library', heading: 'My library', method: 'library.list', region: 'Your library titles' },
  { route: 'discover', heading: 'Discover', method: 'catalog.search', region: 'Catalog titles' },
  { route: 'home', heading: 'Home', method: 'home.get', region: 'Continue tracking' },
] as const;

for (const destination of pages) {
  for (const activation of ['click', 'keyboard Enter'] as const) {
    test(`${destination.route} retry keeps focus on its persistent heading after ${activation}`, async ({ page, library }) => {
      library.failNext(destination.method, 'A temporary fixture service failure.');
      await page.goto(`/#${destination.route}`);
      await expect(page.getByRole('alert')).toContainText('A temporary fixture service failure.');
      const retry = page.getByRole('button', { name: 'Try again', exact: true });
      if (activation === 'click') await retry.click();
      else { await retry.focus(); await page.keyboard.press('Enter'); }

      await expect(page.getByRole('region', { name: destination.region, exact: true }).getByRole('article', { name: title, exact: true })).toBeVisible();
      await expect(retry).toHaveCount(0);
      await expect(page.getByRole('heading', { name: destination.heading, exact: true, level: 1 })).toBeFocused();
      expect(library.requests.filter(request => request.method === destination.method)).toHaveLength(2);
    });
  }
}

test('late retry responses preserve later user focus and focus after navigation', async ({ page, library }) => {
  let releaseLibrary!: () => void;
  const libraryReady = new Promise<void>(resolve => { releaseLibrary = resolve; });
  let holdLibrary = false;
  let releaseHome!: () => void;
  const homeReady = new Promise<void>(resolve => { releaseHome = resolve; });
  let holdHome = false;
  library.override = async ({ method }) => {
    if (method === 'library.list' && holdLibrary) await libraryReady;
    if (method === 'home.get' && holdHome) await homeReady;
    return undefined;
  };
  library.failNext('library.list', 'Library is temporarily unavailable.');
  await page.goto('/#library');
  const retry = page.getByRole('button', { name: 'Try again', exact: true });
  await expect(retry).toBeVisible();
  holdLibrary = true;
  await retry.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'My library', exact: true, level: 1 })).toBeFocused();
  await expect.poll(() => library.requests.filter(request => request.method === 'library.list').length).toBe(2);
  const librarySearch = page.getByRole('searchbox', { name: 'Search your library', exact: true });
  await librarySearch.focus();
  releaseLibrary();
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).toBeVisible();
  await expect(librarySearch).toBeFocused();

  library.failNext('home.get', 'Home is temporarily unavailable.');
  const navigation = page.getByRole('navigation', { name: 'Main navigation', exact: true });
  await navigation.getByRole('button', { name: 'Home', exact: true }).click();
  await expect(retry).toBeVisible();
  holdHome = true;
  await retry.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Home', exact: true, level: 1 })).toBeFocused();
  await expect.poll(() => library.requests.filter(request => request.method === 'home.get').length).toBe(2);
  await navigation.getByRole('button', { name: 'Discover', exact: true }).click();
  const catalogSearch = page.getByRole('searchbox', { name: 'Search the catalog', exact: true });
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).toBeVisible();
  await catalogSearch.focus();
  const homeResponse = page.waitForResponse(response => response.url().endsWith('/api/rpc') && response.request().postDataJSON().method === 'home.get');
  releaseHome();
  await (await homeResponse).finished();
  await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(catalogSearch).toBeFocused();
  await expect(page.getByRole('heading', { name: 'Discover', exact: true, level: 1 })).toBeVisible();
});
