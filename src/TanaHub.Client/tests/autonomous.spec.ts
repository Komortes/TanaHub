import { test, expect, media } from './fixtures';

const title = "Frieren: Beyond Journey's End";
const id = 'anilist:154587';

test('failed library read blocks tracking until retry confirms the saved entry', async ({ page, library }) => {
  library.failNext('library.get', 'Library status could not be read.');
  await page.goto('/');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await expect(page.getByText('Could not load library status. Please try again.', { exact: true })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: `Rate ${title}`, exact: true })).toBeDisabled();
  expect(library.entries.get(id)?.progress).toBe(3);
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toBeEnabled();
  await expect(page.getByRole('button', { name: `Rate ${title}`, exact: true })).toBeEnabled();
});

test('an unknown library status never offers an enabled add action', async ({ page, library }) => {
  library.entries.delete(id);
  library.failNext('library.get', 'Library status could not be read.');
  await page.goto('/#discover');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await expect(page.getByText('Could not load library status. Please try again.', { exact: true })).toBeVisible();
  const add = page.getByRole('button', { name: 'Add to library', exact: true });
  await expect(add).toBeDisabled();
  expect(library.requests.some(request => request.method === 'library.add')).toBe(false);
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(add).toBeEnabled();
  await add.click();
  await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toHaveValue('Planning');
  expect(library.entries.has(id)).toBe(true);
});

test('review typing cannot exceed the storage limit and the boundary value saves intact', async ({ page, library }) => {
  await page.goto('/');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await page.getByRole('button', { name: 'Add review', exact: true }).click();
  const review = page.getByRole('textbox', { name: 'Review', exact: true });
  const text = 'a'.repeat(4000);
  await review.fill(text);
  await review.press('End');
  await review.press('a');
  await expect.poll(async () => (await review.inputValue()).length).toBe(4000);
  await expect(page.getByText('4,000 / 4,000', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Save review', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Save review', exact: true })).toBeDisabled();
  expect(library.entries.get(id)?.review).toBe(text);
  await page.reload();
  await page.getByRole('button', { name: 'Edit review', exact: true }).click();
  await expect(review).toHaveValue(text);
});

test('an oversized saved review remains intact and must be shortened before saving', async ({ page, library }) => {
  library.entries.set(id, { ...library.entries.get(id)!, review: 'a'.repeat(4500) });
  await page.goto('/');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await page.getByRole('button', { name: 'Edit review', exact: true }).click();
  const review = page.getByRole('textbox', { name: 'Review', exact: true });
  await expect(review).toHaveValue('a'.repeat(4500));
  await expect(page.getByText('Review must be 4,000 characters or fewer.', { exact: true })).toBeVisible();
  const save = page.getByRole('button', { name: 'Save review', exact: true });
  await expect(save).toBeDisabled();
  await review.fill('Shortened review');
  await expect(save).toBeEnabled();
  await save.click();
  await expect(save).toBeDisabled();
  expect(library.entries.get(id)?.review).toBe('Shortened review');
});

test.describe('tracking dates', () => {
  test.use({ timezoneId: 'America/Los_Angeles' });

  test('date drafts survive collapse and failed saves; untouched timestamps and other tracking persist', async ({ page, library }) => {
    const completedAt = '2026-10-10T22:30:00Z';
    library.entries.set(id, { ...library.entries.get(id)!, startedAt: '2026-10-01T21:45:00Z', completedAt });
    await page.goto('/');
    await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
    await page.getByRole('button', { name: 'Edit dates', exact: true }).click();
    const start = page.getByLabel('Started on', { exact: true });
    const completion = page.getByLabel('Completed on', { exact: true });
    await expect(start).toHaveValue('2026-10-01');
    await expect(completion).toHaveValue('2026-10-10');
    await start.fill('2026-09-28');
    await page.getByRole('button', { name: 'Close dates editor', exact: true }).click();
    await page.getByRole('button', { name: 'Continue editing dates', exact: true }).click();
    await expect(start).toHaveValue('2026-09-28');
    library.failNext('library.dates', 'Dates could not be saved.');
    const save = page.getByRole('button', { name: 'Save dates', exact: true });
    await save.click();
    await expect(page.getByRole('alert')).toHaveText('Dates could not be saved.');
    await expect(start).toHaveValue('2026-09-28');
    expect(library.entries.get(id)?.startedAt).toBe('2026-10-01T21:45:00Z');
    await save.click();
    await expect(save).toBeDisabled();
    expect(library.entries.get(id)).toMatchObject({ startedAt: '2026-09-28T07:00:00.000Z', completedAt, score: 9, progress: 3, status: 'Watching' });
    await page.reload();
    await page.getByRole('button', { name: 'Edit dates', exact: true }).click();
    await expect(start).toHaveValue('2026-09-28');
    await page.getByRole('button', { name: 'Clear dates', exact: true }).click();
    expect(library.entries.get(id)?.completedAt).toBe(completedAt);
    await save.click();
    await expect(save).toBeDisabled();
    expect(library.entries.get(id)).toMatchObject({ startedAt: null, completedAt: null, score: 9, progress: 3, status: 'Watching' });
  });

  test('date chronology blocks invalid writes and permits completion on the start day', async ({ page, library }) => {
    library.entries.set(id, { ...library.entries.get(id)!, startedAt: '2026-10-10T23:30:00Z' });
    await page.goto('/');
    await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
    await page.getByRole('button', { name: 'Edit dates', exact: true }).click();
    const completion = page.getByLabel('Completed on', { exact: true });
    const save = page.getByRole('button', { name: 'Save dates', exact: true });
    await completion.fill('2026-10-09');
    await expect(page.getByText('Completion date cannot be before the start date.', { exact: true })).toBeVisible();
    await expect(save).toBeDisabled();
    expect(library.requests.some(request => request.method === 'library.dates')).toBe(false);
    await completion.fill('2026-10-10');
    await save.click();
    await expect(save).toBeDisabled();
    const saved = library.entries.get(id)!;
    expect(saved.startedAt).toBe('2026-10-10T23:30:00Z');
    expect(Date.parse(saved.completedAt!)).toBeGreaterThanOrEqual(Date.parse(saved.startedAt!));
    await expect(completion).toHaveValue('2026-10-10');
  });

  test('a status-triggered saved date cannot overwrite a date draft; discard uses the latest value', async ({ page, library }) => {
    library.override = async request => {
      if (request.method !== 'library.status') return undefined;
      const updated = { ...library.entries.get(id)!, status: 'Completed' as const, completedAt: '2026-10-11T18:00:00Z' };
      library.entries.set(id, updated);
      return updated;
    };
    await page.goto('/');
    await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
    await page.getByRole('button', { name: 'Add dates', exact: true }).click();
    const completion = page.getByLabel('Completed on', { exact: true });
    await completion.fill('2026-10-05');
    await page.getByRole('combobox', { name: 'Library status', exact: true }).selectOption('Completed');
    await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toBeEnabled();
    await expect(completion).toHaveValue('2026-10-05');
    await page.getByRole('button', { name: 'Discard dates draft', exact: true }).click();
    await page.getByRole('button', { name: 'Edit dates', exact: true }).click();
    await expect(completion).toHaveValue('2026-10-11');
    expect(library.requests.some(request => request.method === 'library.dates')).toBe(false);
  });

  test('empty date fields explicitly show that no date is recorded until a draft is selected', async ({ page, library }) => {
    await page.goto('/');
    await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
    await page.getByRole('button', { name: 'Add dates', exact: true }).click();
    await expect(page.getByText('No start date recorded', { exact: true })).toBeVisible();
    await expect(page.getByText('No completion date recorded', { exact: true })).toBeVisible();
    await page.getByLabel('Started on', { exact: true }).fill('2026-10-01');
    await expect(page.getByText('No start date recorded', { exact: true })).not.toBeVisible();
    await expect(page.getByText('No completion date recorded', { exact: true })).toBeVisible();
    expect(library.requests.some(request => request.method === 'library.dates')).toBe(false);
  });

  for (const width of [360, 1440]) {
    test(`date editor stays readable and keyboard usable at ${width}px`, async ({ page, library }) => {
      await page.setViewportSize({ width, height: 1000 });
      await page.goto('/');
      await expect(page).toHaveTitle('My library · TanaHub');
      await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
      await page.getByRole('button', { name: 'Add dates', exact: true }).click();
      await expect(page.getByLabel('Started on', { exact: true })).toBeFocused();
      await page.getByLabel('Started on', { exact: true }).fill('2026-10-01');
      await page.getByLabel('Completed on', { exact: true }).fill('2026-10-11');
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
      await expect(page.locator('vite-error-overlay')).toHaveCount(0);
      await page.locator('.journal-panel').screenshot({ path: `/tmp/tanahub-dates-${width}.png` });
      await page.getByRole('button', { name: 'Discard dates draft', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Add dates', exact: true })).toBeFocused();
      expect(library.requests.some(request => request.method === 'library.dates')).toBe(false);
    });
  }
});

test('empty library status and hidden organization filters can be reset directly', async ({ page, library }) => {
  const manga = library.entries.get('anilist:30002')!;
  library.entries.set(manga.mediaId, { ...manga, tags: ['Manga only'] });
  await page.goto('/');
  await page.getByRole('button', { name: 'Dropped', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'No matching titles', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Reset filters', exact: true }).click();
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Anime', exact: true }).click();
  await page.getByRole('button', { name: 'Filters and display', exact: true }).click();
  await page.getByRole('combobox', { name: 'Filter by tag', exact: true }).selectOption('Manga only');
  await page.getByRole('button', { name: 'Filters and display, 1 active filters', exact: true }).click();
  await page.getByRole('button', { name: 'Reset filters', exact: true }).click();
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).toBeVisible();
  expect(library.requests.filter(request => request.method === 'library.list').at(-1)?.params.tag).toBeUndefined();
});

test('empty catalog genre filters offer a reset and recover results', async ({ page, library }) => {
  library.override = async request => request.method === 'catalog.search' && (request.params.genres as string[])?.includes('Horror')
    ? { items: [], page: 1, pageSize: 24, totalCount: 0, hasNextPage: false } : undefined;
  await page.goto('/#discover');
  await page.getByRole('combobox', { name: 'Filter catalog genre', exact: true }).selectOption('Horror');
  await expect(page.getByRole('heading', { name: 'No matching titles', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Reset filters', exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Filter catalog genre', exact: true })).toHaveValue('');
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).toBeVisible();
});

test('retrying a failed third library page retains earlier pages and retries the same page', async ({ page, library }) => {
  const seed = library.entries.get(id)!;
  for (let index = 0; index < 104; index += 1) {
    const item = media(`anilist:page-${index}`, `Pagination title ${index}`);
    library.catalog.set(item.id, item);
    library.entries.set(item.id, { ...seed, mediaId: item.id, media: item });
  }
  const total = library.entries.size;
  await page.goto('/');
  await expect(page.getByRole('article')).toHaveCount(48);
  await page.getByRole('button', { name: 'Load more titles', exact: true }).click();
  await expect(page.getByRole('article')).toHaveCount(96);
  library.failNext('library.list', 'This page could not load.');
  await page.getByRole('button', { name: 'Load more titles', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('This page could not load.');
  await expect(page.getByRole('article')).toHaveCount(96);
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByRole('article')).toHaveCount(total);
  expect(library.requests.filter(request => request.method === 'library.list').map(request => request.params.page)).toEqual([1, 2, 3, 3]);
  const titles = await page.getByRole('article').evaluateAll(cards => cards.map(card => card.getAttribute('aria-label')));
  expect(new Set(titles).size).toBe(total);
});

test('a failed new library filter cannot display titles from the previous filter', async ({ page, library }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).toBeVisible();
  library.failNext('library.list', 'Manga could not load.');
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Manga could not load.');
  await expect(page.getByRole('article')).toHaveCount(0);
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Open Berserk', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).not.toBeVisible();
});

test('a failed new catalog search hides previous results and retries the current search', async ({ page, library }) => {
  await page.goto('/#discover');
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).toBeVisible();
  library.failNext('catalog.search', 'The new search could not load.');
  await page.getByRole('searchbox', { name: 'Search the catalog', exact: true }).fill('Odd');
  await expect(page.getByRole('alert')).toContainText('The new search could not load.');
  await expect(page.getByRole('article')).toHaveCount(0);
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Open Odd Taxi', exact: true })).toBeVisible();
  expect(library.requests.filter(request => request.method === 'catalog.search').at(-1)?.params.search).toBe('Odd');
});

test('a failed Home type switch hides prior statistics and tracking until retry succeeds', async ({ page, library }) => {
  await page.goto('/#home');
  await expect(page.getByText('4 titles tracked', { exact: true })).toBeVisible();
  const tracking = page.getByRole('region', { name: 'Continue tracking', exact: true });
  await expect(tracking.getByRole('article', { name: title, exact: true })).toBeVisible();
  library.failNext('home.get', 'Manga statistics could not load.');
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Manga statistics could not load.');
  await expect(page.getByText('4 titles tracked', { exact: true })).not.toBeVisible();
  await expect(tracking.getByRole('article')).toHaveCount(0);
  await expect(tracking.getByText('Nothing in progress', { exact: true })).not.toBeVisible();
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
  await expect(tracking.getByRole('article', { name: 'Berserk', exact: true })).toBeVisible();
});

test('Home does not show previous-type recommendations while the new type is pending', async ({ page, library }) => {
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  library.override = async request => {
    if (request.method === 'home.recommendations' && request.params.type === 'Manga') await pending;
    return undefined;
  };
  await page.goto('/#home');
  const recommendations = page.getByRole('region', { name: 'Recommendations', exact: true });
  await expect(recommendations.getByRole('button', { name: 'Open Odd Taxi', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  try {
    await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
    await expect(recommendations.getByRole('button', { name: 'Open Odd Taxi', exact: true })).not.toBeVisible();
    await expect(recommendations.getByRole('status')).toHaveText('Finding recommendations…');
  } finally { release(); }
  await expect(recommendations.getByRole('button', { name: 'Open Dandadan', exact: true })).toBeVisible();
});

test('organization validates name lengths and counts before saving and removes case duplicates', async ({ page, library }) => {
  await page.goto('/');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await page.getByRole('button', { name: 'Add tags or lists', exact: true }).click();
  const tags = page.getByRole('textbox', { name: 'Tags', exact: true });
  const lists = page.getByRole('textbox', { name: 'Custom lists', exact: true });
  const save = page.getByRole('button', { name: 'Save organization', exact: true });
  const tooLong = 'a'.repeat(101);
  await tags.fill(tooLong);
  await expect(page.getByText('Each tag must be 100 characters or fewer.', { exact: true })).toBeVisible();
  await expect(save).toBeDisabled();
  await tags.press('Enter');
  await expect(tags).toHaveValue(tooLong);
  expect(library.requests.some(request => request.method === 'library.organization')).toBe(false);
  await tags.fill('Comfort, comfort, seasonal');
  const names = Array.from({ length: 51 }, (_, index) => `List ${index}`);
  await lists.fill(names.join(', '));
  await expect(page.getByText('Use up to 50 custom lists.', { exact: true })).toBeVisible();
  await expect(save).toBeDisabled();
  await lists.fill(names.slice(0, 50).join(', '));
  await save.click();
  await expect(save).toBeDisabled();
  expect(library.entries.get(id)).toMatchObject({ tags: ['Comfort', 'seasonal'], customLists: names.slice(0, 50), progress: 3, score: 9 });
});

test('editing custom lists preserves Unicode tags that the core treats as distinct', async ({ page, library }) => {
  const names = ['Straße', 'STRAẞE'];
  library.entries.set(id, { ...library.entries.get(id)!, tags: names });
  await page.goto('/');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await page.locator('button[aria-controls="organization-editor"]').click();
  await page.getByRole('textbox', { name: 'Custom lists', exact: true }).fill('Weekend');
  const save = page.getByRole('button', { name: 'Save organization', exact: true });
  await save.click();
  await expect(save).toBeDisabled();
  expect(library.entries.get(id)?.tags).toEqual(names);
  expect(library.entries.get(id)?.customLists).toEqual(['Weekend']);
});

test('a stored ID case variant remains tracked, editable and removable through canonical catalog metadata', async ({ page, library }) => {
  const storedId = id.toUpperCase();
  const saved = { ...library.entries.get(id)!, mediaId: storedId };
  library.entries.delete(id);
  library.entries.set(storedId, saved);
  // Match the actual core contract: lookups accept either spelling and preserve
  // the stored ID, while catalog media retain their canonical provider ID.
  library.override = async request => {
    if (String(request.params.mediaId).toLowerCase() !== id) return undefined;
    if (request.method === 'library.get') return library.entries.get(storedId) ?? null;
    if (request.method === 'library.score') {
      const updated = { ...library.entries.get(storedId)!, score: request.params.score as number };
      library.entries.set(storedId, updated);
      return updated;
    }
    if (request.method === 'library.remove') return { removed: library.entries.delete(storedId) };
    return undefined;
  };
  await page.goto('/');
  const rate = page.getByRole('button', { name: `Rate ${title}`, exact: true });
  await expect(rate).toBeVisible();
  await rate.click();
  const dialog = page.getByRole('dialog', { name: 'Your rating', exact: true });
  await dialog.getByRole('slider', { name: 'Your rating out of 10', exact: true }).press('ArrowLeft');
  await dialog.getByRole('button', { name: 'Save rating', exact: true }).click();
  await expect(rate).toContainText('8');
  expect(library.entries.get(storedId)?.mediaId).toBe(storedId);
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toHaveValue('Watching');
  await page.getByRole('button', { name: 'Remove from library', exact: true }).click();
  await page.getByRole('button', { name: 'Remove title', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: `Open ${title}`, exact: true })).not.toBeVisible();
  await expect(page.getByText('3 titles', { exact: true })).toBeVisible();
});

test('maximum progress cannot be incremented in collections, Home or title details', async ({ page, library }) => {
  const mangaId = 'anilist:30002';
  library.entries.set(mangaId, { ...library.entries.get(mangaId)!, progress: 2147483647 });
  await page.goto('/');
  await page.getByRole('button', { name: 'Manga', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Add chapter to Berserk', exact: true })).toBeDisabled();
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Home', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Add chapter to Berserk', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Open Berserk', exact: true }).click();
  const increment = page.getByRole('button', { name: '+1 chapter', exact: true });
  const progress = page.getByRole('spinbutton', { name: 'Set progress', exact: true });
  await expect(increment).toBeDisabled();
  await progress.fill('2147483646');
  await page.getByRole('button', { name: 'Set progress', exact: true }).click();
  await expect(increment).toBeEnabled();
  await increment.click();
  await expect(progress).toHaveValue('2147483647');
  await expect(increment).toBeDisabled();
  await expect(progress).toBeFocused();
  expect(library.entries.get(mangaId)?.progress).toBe(2147483647);
});

test('a direct manga route uses library metadata when catalog refresh fails and keeps tracking usable', async ({ page, library }) => {
  library.failNext('media.detail', 'Catalog details are unavailable.');
  await page.goto('/#title/anilist%3A30002');
  await expect(page.getByText('Could not refresh catalog details. Showing available information.', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Berserk', level: 1, exact: true })).toBeVisible();
  await expect(page.getByText('Chapters read', { exact: true })).toBeVisible();
  const status = page.getByRole('combobox', { name: 'Library status', exact: true });
  await expect(status).toBeEnabled();
  await expect(page.getByRole('button', { name: '+1 chapter', exact: true })).toBeEnabled();
  await status.selectOption('Paused');
  await expect(status).toHaveValue('Paused');
  expect(library.entries.get('anilist:30002')?.status).toBe('Paused');
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByText('Could not refresh catalog details. Showing available information.', { exact: true })).not.toBeVisible();
  await expect(status).toHaveValue('Paused');
});

test('the last Home episode restores keyboard focus to its title opener', async ({ page, library }) => {
  library.entries.set(id, { ...library.entries.get(id)!, progress: 27 });
  await page.goto('/#home');
  const card = page.getByRole('region', { name: 'Continue tracking', exact: true }).getByRole('article', { name: title, exact: true });
  const progress = card.getByRole('button', { name: `Add episode to ${title}`, exact: true });
  await progress.focus();
  await progress.press('Enter');
  await expect(card).toContainText('EP 28');
  await expect(progress).toBeDisabled();
  await expect(card.getByRole('button', { name: `Open ${title}`, exact: true })).toBeFocused();
});

test('a Home save finishing after navigation does not take focus from the chosen page', async ({ page, library }) => {
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  library.override = async request => { if (request.method === 'library.progress') await pending; return undefined; };
  await page.goto('/#home');
  const progress = page.getByRole('button', { name: `Add episode to ${title}`, exact: true });
  await progress.click();
  await expect(progress).toBeDisabled();
  const settings = page.getByRole('navigation', { name: 'Main navigation', exact: true }).getByRole('button', { name: 'Settings', exact: true });
  await settings.click();
  const response = page.waitForResponse(result => result.url().endsWith('/api/rpc') && result.request().postDataJSON().method === 'library.progress');
  release();
  await (await response).finished();
  await expect(page.getByRole('heading', { name: 'Settings', level: 1, exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Settings', level: 1, exact: true })).toBeFocused();
});

test('Home progress failure and retry keep keyboard focus on the usable progress action', async ({ page, library }) => {
  library.failNext('library.progress', 'Progress could not save.');
  await page.goto('/#home');
  const card = page.getByRole('region', { name: 'Continue tracking', exact: true }).getByRole('article', { name: title, exact: true });
  const progress = card.getByRole('button', { name: `Add episode to ${title}`, exact: true });
  await progress.focus();
  await progress.press('Enter');
  await expect(page.getByRole('alert')).toHaveText('Progress could not save.');
  await expect(progress).toBeFocused();
  expect(library.entries.get(id)?.progress).toBe(3);
  await progress.press('Enter');
  await expect(card).toContainText('EP 4');
  await expect(progress).toBeFocused();
});

test('Back restores the opener even when refreshed metadata changes the title caption', async ({ page, library }) => {
  const updatedTitle = 'Frieren — refreshed provider title';
  library.override = async request => {
    if (request.method !== 'media.detail' || request.params.mediaId !== id) return undefined;
    const refreshed = { ...library.catalog.get(id)!, title: updatedTitle };
    library.catalog.set(id, refreshed);
    library.entries.set(id, { ...library.entries.get(id)!, media: refreshed });
    return refreshed;
  };
  await page.goto('/');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await expect(page.getByRole('heading', { name: updatedTitle, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByRole('button', { name: `Open ${updatedTitle}`, exact: true })).toBeFocused();
});

test('Home media filter and title focus survive a details round trip', async ({ page, library }) => {
  await page.goto('/#home');
  const manga = page.getByRole('button', { name: 'Manga', exact: true });
  await manga.click();
  const open = page.getByRole('button', { name: 'Open Berserk', exact: true });
  await open.click();
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(manga).toHaveAttribute('aria-pressed', 'true');
  await expect(open).toBeFocused();
  await expect(page.getByText('1 title tracked', { exact: true })).toBeVisible();
});

test('Schedule range, timezone and library filter survive a details round trip', async ({ page, library }) => {
  await page.goto('/#schedule');
  await page.getByRole('button', { name: 'Tomorrow', exact: true }).click();
  await page.getByRole('combobox', { name: 'Schedule timezone', exact: true }).selectOption('Asia/Tokyo');
  await page.getByLabel('My library only', { exact: true }).check();
  await page.getByRole('button', { name: `Open ${title}, episode 4`, exact: true }).click();
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Tomorrow', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('combobox', { name: 'Schedule timezone', exact: true })).toHaveValue('Asia/Tokyo');
  await expect(page.getByLabel('My library only', { exact: true })).toBeChecked();
});

test('recognition preview and matches survive opening a title without a second upload', async ({ page, library }) => {
  page.on('dialog', dialog => void dialog.accept());
  await page.goto('/#recognize');
  const png = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=';
  await page.getByLabel('Choose recognition image').setInputFiles({ name: 'context.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') });
  await page.getByRole('button', { name: 'Find matches with trace.moe', exact: true }).click();
  const results = page.getByRole('region', { name: 'Recognition results', exact: true });
  await results.getByRole('button', { name: 'Open title', exact: true }).click();
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByRole('img', { name: 'Selected source for recognition', exact: true })).toBeVisible();
  await expect(results.getByRole('article', { name: `Match ${title}`, exact: true })).toBeVisible();
  expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(1);
});

for (const origin of ['library', 'home']) {
  test(`removing an opened title returns focus to the ${origin} page instead of a stale card`, async ({ page, library }) => {
    await page.goto(`/#${origin}`);
    const open = origin === 'home'
      ? page.getByRole('region', { name: 'Continue tracking', exact: true }).getByRole('button', { name: `Open ${title}`, exact: true })
      : page.getByRole('button', { name: `Open ${title}`, exact: true });
    await open.click();
    await page.getByRole('button', { name: 'Remove from library', exact: true }).click();
    await page.getByRole('button', { name: 'Remove title', exact: true }).click();
    if (origin === 'library') await expect(page.getByRole('searchbox', { name: 'Search your library', exact: true })).toBeFocused();
    else await expect(page.getByRole('heading', { name: 'Home', level: 1, exact: true })).toBeFocused();
    const tracked = origin === 'home' ? page.getByRole('region', { name: 'Continue tracking', exact: true }) : page;
    await expect(tracked.getByRole('button', { name: `Open ${title}`, exact: true })).not.toBeVisible();
    expect(library.entries.has(id)).toBe(false);
  });
}

test.describe('buttons activated without automatic pointer focus', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      // WebKit/native accessibility activation does not necessarily focus a button.
      document.addEventListener('mousedown', event => {
        if (event.target instanceof Element && event.target.closest('button')) event.preventDefault();
      }, true);
    });
  });

  test('rating dismissal restores the initiating button', async ({ page, library }) => {
    await page.goto('/');
    const rate = page.getByRole('button', { name: `Rate ${title}`, exact: true });
    await rate.click();
    await page.getByRole('dialog', { name: 'Your rating', exact: true }).getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(rate).toBeFocused();
  });

  test('Back restores the initiating title button', async ({ page, library }) => {
    await page.goto('/');
    const open = page.getByRole('button', { name: `Open ${title}`, exact: true });
    await open.click();
    await page.getByRole('button', { name: 'Back', exact: true }).click();
    await expect(open).toBeFocused();
  });
});
