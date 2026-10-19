import { test, expect, media } from './fixtures';

const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="300" height="450"><rect width="300" height="450" fill="#51457a"/></svg>';

test('cards use the preview and list rows use the thumbnail, keeping the original for enlargement', async ({ page, library }) => {
  const record = library.entries.get('anilist:154587')!;
  Object.assign(record.media, { posterUrl: 'https://s4.anilist.co/full.jpg', previewUrl: 'https://s4.anilist.co/card.jpg', thumbnailUrl: 'https://s4.anilist.co/thumb.jpg' });
  await page.route('https://s4.anilist.co/**', route => route.fulfill({ contentType: 'image/svg+xml', body: svg }));
  await page.goto('/');
  const card = page.getByRole('article', { name: record.media.title, exact: true });
  await expect(card.getByRole('img')).toHaveAttribute('src', /card\.jpg$/);
  await page.getByRole('button', { name: 'List view', exact: true }).click();
  await expect(card.getByRole('img')).toHaveAttribute('src', /thumb\.jpg$/);
  await card.getByRole('button', { name: `Open ${record.media.title}`, exact: true }).click();
  await page.getByRole('button', { name: `Enlarge cover of ${record.media.title}`, exact: true }).click();
  await expect(page.getByRole('dialog').getByRole('img')).toHaveAttribute('src', /full\.jpg$/);
});

test('distant covers wait for scrolling instead of filling the network queue', async ({ page, library }) => {
  library.entries.clear(); library.catalog.clear();
  const requested: string[] = [];
  let active = 0, peak = 0;
  for (let i = 0; i < 48; i++) {
    const item = media(`anilist:${i}`, `Story ${String(i).padStart(2, '0')}`, { posterUrl: `https://s4.anilist.co/${i}.jpg` });
    library.catalog.set(item.id, item);
    library.entries.set(item.id, { mediaId: item.id, mediaType: 'Anime', status: 'Planning', progress: 0, score: null, notes: null, review: null, updatedAt: '2026-10-19T00:00:00Z', media: item });
  }
  await page.route('https://s4.anilist.co/**', async route => {
    requested.push(route.request().url()); active++; peak = Math.max(peak, active);
    await new Promise(resolve => setTimeout(resolve, 30));
    active--;
    await route.fulfill({ contentType: 'image/svg+xml', body: svg });
  });
  await page.goto('/');
  const cards = page.getByRole('article');
  await expect(cards).toHaveCount(48);
  await expect(cards.first().getByRole('img')).toBeVisible();
  console.log('Initial cover requests:', requested.length);
  await expect(cards.nth(30).locator('img')).not.toHaveAttribute('src', /./);
  await cards.nth(30).scrollIntoViewIfNeeded();
  await expect(cards.nth(30).getByRole('img')).toBeVisible();
  expect(peak).toBeLessThanOrEqual(4);
});

test('a failed preview falls back to the original without hiding the cover', async ({ page, library }) => {
  const record = library.entries.get('anilist:154587')!;
  Object.assign(record.media, { posterUrl: 'https://s4.anilist.co/full.jpg', previewUrl: 'https://s4.anilist.co/broken.jpg' });
  await page.route('https://s4.anilist.co/**', route => route.fulfill({ contentType: route.request().url().endsWith('broken.jpg') ? 'image/jpeg' : 'image/svg+xml', body: route.request().url().endsWith('broken.jpg') ? 'not an image' : svg }));
  await page.goto('/');
  const cover = page.getByRole('article', { name: record.media.title, exact: true }).getByRole('img');
  await expect(cover).toHaveAttribute('src', /full\.jpg$/);
  await expect.poll(() => cover.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
});

test.describe('Retina artwork', () => {
  test.use({ deviceScaleFactor: 2 });
  test('uses enough pixels for the actual display size', async ({ page, library }) => {
    const record = library.entries.get('anilist:154587')!;
    Object.assign(record.media, { posterUrl: 'https://s4.anilist.co/full.jpg', previewUrl: 'https://s4.anilist.co/card.jpg', thumbnailUrl: 'https://s4.anilist.co/thumb.jpg' });
    await page.route('https://s4.anilist.co/**', route => route.fulfill({ contentType: 'image/svg+xml', body: svg }));
    await page.goto('/');
    const cover = page.getByRole('article', { name: record.media.title, exact: true }).getByRole('img');
    await expect(cover).toHaveAttribute('src', /full\.jpg$/);
    await page.getByRole('button', { name: 'List view', exact: true }).click();
    await expect(cover).toHaveAttribute('src', /card\.jpg$/);
  });
});

test('duplicate visibility notifications cannot exhaust the artwork queue', async ({ page, library }) => {
  await page.addInitScript(() => {
    const Original = window.IntersectionObserver;
    window.IntersectionObserver = class extends Original {
      constructor(callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
        super((entries, observer) => { callback(entries, observer); callback(entries, observer); }, options);
      }
    };
  });
  await page.goto('/');
  const title = library.entries.get('anilist:154587')!.media.title;
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  const detail = page.getByRole('article').filter({ has: page.getByRole('heading', { name: title, exact: true }) });
  const cover = detail.getByRole('img', { name: `Cover of ${title}` });
  await expect.poll(() => cover.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
});

test('an error from removed artwork cannot hide a refreshed cover', async ({ page, library }) => {
  const title = library.entries.get('anilist:154587')!.media.title;
  const oldPoster = library.entries.get('anilist:101922')!.media.posterUrl;
  const saved = library.entries.get('anilist:154587')!;
  library.entries.set(saved.mediaId, { ...saved, media: { ...saved.media, posterUrl: oldPoster } });
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  library.override = async ({ method }) => { if (method === 'media.detail') { await gate; return library.catalog.get(saved.mediaId); } return undefined; };
  await page.goto('/');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  const detail = page.getByRole('article').filter({ has: page.getByRole('heading', { name: title, exact: true }) });
  const cover = detail.getByRole('img', { name: `Cover of ${title}` });
  await expect.poll(() => cover.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  const previous = await cover.elementHandle();
  await previous!.evaluate(image => image.dispatchEvent(new Event('error')));
  await expect(cover).toHaveCount(0);
  release();
  await expect.poll(() => cover.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  await previous!.evaluate(image => image.dispatchEvent(new Event('error')));
  await expect.poll(() => cover.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
});
