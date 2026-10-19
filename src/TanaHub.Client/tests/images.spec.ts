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
  for (let i = 0; i < 48; i++) {
    const item = media(`anilist:${i}`, `Story ${String(i).padStart(2, '0')}`, { posterUrl: `https://s4.anilist.co/${i}.jpg` });
    library.catalog.set(item.id, item);
    library.entries.set(item.id, { mediaId: item.id, mediaType: 'Anime', status: 'Planning', progress: 0, score: null, notes: null, review: null, updatedAt: '2026-10-19T00:00:00Z', media: item });
  }
  await page.route('https://s4.anilist.co/**', route => { requested.push(route.request().url()); return route.fulfill({ contentType: 'image/svg+xml', body: svg }); });
  await page.goto('/');
  const cards = page.getByRole('article');
  await expect(cards).toHaveCount(48);
  await expect(cards.first().getByRole('img')).toBeVisible();
  console.log('Initial cover requests:', requested.length);
  await expect(cards.nth(30).locator('img')).not.toHaveAttribute('src', /./);
  await cards.nth(30).scrollIntoViewIfNeeded();
  await expect(cards.nth(30).getByRole('img')).toBeVisible();
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
