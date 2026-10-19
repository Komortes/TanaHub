import { mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';
import type { Locator, Page, Route } from '@playwright/test';
import { test, expect, media } from './fixtures';

const evidenceDirectory = process.env.TANAHUB_QA_EVIDENCE_DIR;
const artworkHost = 'https://artwork.example.test';
const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="400" height="600"><rect width="400" height="600" fill="#51457a"/><circle cx="200" cy="200" r="100" fill="#b7a2ea"/></svg>';

type ImageReply = { body: string; contentType: string };
type PendingImage = { path: string; complete: (reply?: ImageReply) => void };

/** Hold real browser image requests until the test releases a network slot. */
async function controlImages(page: Page) {
  const requests: PendingImage[] = [];
  let active = 0;
  let peak = 0;
  let passThrough = false;
  await page.route(`${artworkHost}/**`, async (route: Route) => {
    let release!: (reply: ImageReply) => void;
    const reply = new Promise<ImageReply>(resolve => { release = resolve; });
    const request: PendingImage = {
      path: new URL(route.request().url()).pathname,
      complete: (response = { body: svg, contentType: 'image/svg+xml' }) => release(response),
    };
    requests.push(request);
    active += 1;
    peak = Math.max(peak, active);
    if (passThrough) request.complete();
    const response = await reply;
    try { await route.fulfill({ ...response, headers: { 'cache-control': 'no-store' } }); }
    finally { active -= 1; }
  });
  return {
    requests,
    get peak() { return peak; },
    finishAll() { passThrough = true; requests.forEach(request => request.complete()); },
  };
}

async function screenshot(page: Page, name: string) {
  const path = evidenceDirectory ? `${evidenceDirectory}/${name}.png` : test.info().outputPath(`${name}.png`);
  await mkdir(dirname(path), { recursive: true });
  await page.screenshot({ path, fullPage: true });
}

async function expectLoaded(image: Locator) {
  await expect(image).toBeVisible();
  await expect.poll(() => image.evaluate(node => (node as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  await expect(image).not.toHaveClass(/\bartwork-loading\b/);
}

for (const collection of ['library', 'discover'] as const) {
  test(`${collection}: opening a title gives visible artwork the next free slots and Back resumes covers`, async ({ page, library }) => {
    await page.setViewportSize({ width: 1440, height: 1200 });
    library.entries.clear();
    library.catalog.clear();
    for (let index = 0; index < 48; index += 1) {
      const item = media(`anilist:queue-${index}`, `Queue story ${String(index).padStart(2, '0')}`, {
        posterUrl: `${artworkHost}/${collection === 'discover' ? 'original' : 'grid'}/${index}.svg`,
        ...(collection === 'discover' ? { previewUrl: `${artworkHost}/grid/${index}.svg` } : {}),
        ...(index === 0 ? {
          bannerUrl: `${artworkHost}/detail/banner.svg`,
          characters: [{ name: 'Visible companion', role: 'Main', imageUrl: `${artworkHost}/detail/character.svg` }],
        } : {}),
      });
      library.catalog.set(item.id, item);
      if (collection === 'library') library.entries.set(item.id, {
        mediaId: item.id, mediaType: item.type, status: 'Planning', progress: 0,
        score: null, notes: null, review: null, updatedAt: '2026-10-19T00:00:00Z', media: item,
      });
    }
    const images = await controlImages(page);
    try {
      if (collection === 'discover') {
        // A new user enters discovery from the empty library.
        await page.goto('/');
        await page.getByRole('button', { name: 'Discover titles', exact: true }).click();
      } else await page.goto('/');
      const cards = page.getByRole('article');
      await expect(cards).toHaveCount(collection === 'library' ? 48 : 24);
      await expect.poll(() => images.requests.length).toBe(4);
      const firstBatch = images.requests.slice();
      expect(firstBatch.every(request => request.path.startsWith('/grid/'))).toBe(true);
      expect(images.peak, 'The initial collection must use at most four image loads').toBe(4);
      // More covers are close enough to need loading, while distant covers stay deferred.
      await expect(cards.nth(4)).toBeInViewport();
      await expect(cards.last().locator('img')).not.toHaveAttribute('src', /./);

      await page.getByRole('button', { name: 'Open Queue story 00', exact: true }).click();
      const detail = page.getByRole('article', { name: 'Queue story 00', exact: true });
      await expect(detail.getByRole('heading', { name: 'Queue story 00', exact: true })).toBeVisible();
      await expect(detail.getByRole('heading', { name: 'Characters', exact: true })).toBeInViewport();
      firstBatch.forEach(request => request.complete());
      await expect.poll(() => images.requests.length).toBeGreaterThanOrEqual(6);
      await screenshot(page, `queue-${collection}-after-opening-title`);

      const afterOpening = images.requests.filter(request => !firstBatch.includes(request));
      expect(afterOpening.map(request => request.path), 'Only visible title artwork may start while the collection is hidden').toEqual(
        expect.arrayContaining(['/detail/banner.svg', '/detail/character.svg']),
      );
      const visibleArtwork = new Set(['/detail/banner.svg', '/detail/character.svg', '/grid/0.svg', '/original/0.svg']);
      expect(afterOpening.filter(request => !visibleArtwork.has(request.path)), 'Hidden collection covers must leave the network slots for the title').toEqual([]);
      expect(images.peak).toBeLessThanOrEqual(4);
      afterOpening.forEach(request => request.complete());
      await expectLoaded(detail.locator('img.title-banner'));
      await expectLoaded(detail.getByRole('img', { name: 'Cover of Queue story 00', exact: true }));
      await expectLoaded(detail.getByRole('region', { name: 'Characters', exact: true }).locator('img'));

      const beforeBack = images.requests.length;
      await detail.getByRole('button', { name: 'Back', exact: true }).click();
      await expect(cards).toHaveCount(collection === 'library' ? 48 : 24);
      await expect.poll(() => images.requests.length).toBeGreaterThan(beforeBack);
      expect(images.requests.slice(beforeBack).every(request => request.path.startsWith('/grid/'))).toBe(true);
      images.finishAll();
      await expectLoaded(cards.nth(4).getByRole('img'));
      await expect(cards.last().locator('img')).not.toHaveAttribute('src', /./);
      await screenshot(page, `queue-${collection}-back-resumes-covers`);
    } finally { images.finishAll(); }
  });
}

for (const changedUrl of [false, true]) {
  test(`Refresh related titles recovers a failed cover when its URL ${changedUrl ? 'changes' : 'stays the same'}`, async ({ page, library }) => {
    const parentId = 'anilist:154587';
    const result = library.relations.get(parentId)!;
    const oldUrl = `${artworkHost}/related/first.svg`;
    const newUrl = changedUrl ? `${artworkHost}/related/replacement.svg` : oldUrl;
    library.relations.set(parentId, { ...result, titles: [{ ...result.titles[0], posterUrl: oldUrl }] });
    const images = await controlImages(page);
    try {
      await page.goto(`/#title/${encodeURIComponent(parentId)}`);
      const related = page.getByRole('button', { name: /^Open Frieren: The Next Journey,/ });
      await related.scrollIntoViewIfNeeded();
      await expect.poll(() => images.requests.length).toBe(1);
      // A successful HTTP response with undecodable bytes models a transient image failure
      // without generating a console/network error unrelated to the recovery behavior.
      images.requests[0].complete({ contentType: 'image/jpeg', body: 'temporarily invalid image bytes' });
      await expect(related.locator('img')).toHaveCount(0);
      await screenshot(page, `related-${changedUrl ? 'changed' : 'same'}-url-failed`);

      library.relations.set(parentId, {
        ...result, titles: [{ ...result.titles[0], posterUrl: newUrl }], notice: 'Fresh connections loaded.',
      });
      await page.getByRole('button', { name: 'Refresh related titles', exact: true }).click();
      await expect(page.getByText('Fresh connections loaded.', { exact: true })).toBeVisible();
      await screenshot(page, `related-${changedUrl ? 'changed' : 'same'}-url-after-refresh`);
      await expect.poll(() => images.requests.length, { message: 'A successful explicit refresh must retry the failed related cover' }).toBe(2);
      expect(images.requests[1].path).toBe(new URL(newUrl).pathname);
      images.requests[1].complete();
      await expectLoaded(related.locator('img'));
      await screenshot(page, `related-${changedUrl ? 'changed' : 'same'}-url-recovered`);
    } finally { images.finishAll(); }
  });
}

