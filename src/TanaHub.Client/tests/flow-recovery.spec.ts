import type { RecognitionHistoryItem } from '../src/lib/feature-types';
import { test, expect, type MockLibrary } from './fixtures';

const frieren = "Frieren: Beyond Journey's End";
const image = { name: 'Recognition.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') };
const xml = { name: 'my-list.xml', mimeType: 'application/xml', buffer: Buffer.from('<myanimelist></myanimelist>') };

function historyItem(id: string, title: string): RecognitionHistoryItem {
  return { id, title, mediaId: null, provider: 'SauceNAO', sourceName: 'Saved image.png', createdAt: '2026-10-25T18:00:00Z', romajiTitle: null, nativeTitle: null, episode: null, similarity: .96, thumbnailUrl: null };
}

/** Capture the server snapshot before allowing a concurrent write to complete. */
function holdHistoryRefresh(library: MockLibrary) {
  let hold = false;
  let started = false;
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  library.override = async ({ method }) => {
    if (method !== 'recognition.history' || !hold) return undefined;
    const items = structuredClone([...library.recognitionHistory.values()].reverse());
    started = true;
    await gate;
    return { items };
  };
  return { start() { hold = true; }, get started() { return started; }, release() { hold = false; release(); } };
}

test('a delayed history refresh keeps a successful deletion and still includes newly fetched rows', async ({ page, library }) => {
  library.recognitionHistory.set('existing', historyItem('existing', 'Old match'));
  const refresh = holdHistoryRefresh(library);
  try {
    await page.goto('/#recognize');
    const old = page.getByRole('article', { name: 'Recognition history Old match', exact: true });
    await expect(old).toBeVisible();
    // This row arrives from the source after the page's first history response.
    library.recognitionHistory.set('new-source-row', historyItem('new-source-row', 'New source match'));
    refresh.start();
    const refreshButton = page.getByRole('button', { name: 'Refresh recognition history', exact: true });
    await refreshButton.click();
    await expect.poll(() => refresh.started).toBe(true);
    await old.getByRole('button', { name: 'Remove recognition Old match', exact: true }).click();
    await expect(old).toHaveCount(0);
    expect(library.recognitionHistory.has('existing')).toBe(false);
    refresh.release();
    await expect(refreshButton).toBeEnabled();
    await expect(old).toHaveCount(0);
    await expect(page.getByRole('article', { name: 'Recognition history New source match', exact: true })).toBeVisible();
  } finally { refresh.release(); }
});

test('a delayed history refresh keeps a successful save and still includes newly fetched rows', async ({ page, library }) => {
  const refresh = holdHistoryRefresh(library);
  try {
    await page.goto('/#recognize');
    await page.getByRole('button', { name: /Artwork & manga/ }).click();
    await page.getByLabel('Choose recognition image', { exact: true }).setInputFiles(image);
    await page.getByRole('button', { name: 'Find matches with SauceNAO', exact: true }).click();
    const match = page.getByRole('article', { name: `Match ${frieren}`, exact: true });
    await expect(match).toBeVisible();
    const refreshButton = page.getByRole('button', { name: 'Refresh recognition history', exact: true });
    await expect(refreshButton).toBeEnabled();
    library.recognitionHistory.set('new-source-row', historyItem('new-source-row', 'New source match'));
    refresh.start();
    await refreshButton.click();
    await expect.poll(() => refresh.started).toBe(true);
    await match.getByRole('button', { name: `Save ${frieren} to recognition history`, exact: true }).click();
    const saved = page.getByRole('article', { name: `Recognition history ${frieren}`, exact: true });
    await expect(saved).toBeVisible();
    refresh.release();
    await expect(refreshButton).toBeEnabled();
    await expect(saved).toBeVisible();
    await expect(page.getByRole('article', { name: 'Recognition history New source match', exact: true })).toBeVisible();
  } finally { refresh.release(); }
});

test('a failed history deletion during refresh keeps the row and allows retry', async ({ page, library }) => {
  library.recognitionHistory.set('existing', historyItem('existing', 'Old match'));
  const refresh = holdHistoryRefresh(library);
  try {
    await page.goto('/#recognize');
    const old = page.getByRole('article', { name: 'Recognition history Old match', exact: true });
    await expect(old).toBeVisible();
    refresh.start();
    const refreshButton = page.getByRole('button', { name: 'Refresh recognition history', exact: true });
    await refreshButton.click();
    await expect.poll(() => refresh.started).toBe(true);
    library.failNext('recognition.remove', 'The saved match could not be removed.');
    await old.getByRole('button', { name: 'Remove recognition Old match', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('The saved match could not be removed.');
    await expect(old).toBeVisible();
    refresh.release();
    await expect(refreshButton).toBeEnabled();
    await expect(old).toBeVisible();
    await old.getByRole('button', { name: 'Remove recognition Old match', exact: true }).click();
    await expect(old).toHaveCount(0);
    expect(library.recognitionHistory.has('existing')).toBe(false);
  } finally { refresh.release(); }
});

test('a failed match save during refresh stays unsaved and allows retry', async ({ page, library }) => {
  const refresh = holdHistoryRefresh(library);
  try {
    await page.goto('/#recognize');
    await page.getByRole('button', { name: /Artwork & manga/ }).click();
    await page.getByLabel('Choose recognition image', { exact: true }).setInputFiles(image);
    await page.getByRole('button', { name: 'Find matches with SauceNAO', exact: true }).click();
    const match = page.getByRole('article', { name: `Match ${frieren}`, exact: true });
    await expect(match).toBeVisible();
    const refreshButton = page.getByRole('button', { name: 'Refresh recognition history', exact: true });
    await expect(refreshButton).toBeEnabled();
    refresh.start();
    await refreshButton.click();
    await expect.poll(() => refresh.started).toBe(true);
    library.failNext('recognition.save', 'This match could not be saved.');
    const save = match.getByRole('button', { name: `Save ${frieren} to recognition history`, exact: true });
    await save.click();
    await expect(page.getByRole('alert')).toContainText('This match could not be saved.');
    await expect(save).toBeEnabled();
    refresh.release();
    await expect(refreshButton).toBeEnabled();
    await expect(page.getByRole('article', { name: `Recognition history ${frieren}`, exact: true })).toHaveCount(0);
    await save.click();
    await expect(match.getByRole('button', { name: `${frieren} saved to history`, exact: true })).toBeDisabled();
    await expect(page.getByRole('article', { name: `Recognition history ${frieren}`, exact: true })).toBeVisible();
  } finally { refresh.release(); }
});

test('cancelling an XML preview returns keyboard focus to the import file control', async ({ page, library }) => {
  await page.goto('/#settings');
  const file = page.getByLabel('Import MAL XML', { exact: true });
  await file.setInputFiles(xml);
  await expect(page.getByText('1 title in my-list.xml', { exact: true })).toBeVisible();
  const cancel = page.getByRole('button', { name: 'Cancel', exact: true });
  await cancel.focus(); await page.keyboard.press('Enter');
  await expect(cancel).toHaveCount(0);
  await expect(file).toBeFocused();
  expect(library.requests.some(request => request.method === 'library.import.apply')).toBe(false);
});

test('a successful XML import returns keyboard focus to the import file control', async ({ page, library }) => {
  await page.goto('/#settings');
  const file = page.getByLabel('Import MAL XML', { exact: true });
  await file.setInputFiles(xml);
  const apply = page.getByRole('button', { name: 'Import 1 title', exact: true });
  await apply.focus(); await page.keyboard.press('Enter');
  await expect(page.getByRole('status').filter({ hasText: 'Imported 1 title' })).toBeVisible();
  await expect(apply).toHaveCount(0);
  await expect(file).toBeFocused();
  expect(library.requests.some(request => request.method === 'library.import.apply')).toBe(true);
});

test('an XML import failure keeps the confirmation focused and retry returns to the file control', async ({ page, library }) => {
  await page.goto('/#settings');
  const file = page.getByLabel('Import MAL XML', { exact: true });
  await file.setInputFiles(xml);
  const apply = page.getByRole('button', { name: 'Import 1 title', exact: true });
  library.failNext('library.import.apply', 'Could not import this list. Your library is unchanged.');
  await apply.focus(); await page.keyboard.press('Enter');
  await expect(page.getByRole('alert')).toContainText('Could not import this list.');
  await expect(page.getByText('1 title in my-list.xml', { exact: true })).toBeVisible();
  await expect(apply).toBeEnabled(); await expect(apply).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('status').filter({ hasText: 'Imported 1 title' })).toBeVisible();
  await expect(file).toBeFocused();
});
