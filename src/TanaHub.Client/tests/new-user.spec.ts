import { test, expect } from './fixtures';

const title = "Frieren: Beyond Journey's End";
const openTitle = (page: import('@playwright/test').Page) => page.getByRole('button', { name: `Open ${title}`, exact: true }).click();

test('adding from title details moves focus into tracking', async ({ page, library }) => {
  library.entries.clear();
  await page.goto('/#discover'); await openTitle(page);
  const add = page.getByRole('button', { name: 'Add to library', exact: true });
  await add.focus(); await page.keyboard.press('Enter');
  await expect(page.getByRole('spinbutton', { name: 'Set progress', exact: true })).toBeFocused();
});

test('the final catalog episode and a failed save leave a usable keyboard target', async ({ page, library }) => {
  const record = library.entries.get('anilist:154587')!;
  record.progress = 27;
  await page.goto('/#discover');
  const increment = page.getByRole('button', { name: `Add episode to ${title}`, exact: true });
  library.failNext('library.progress');
  await increment.focus(); await page.keyboard.press('Enter');
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(increment).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(increment).toBeDisabled();
  await expect(page.getByRole('button', { name: title, exact: true })).toBeFocused();
});

test('adding and incrementing from a catalog card keeps a usable keyboard target', async ({ page, library }) => {
  library.entries.clear();
  await page.goto('/');
  await page.getByRole('button', { name: 'Discover titles', exact: true }).click();
  const add = page.getByRole('button', { name: `Add ${title} to library`, exact: true });
  await add.focus();
  await page.keyboard.press('Enter');
  const increment = page.getByRole('button', { name: `Add episode to ${title}`, exact: true });
  await expect(increment).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('article', { name: title, exact: true })).toContainText('EP 1');
  await expect(increment).toBeFocused();
});

for (const editor of ['notes', 'review', 'organization', 'dates', 'progress'] as const) {
  test(`${editor} draft survives navigation and reload without being saved`, async ({ page, library }) => {
    await page.setViewportSize({ width: 320, height: 800 });
    await page.goto('/');
    await openTitle(page);
    if (editor === 'notes' || editor === 'review') {
      await page.getByRole('button', { name: `Add ${editor}`, exact: true }).click();
      await page.getByRole('textbox', { name: editor === 'notes' ? 'Personal notes' : 'Review', exact: true }).fill('My unfinished draft');
    } else if (editor === 'organization') {
      await page.getByRole('button', { name: 'Add tags or lists', exact: true }).click();
      await page.getByRole('textbox', { name: 'Tags', exact: true }).fill('Fantasy, Weekend');
      await page.getByRole('textbox', { name: 'Custom lists', exact: true }).fill('Next up');
    } else if (editor === 'dates') {
      await page.getByRole('button', { name: 'Add dates', exact: true }).click();
      await page.getByLabel('Started on', { exact: true }).fill('2026-01-02');
    } else await page.getByRole('spinbutton', { name: 'Set progress', exact: true }).fill('8');
    await page.getByRole('button', { name: 'Back', exact: true }).click();
    // Visiting another title must neither inherit nor overwrite this title's draft.
    await page.getByRole('button', { name: 'Open Berserk', exact: true }).click();
    await expect(page.getByText('Unsaved draft', { exact: true })).not.toBeVisible();
    await page.getByRole('button', { name: 'Back', exact: true }).click();
    await openTitle(page);
    await page.reload();
    if (editor === 'notes' || editor === 'review') await expect(page.getByRole('textbox', { name: editor === 'notes' ? 'Personal notes' : 'Review', exact: true })).toHaveValue('My unfinished draft');
    else if (editor === 'organization') {
      await expect(page.getByRole('textbox', { name: 'Tags', exact: true })).toHaveValue('Fantasy, Weekend');
      await expect(page.getByRole('textbox', { name: 'Custom lists', exact: true })).toHaveValue('Next up');
    } else if (editor === 'dates') await expect(page.getByLabel('Started on', { exact: true })).toHaveValue('2026-01-02');
    else await expect(page.getByRole('spinbutton', { name: 'Set progress', exact: true })).toHaveValue('8');
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
    expect(library.requests.filter(request => ['library.notes', 'library.review', 'library.organization', 'library.dates', 'library.progress.set'].includes(request.method))).toHaveLength(0);
  });
}

test('a discarded or saved draft does not reappear after reopening', async ({ page, library }) => {
  await page.goto('/'); await openTitle(page);
  await page.getByRole('button', { name: 'Add notes', exact: true }).click();
  await page.getByRole('textbox', { name: 'Personal notes', exact: true }).fill('Discard this draft');
  await page.getByRole('button', { name: 'Discard notes draft', exact: true }).click();
  await page.getByRole('button', { name: 'Back', exact: true }).click(); await openTitle(page);
  await expect(page.getByRole('button', { name: 'Add notes', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Add notes', exact: true }).click();
  await page.getByRole('textbox', { name: 'Personal notes', exact: true }).fill('Saved note');
  await page.getByRole('button', { name: 'Save notes', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Save notes', exact: true })).toBeDisabled();
  await page.reload();
  await expect(page.getByText('Saved note', { exact: true })).toBeVisible();
  await expect(page.getByText('Unsaved draft', { exact: true })).not.toBeVisible();
});

test('a draft does not overwrite a newer imported value', async ({ page, library }) => {
  await page.goto('/'); await openTitle(page);
  await page.getByRole('button', { name: 'Add notes', exact: true }).click();
  await page.getByRole('textbox', { name: 'Personal notes', exact: true }).fill('Old draft');
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  const entry = library.entries.get('anilist:154587')!;
  library.entries.set(entry.mediaId, { ...entry, notes: 'New imported note' });
  await page.reload(); await openTitle(page);
  await expect(page.getByText('New imported note', { exact: true })).toBeVisible();
  await expect(page.getByText('Unsaved draft', { exact: true })).not.toBeVisible();
});

test('a storage write failure keeps the latest draft and still allows discarding it', async ({ page, library }) => {
  await page.goto('/'); await openTitle(page);
  await page.getByRole('button', { name: 'Add notes', exact: true }).click();
  await page.getByRole('textbox', { name: 'Personal notes', exact: true }).fill('First version');
  await page.evaluate(() => {
    const setItem = Storage.prototype.setItem;
    const removeItem = Storage.prototype.removeItem;
    Storage.prototype.setItem = function (key, value) {
      if (key.startsWith('tanahub-draft:')) throw new DOMException('Storage full', 'QuotaExceededError');
      return setItem.call(this, key, value);
    };
    Storage.prototype.removeItem = function (key) {
      if (key.startsWith('tanahub-draft:')) throw new DOMException('Storage blocked', 'SecurityError');
      return removeItem.call(this, key);
    };
  });
  await page.getByRole('textbox', { name: 'Personal notes', exact: true }).fill('Latest version');
  await page.getByRole('button', { name: 'Back', exact: true }).click(); await openTitle(page);
  await expect(page.getByRole('textbox', { name: 'Personal notes', exact: true })).toHaveValue('Latest version');
  await page.getByRole('button', { name: 'Discard notes draft', exact: true }).click();
  await page.getByRole('button', { name: 'Back', exact: true }).click(); await openTitle(page);
  await expect(page.getByRole('button', { name: 'Add notes', exact: true })).toBeVisible();
});

test('update checks explain stable releases and always offer access to previews', async ({ page, library }) => {
  library.override = async request => request.method === 'updates.check' ? { currentVersion: '0.1.0', isUpdateAvailable: false, latestRelease: null } : undefined;
  await page.goto('/#settings');
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(page.getByText('No stable release yet. Preview builds are available under Browse releases.', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Browse releases', exact: true })).toBeEnabled();
  await expect(page.getByText('Checks stable releases. For preview builds and installers, browse all releases.', { exact: true })).toBeVisible();
});
