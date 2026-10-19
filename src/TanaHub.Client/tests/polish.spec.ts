import { test, expect, media } from './fixtures';
import type { MockLibrary } from './fixtures';
import type { Media } from '../src/lib/types';
import type { Page } from '@playwright/test';

const title = "Frieren: Beyond Journey's End";
const id = 'anilist:154587';

async function openTitle(page: Page) {
  await page.goto('/#library');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  const detail = page.getByRole('article', { name: title, exact: true }).filter({ has: page.getByRole('heading', { name: title, exact: true }) });
  await expect(detail.getByRole('spinbutton', { name: 'Set progress', exact: true })).toBeEnabled();
  return detail;
}

function replaceMedia(library: MockLibrary, changes: Partial<Media>) {
  const saved = library.entries.get(id)!;
  const updated = { ...saved.media, ...changes };
  library.catalog.set(id, updated);
  library.entries.set(id, { ...saved, media: updated });
}

function adversarialTitles(library: MockLibrary) {
  const saved = library.entries.get(id)!;
  library.entries.clear();
  library.catalog.clear();
  const titles = [
    media('qa:short', 'Short', { format: 'Unknown', year: null, totalUnits: null, posterUrl: null }),
    media('qa:long', 'A very long title that occupies two whole lines and continues after the clamp', { format: 'TvShort', totalUnits: 1234 }),
    media('qa:word', 'ThisIsAnExtremelyLongUnbrokenTitleWithoutAnySpacesThatMustNotPushTheWidth', { totalUnits: null }),
    media('qa:normal', 'Normal', { totalUnits: 12 }),
  ];
  for (const [index, item] of titles.entries()) {
    library.catalog.set(item.id, item);
    library.entries.set(item.id, { ...saved, mediaId: item.id, media: item, score: index % 2 ? 0 : null, status: index === 0 ? 'Completed' : 'Watching' });
  }
  return titles;
}

for (const width of [360, 1440]) {
  test(`grid metadata and title lengths keep action baselines aligned at ${width}px`, async ({ page, library }) => {
    adversarialTitles(library);
    await page.setViewportSize({ width, height: 1000 });
    await page.goto('/#library');
    await expect(page.locator('.media-card')).toHaveCount(4);
    await page.mouse.move(0, 0);
    const offsets = await page.locator('.media-card').evaluateAll(cards => cards.map(card => {
      const parent = card.getBoundingClientRect();
      const title = card.querySelector('.title-button')!.getBoundingClientRect();
      const status = card.querySelector('.tracking-row')!.getBoundingClientRect();
      const rating = card.querySelector('.card-bottom')!.getBoundingClientRect();
      return { height: parent.height, titleHeight: title.height, statusOffset: status.top - parent.top, ratingOffset: rating.top - parent.top };
    }));
    for (const key of ['height', 'titleHeight', 'statusOffset', 'ratingOffset'] as const) {
      expect(Math.max(...offsets.map(row => row[key])) - Math.min(...offsets.map(row => row[key])), `${key} must remain stable for long, unknown and short metadata`).toBeLessThanOrEqual(1);
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
    library.entries.delete('qa:short');
    library.entries.delete('qa:normal');
    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Discover', exact: true }).click();
    await expect(page.locator('.media-card')).toHaveCount(4);
    await expect(page.getByRole('button', { name: 'Add Short to library', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add Normal to library', exact: true })).toBeVisible();
    await expect(page.locator('.media-card .catalog-bottom')).toHaveCount(2);
    await expect(page.locator('.media-card .rating-button')).toHaveCount(2);
    const mixedOffsets = await page.locator('.media-card').evaluateAll(cards => cards.map(card => {
      const parent = card.getBoundingClientRect();
      const title = card.querySelector('.title-button')!.getBoundingClientRect();
      const bottom = card.querySelector('.card-bottom')!.getBoundingClientRect();
      return { height: parent.height, titleHeight: title.height, actionOffset: bottom.top - parent.top };
    }));
    for (const key of ['height', 'titleHeight', 'actionOffset'] as const) {
      expect(Math.max(...mixedOffsets.map(row => row[key])) - Math.min(...mixedOffsets.map(row => row[key])), `${key} must remain stable between tracked and untracked catalog titles`).toBeLessThanOrEqual(1);
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
  });
}

test('direct progress saves on Enter, does not save on blur, and Escape cancels only the draft', async ({ page, library }) => {
  const detail = await openTitle(page);
  const input = detail.getByRole('spinbutton', { name: 'Set progress', exact: true });
  await input.fill('11');
  await detail.getByRole('heading', { name: 'Your tracking', exact: true }).click();
  expect(library.requests.filter(request => request.method === 'library.progress.set')).toHaveLength(0);
  expect(library.entries.get(id)?.progress).toBe(3);
  await expect(detail.getByRole('button', { name: '+1 episode', exact: true })).toBeDisabled();
  await input.focus();
  await page.keyboard.press('Escape');
  await expect(input).toHaveValue('3');
  await expect(detail.getByRole('button', { name: '+1 episode', exact: true })).toBeEnabled();
  await input.fill('5');
  await detail.getByRole('button', { name: 'Cancel progress edit', exact: true }).click();
  await expect(input).toHaveValue('3');
  await expect(input).toBeFocused();
  expect(library.requests.filter(request => request.method === 'library.progress.set')).toHaveLength(0);
  await input.fill('11');
  await page.keyboard.press('Enter');
  await expect.poll(() => library.entries.get(id)?.progress).toBe(11);
  await expect(input).toHaveValue('11');
  await page.reload();
  await expect(input).toHaveValue('11');
});

test('saving progress locks conflicting actions and a failure keeps the draft for retry', async ({ page, library }) => {
  const original = library.entries.get(id)!;
  let finish!: () => void;
  const pending = new Promise<void>(resolve => { finish = resolve; });
  // The first controlled request succeeds only after the user-visible pending state.
  library.override = async ({ method, params }) => {
    if (method !== 'library.progress.set') return undefined;
    await pending;
    const updated = { ...original, progress: Number(params.progress) };
    library.entries.set(id, updated);
    return updated;
  };
  const detail = await openTitle(page);
  const input = detail.getByRole('spinbutton', { name: 'Set progress', exact: true });
  await input.fill('7');
  await input.press('Enter');
  await expect.poll(() => library.requests.filter(request => request.method === 'library.progress.set').length).toBe(1);
  await expect(input).toBeDisabled();
  await expect(detail.getByRole('combobox', { name: 'Library status', exact: true })).toBeDisabled();
  await expect(detail.getByRole('button', { name: `Rate ${title}`, exact: true })).toBeDisabled();
  finish();
  await expect(input).toBeEnabled();
  expect(library.entries.get(id)?.progress).toBe(7);
  library.override = null;
  library.failNext('library.progress.set', 'Progress could not be saved.');
  await input.fill('9');
  await input.press('Enter');
  await expect(detail.getByRole('alert')).toContainText('Progress could not be saved.');
  expect(library.entries.get(id)?.progress).toBe(7);
  await expect(input).toHaveValue('9');
  await input.press('Enter');
  await expect.poll(() => library.entries.get(id)?.progress).toBe(9);
  await expect(detail.getByRole('alert')).toHaveCount(0);
});

test('Completed with zero progress stays intact and reset needs explicit confirmation', async ({ page, library }) => {
  const saved = library.entries.get(id)!;
  library.entries.set(id, { ...saved, status: 'Completed', progress: 0 });
  const detail = await openTitle(page);
  await expect(detail.getByRole('combobox', { name: 'Library status', exact: true })).toHaveValue('Completed');
  expect(library.entries.get(id)?.progress).toBe(0);
  expect(library.requests.filter(request => ['library.progress.set', 'library.status'].includes(request.method))).toHaveLength(0);
  await detail.getByRole('button', { name: '+1 episode', exact: true }).click();
  await expect.poll(() => library.entries.get(id)?.progress).toBe(1);
  await detail.getByText('More progress actions', { exact: true }).click();
  await detail.getByRole('button', { name: 'Reset', exact: true }).click();
  expect(library.entries.get(id)?.progress).toBe(1);
  await detail.getByRole('button', { name: 'Keep progress', exact: true }).click();
  expect(library.entries.get(id)?.progress).toBe(1);
  await expect(detail.getByRole('spinbutton', { name: 'Set progress', exact: true })).toBeFocused();
  await detail.getByRole('button', { name: 'Reset', exact: true }).click();
  await detail.getByRole('button', { name: 'Reset progress', exact: true }).click();
  await expect.poll(() => library.entries.get(id)?.progress).toBe(0);
  expect(library.entries.get(id)?.status).toBe('Completed');
  await expect(detail.getByRole('spinbutton', { name: 'Set progress', exact: true })).toBeFocused();
});

test('title rating preserves a failed draft and distinguishes zero from clearing', async ({ page, library }) => {
  const detail = await openTitle(page);
  const trigger = detail.getByRole('button', { name: `Rate ${title}`, exact: true });
  await trigger.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  const slider = dialog.getByRole('slider', { name: 'Your rating out of 10', exact: true });
  library.failNext('library.score', 'This rating could not be saved.');
  await slider.press('Home');
  expect(library.requests.filter(request => request.method === 'library.score')).toHaveLength(0);
  await dialog.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect(dialog.getByRole('alert')).toContainText('This rating could not be saved.');
  await expect(slider).toHaveValue('0');
  await expect(slider).toBeFocused();
  expect(library.entries.get(id)?.score).toBe(9);
  await dialog.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  expect(library.entries.get(id)?.score).toBe(0);
  await expect(trigger).toBeFocused();
  await page.reload();
  await expect(trigger).toContainText('0');
  await trigger.click();
  await dialog.getByRole('button', { name: 'Clear', exact: true }).click();
  await expect(dialog.getByText('Not rated yet', { exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(trigger).toContainText('Add a rating');
  expect(library.entries.get(id)?.score).toBeNull();
  expect(library.requests.filter(request => request.method === 'library.score').at(-1)?.params.score).toBeNull();
});

test('cover lightbox traps focus, closes with Escape or outside click, and restores its opener', async ({ page, library }) => {
  const detail = await openTitle(page);
  const opener = detail.getByRole('button', { name: `Enlarge cover of ${title}`, exact: true });
  await opener.click();
  const dialog = page.getByRole('dialog', { name: `Cover of ${title}`, exact: true });
  await expect(dialog).toBeVisible();
  const cover = dialog.getByRole('img');
  const bounds = await cover.boundingBox();
  expect(bounds?.width).toBeGreaterThan(140);
  expect(bounds?.height).toBeLessThanOrEqual(1000);
  for (let index = 0; index < 4; index += 1) {
    await page.keyboard.press('Tab');
    expect(await dialog.evaluate(node => node.contains(document.activeElement))).toBe(true);
  }
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(opener).toBeFocused();
  await opener.click();
  await expect(dialog).toBeVisible();
  await page.mouse.click(2, 2);
  await expect(dialog).not.toBeVisible();
  await expect(opener).toBeFocused();
});

for (const posterUrl of [null, 'data:image/png;base64,invalid']) {
  test(`${posterUrl === null ? 'missing' : 'failed'} cover never offers an empty lightbox`, async ({ page, library }) => {
    replaceMedia(library, { posterUrl });
    const detail = await openTitle(page);
    await expect(detail.getByRole('button', { name: `Enlarge cover of ${title}`, exact: true })).toHaveCount(0);
    await expect(page.getByRole('dialog', { name: `Cover of ${title}`, exact: true })).toHaveCount(0);
  });
}

test('long synopsis can be expanded and folded with an announced state', async ({ page, library }) => {
  replaceMedia(library, { description: Array.from({ length: 30 }, (_, index) => `Story paragraph ${index + 1}: an exceptionally long journey through unfamiliar places.`).join('\n\n') });
  const detail = await openTitle(page);
  const section = detail.getByRole('region', { name: 'Synopsis', exact: true });
  const more = detail.getByRole('button', { name: 'Show more', exact: true });
  await expect(more).toHaveAttribute('aria-expanded', 'false');
  const foldedHeight = (await section.boundingBox())!.height;
  await more.click();
  const less = detail.getByRole('button', { name: 'Show less', exact: true });
  await expect(less).toHaveAttribute('aria-expanded', 'true');
  expect((await section.boundingBox())!.height).toBeGreaterThan(foldedHeight + 300);
  await less.click();
  await expect(more).toHaveAttribute('aria-expanded', 'false');
  expect((await section.boundingBox())!.height).toBeLessThanOrEqual(foldedHeight + 1);
});

for (const reject of [false, true]) {
  test(`copy title ${reject ? 'reports a clipboard failure' : 'copies the complete title'} without leaving details`, async ({ page, library }) => {
    await page.addInitScript(({ reject }) => {
      Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {
        writeText: async (value: string) => {
          if (reject) throw new DOMException('Clipboard unavailable', 'NotAllowedError');
          Object.assign(window, { copiedFixtureTitle: value });
        },
      } });
    }, { reject });
    const detail = await openTitle(page);
    await detail.getByRole('button', { name: 'Copy title', exact: true }).click();
    if (reject) await expect(detail.getByRole('alert').filter({ hasText: /copy|clipboard/i })).toBeVisible();
    else {
      await expect(detail.getByRole('status').filter({ hasText: /copied/i })).toBeVisible();
      expect(await page.evaluate(() => Reflect.get(window, 'copiedFixtureTitle'))).toBe(title);
    }
    await expect(detail.getByRole('heading', { name: title, exact: true })).toBeVisible();
    await expect(detail.getByRole('spinbutton', { name: 'Set progress', exact: true })).toBeEnabled();
  });
}

test('320px title tools and enlarged cover stay inside the window with reduced motion', async ({ page, library }) => {
  await page.setViewportSize({ width: 320, height: 800 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const detail = await openTitle(page);
  await detail.getByRole('button', { name: `Rate ${title}`, exact: true }).click();
  const rating = page.getByRole('dialog', { name: 'Your rating', exact: true });
  await rating.getByRole('slider', { name: 'Your rating out of 10', exact: true }).press('Home');
  await rating.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect(rating).not.toBeVisible();
  const input = detail.getByRole('spinbutton', { name: 'Set progress', exact: true });
  await input.fill('2147483647');
  await input.press('Escape');
  await expect(input).toHaveValue('3');
  expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
  await detail.getByRole('button', { name: `Enlarge cover of ${title}`, exact: true }).click();
  const dialog = page.getByRole('dialog', { name: `Cover of ${title}`, exact: true });
  await expect(dialog).toBeVisible();
  const cover = (await dialog.getByRole('img').boundingBox())!;
  expect(cover.x).toBeGreaterThanOrEqual(0);
  expect(cover.y).toBeGreaterThanOrEqual(0);
  expect(cover.x + cover.width).toBeLessThanOrEqual(320);
  expect(cover.y + cover.height).toBeLessThanOrEqual(800);
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
});

test('rating keyboard selection is a draft until saved and Escape discards it', async ({ page, library }) => {
  const detail = await openTitle(page);
  const trigger = detail.getByRole('button', { name: `Rate ${title}`, exact: true });
  await trigger.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  const slider = dialog.getByRole('slider', { name: 'Your rating out of 10', exact: true });
  await expect(slider).toBeFocused();
  await slider.press('ArrowLeft');
  await expect(slider).toHaveValue('8');
  await slider.press('Home');
  await expect(slider).toHaveValue('0');
  await slider.press('End');
  await expect(slider).toHaveValue('10');
  expect(library.entries.get(id)?.score).toBe(9);
  expect(library.requests.filter(request => request.method === 'library.score')).toHaveLength(0);
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(trigger).toBeFocused();
  await trigger.click();
  await expect(slider).toHaveValue('9');
  await dialog.getByRole('button', { name: 'Increase rating', exact: true }).click();
  await dialog.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  expect(library.entries.get(id)?.score).toBe(10);
});

test('saving from the disappearing Apply action restores focus to the progress input', async ({ page, library }) => {
  const detail = await openTitle(page);
  const input = detail.getByRole('spinbutton', { name: 'Set progress', exact: true });
  await input.fill('11');
  await detail.getByRole('button', { name: 'Set progress', exact: true }).click();
  await expect.poll(() => library.entries.get(id)?.progress).toBe(11);
  await expect(detail.getByRole('button', { name: 'Set progress', exact: true })).toHaveCount(0);
  await expect(input).toBeFocused();
});

test('an unsaved progress draft survives unrelated rating and status saves', async ({ page, library }) => {
  const detail = await openTitle(page);
  const input = detail.getByRole('spinbutton', { name: 'Set progress', exact: true });
  await input.fill('11');
  await detail.getByRole('button', { name: `Rate ${title}`, exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  await dialog.getByRole('slider', { name: 'Your rating out of 10', exact: true }).press('ArrowLeft');
  await dialog.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect.poll(() => library.entries.get(id)?.score).toBe(8);
  await expect(input).toHaveValue('11');
  const status = detail.getByRole('combobox', { name: 'Library status', exact: true });
  await status.selectOption('Paused');
  await expect(status).toHaveValue('Paused');
  await expect(input).toHaveValue('11');
  expect(library.entries.get(id)?.progress).toBe(3);
  expect(library.requests.filter(request => request.method === 'library.progress.set')).toHaveLength(0);
  await input.focus();
  await input.press('Escape');
  await expect(input).toHaveValue('3');
  expect(library.entries.get(id)?.status).toBe('Paused');
  expect(library.entries.get(id)?.score).toBe(8);
});


test('rating blocks duplicate saves and dismissal while its request is pending', async ({ page, library }) => {
  const original = library.entries.get(id)!;
  let finish!: () => void;
  const pending = new Promise<void>(resolve => { finish = resolve; });
  library.override = async ({ method, params }) => {
    if (method !== 'library.score') return undefined;
    await pending;
    const updated = { ...original, score: Number(params.score) };
    library.entries.set(id, updated);
    return updated;
  };
  const detail = await openTitle(page);
  const trigger = detail.getByRole('button', { name: `Rate ${title}`, exact: true });
  await trigger.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  const slider = dialog.getByRole('slider', { name: 'Your rating out of 10', exact: true });
  await slider.press('ArrowLeft');
  await dialog.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect.poll(() => library.requests.filter(request => request.method === 'library.score').length).toBe(1);
  await expect(slider).toBeDisabled();
  await expect(dialog.getByRole('button', { name: 'Cancel', exact: true })).toBeDisabled();
  await expect(dialog.getByRole('button', { name: 'Close rating', exact: true })).toBeDisabled();
  await page.keyboard.press('Escape');
  await page.mouse.click(2, 2);
  await page.keyboard.press('Enter');
  await expect(dialog).toBeVisible();
  expect(library.requests.filter(request => request.method === 'library.score')).toHaveLength(1);
  finish();
  await expect(dialog).not.toBeVisible();
  await expect(trigger).toBeFocused();
  expect(library.entries.get(id)?.score).toBe(8);
});

test('rating keeps actions visible in a short window and Cancel discards the draft', async ({ page, library }) => {
  await page.setViewportSize({ width: 768, height: 360 });
  const detail = await openTitle(page);
  const trigger = detail.getByRole('button', { name: `Rate ${title}`, exact: true });
  await trigger.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  const initialSlider = (await dialog.getByRole('slider', { name: 'Your rating out of 10', exact: true }).boundingBox())!;
  const body = (await dialog.locator('.rating-body').boundingBox())!;
  expect(initialSlider.y).toBeGreaterThanOrEqual(body.y);
  expect(initialSlider.y + initialSlider.height).toBeLessThanOrEqual(body.y + body.height);
  await dialog.getByRole('slider', { name: 'Your rating out of 10', exact: true }).press('ArrowLeft');
  const save = (await dialog.getByRole('button', { name: 'Save rating', exact: true }).boundingBox())!;
  expect(save.y).toBeGreaterThanOrEqual(16);
  expect(save.y + save.height).toBeLessThanOrEqual(344);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(trigger).toBeFocused();
  expect(library.entries.get(id)?.score).toBe(9);
  expect(library.requests.filter(request => request.method === 'library.score')).toHaveLength(0);
});

test('rating boundary and Clear actions keep keyboard focus inside the modal', async ({ page, library }) => {
  const detail = await openTitle(page);
  await detail.getByRole('button', { name: `Rate ${title}`, exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  const slider = dialog.getByRole('slider', { name: 'Your rating out of 10', exact: true });
  const plus = dialog.getByRole('button', { name: 'Increase rating', exact: true });
  await plus.focus();
  await page.keyboard.press('Space');
  await expect(slider).toHaveValue('10');
  await expect(plus).toBeDisabled();
  await expect(slider).toBeFocused();
  await slider.press('Home');
  await slider.press('ArrowRight');
  const minus = dialog.getByRole('button', { name: 'Decrease rating', exact: true });
  await minus.focus();
  await page.keyboard.press('Space');
  await expect(slider).toHaveValue('0');
  await expect(minus).toBeDisabled();
  await expect(slider).toBeFocused();
  const clear = dialog.getByRole('button', { name: 'Clear', exact: true });
  await clear.focus();
  await page.keyboard.press('Space');
  await expect(clear).toBeDisabled();
  await expect(slider).toBeFocused();
  await expect(slider).toHaveAttribute('aria-valuetext', 'Not rated');
  await page.keyboard.press('Escape');
  expect(library.entries.get(id)?.score).toBe(9);
});
