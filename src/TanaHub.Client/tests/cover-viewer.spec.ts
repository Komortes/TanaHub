import { mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';
import type { Locator, Page, Route } from '@playwright/test';
import { test, expect } from './fixtures';

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
  await page.screenshot({ path, fullPage: false });
}

async function expectLoaded(image: Locator) {
  await expect(image).toBeVisible();
  await expect.poll(() => image.evaluate(node => (node as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  await expect(image).toHaveCSS('opacity', '1');
}

for (const width of [1440, 320]) {
  test(`the enlarged cover has a stable loading state at ${width}px and displays the original`, async ({ page, library }) => {
    await page.setViewportSize({ width, height: 1000 });
    const item = library.catalog.get('anilist:154587')!;
    item.posterUrl = `${artworkHost}/lightbox/original.svg`;
    const images = await controlImages(page);
    try {
      await page.goto(`/#title/${encodeURIComponent(item.id)}`, { waitUntil: 'domcontentloaded' });
      await expect.poll(() => images.requests.length).toBe(1);
      // The header can be enlarged while its original is still downloading.
      const opener = page.getByRole('button', { name: `Enlarge cover of ${item.title}`, exact: true });
      await opener.click();
      const dialog = page.getByRole('dialog', { name: `Cover of ${item.title}`, exact: true });
      await expect(dialog.getByRole('img')).toHaveAttribute('src', item.posterUrl);
      await screenshot(page, `lightbox-${width}-loading`);
      await expect.soft(dialog.getByRole('status')).toHaveText('Loading cover…');
      const pendingBounds = (await dialog.boundingBox())!;
      expect.soft(pendingBounds.width, 'The loading viewer must reserve a usable cover area').toBeGreaterThanOrEqual(240);
      expect.soft(pendingBounds.height, 'The loading viewer must not collapse around its caption').toBeGreaterThanOrEqual(300);
      const close = dialog.getByRole('button', { name: 'Close cover', exact: true });
      await expect(close).toBeFocused();
      await page.keyboard.press('Tab');
      await expect(close).toBeFocused();
      images.requests[0].complete();
      await expectLoaded(dialog.getByRole('img', { name: `Cover of ${item.title}`, exact: true }));
      await screenshot(page, `lightbox-${width}-loaded`);
      await page.keyboard.press('Escape');
      await expect(dialog).not.toBeVisible();
      await expect(opener).toBeFocused();
    } finally { images.finishAll(); }
  });
}

test('the header uses a preview and a failed enlarged original retries without losing keyboard focus', async ({ page, library }) => {
  const item = library.catalog.get('anilist:154587')!;
  item.previewUrl = `${artworkHost}/lightbox/card.svg`;
  item.thumbnailUrl = `${artworkHost}/lightbox/thumb.svg`;
  item.posterUrl = `${artworkHost}/lightbox/retry.svg`;
  const images = await controlImages(page);
  try {
    await page.goto(`/#title/${encodeURIComponent(item.id)}`, { waitUntil: 'domcontentloaded' });
    await expect.poll(() => images.requests.length).toBe(1);
    await screenshot(page, 'lightbox-header-source');
    expect(images.requests[0].path, 'The 140px header should load a preview before the original is enlarged').toBe('/lightbox/card.svg');
    images.requests[0].complete();
    await expectLoaded(page.getByRole('img', { name: `Cover of ${item.title}`, exact: true }));
    const opener = page.getByRole('button', { name: `Enlarge cover of ${item.title}`, exact: true });
    await opener.click();
    const dialog = page.getByRole('dialog', { name: `Cover of ${item.title}`, exact: true });
    await expect.poll(() => images.requests.length).toBe(2);
    expect(images.requests[1].path).toBe('/lightbox/retry.svg');
    images.requests[1].complete({ contentType: 'image/jpeg', body: 'temporarily invalid image bytes' });
    await expect(dialog.getByRole('status')).toContainText('This cover could not be loaded.');
    await screenshot(page, 'lightbox-failed');
    const retry = dialog.getByRole('button', { name: 'Retry cover', exact: true });
    await expect(retry).toBeVisible();
    await retry.focus();
    await page.keyboard.press('Enter');
    await expect.poll(() => images.requests.length).toBe(3);
    await expect(dialog).toBeVisible();
    expect(await dialog.evaluate(node => node.contains(document.activeElement)), 'Retry must leave keyboard focus inside the viewer').toBe(true);
    await screenshot(page, 'lightbox-retrying');
    images.requests[2].complete();
    await expectLoaded(dialog.getByRole('img', { name: `Cover of ${item.title}`, exact: true }));
    expect(await dialog.evaluate(node => node.contains(document.activeElement))).toBe(true);
    await screenshot(page, 'lightbox-recovered');
    await page.keyboard.press('Escape');
    await expect(opener).toBeFocused();
  } finally { images.finishAll(); }
});

for (const recover of [false, true]) {
  test(`a shared original header failure keeps the viewer open to ${recover ? 'retry and restore the cover' : 'close with a usable focus target'}`, async ({ page, library }) => {
    const item = library.catalog.get('anilist:154587')!;
    item.posterUrl = `${artworkHost}/lightbox/shared-original.svg`;
    const images = await controlImages(page);
    try {
      await page.goto(`/#title/${encodeURIComponent(item.id)}`, { waitUntil: 'domcontentloaded' });
      await expect.poll(() => images.requests.length).toBe(1);
      const opener = page.getByRole('button', { name: `Enlarge cover of ${item.title}`, exact: true });
      await opener.click();
      const dialog = page.getByRole('dialog', { name: `Cover of ${item.title}`, exact: true });
      await expect(dialog.getByRole('img')).toHaveAttribute('src', item.posterUrl);
      images.requests[0].complete({ contentType: 'image/jpeg', body: 'temporarily invalid image bytes' });
      await expect(dialog.getByRole('status')).toContainText('This cover could not be loaded.');
      await expect(opener).toHaveCount(0);
      if (recover) {
        const retry = dialog.getByRole('button', { name: 'Retry cover', exact: true });
        await retry.focus(); await page.keyboard.press('Enter');
        await expect.poll(() => images.requests.length).toBe(2);
        images.finishAll();
        await expectLoaded(dialog.getByRole('img'));
        await expect(opener).toBeVisible();
        await screenshot(page, 'lightbox-shared-original-recovered');
      }
      await page.keyboard.press('Escape');
      await expect(dialog).not.toBeVisible();
      if (recover) await expect(opener).toBeFocused();
      else await expect(page.getByRole('heading', { name: item.title, exact: true })).toBeFocused();
    } finally { images.finishAll(); }
  });
}

test('background catalog details do not dismiss an already open cover', async ({ page, library }) => {
  const item = library.catalog.get('anilist:154587')!;
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  library.override = async ({ method }) => {
    if (method !== 'media.detail') return undefined;
    await gate;
    return { ...item, description: 'Refreshed catalog details.' };
  };
  try {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: `Open ${item.title}`, exact: true }).click();
    await expect(page.getByText('Loading full details…', { exact: true })).toBeVisible();
    const opener = page.getByRole('button', { name: `Enlarge cover of ${item.title}`, exact: true });
    await opener.click();
    const dialog = page.getByRole('dialog', { name: `Cover of ${item.title}`, exact: true });
    await expectLoaded(dialog.getByRole('img'));
    release();
    await expect(page.locator('#title-synopsis')).toHaveText('Refreshed catalog details.');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Close cover', exact: true })).toBeFocused();
    await screenshot(page, 'lightbox-after-catalog-refresh');
    await page.keyboard.press('Escape');
    await expect(opener).toBeFocused();
  } finally { release(); }
});
