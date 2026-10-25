import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';

const frieren = "Frieren: Beyond Journey's End";
const tinyPng = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=';

async function recognizeOriginal(page: Page) {
  await page.goto('/#recognize');
  await page.getByLabel('Choose recognition image', { exact: true }).setInputFiles({ name: 'Original.png', mimeType: 'image/png', buffer: Buffer.from(tinyPng, 'base64') });
  await page.getByRole('button', { name: 'Find matches with trace.moe', exact: true }).click();
  await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
}

async function openMatch(page: Page) {
  await page.getByRole('article', { name: `Match ${frieren}`, exact: true }).getByRole('button', { name: 'Open title', exact: true }).click();
  await expect(page.getByRole('heading', { name: frieren, exact: true, level: 1 })).toBeVisible();
}

async function pasteImage(page: Page, name: string) {
  return page.evaluate(({ bytes, name }) => {
    const clipboard = new DataTransfer();
    clipboard.items.add(new File([Uint8Array.from(atob(bytes), character => character.charCodeAt(0))], name, { type: 'image/png' }));
    const event = new ClipboardEvent('paste', { clipboardData: clipboard, bubbles: true, cancelable: true });
    (document.activeElement ?? document.body).dispatchEvent(event);
    return event.defaultPrevented;
  }, { bytes: tinyPng, name });
}

async function pasteText(page: Page, text: string) {
  return page.evaluate(text => {
    const clipboard = new DataTransfer();
    clipboard.setData('text/plain', text);
    const event = new ClipboardEvent('paste', { clipboardData: clipboard, bubbles: true, cancelable: true });
    (document.activeElement ?? document.body).dispatchEvent(event);
    return { prevented: event.defaultPrevented, text: event.clipboardData?.getData('text/plain') };
  }, text);
}

async function holdFirstClipboardOperation(page: Page, phase: 'read' | 'image', outcome: 'resolve' | 'reject') {
  await page.addInitScript(({ bytes, phase, outcome }) => {
    let reads = 0;
    const image = () => new Blob([Uint8Array.from(atob(bytes), character => character.charCodeAt(0))], { type: 'image/png' });
    const pending = <T,>(value: T) => new Promise<T>((resolve, reject) => {
      (window as unknown as { finishClipboardOperation: () => void }).finishClipboardOperation = () => {
        (window as unknown as { clipboardOperationCompleted: boolean }).clipboardOperationCompleted = true;
        if (outcome === 'reject') reject(new Error('An outdated clipboard request failed.'));
        else resolve(value);
      };
    });
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {
      read: () => {
        const first = reads++ === 0;
        const items = [{ types: ['image/png'], getType: () => first && phase === 'image' ? pending(image()) : Promise.resolve(image()) }];
        return first && phase === 'read' ? pending(items) : Promise.resolve(items);
      },
    } });
  }, { bytes: tinyPng, phase, outcome });
}

async function startHeldClipboardPaste(page: Page) {
  await page.getByRole('button', { name: 'Paste image', exact: true }).click();
  await expect.poll(() => page.evaluate(() => typeof (window as unknown as { finishClipboardOperation?: () => void }).finishClipboardOperation)).toBe('function');
}

async function finishHeldClipboardPaste(page: Page) {
  await page.evaluate(() => (window as unknown as { finishClipboardOperation: () => void }).finishClipboardOperation());
  await expect.poll(() => page.evaluate(() => (window as unknown as { clipboardOperationCompleted?: boolean }).clipboardOperationCompleted)).toBe(true);
}

test('pasting an image on title details does not replace the preserved recognition source or results', async ({ page, library }) => {
  await recognizeOriginal(page);
  await openMatch(page);
  await page.getByRole('heading', { name: frieren, exact: true, level: 1 }).focus();
  const prevented = await pasteImage(page, 'While viewing title.png');
  expect.soft(prevented, 'A hidden recognition page must not consume the active page paste').toBe(false);
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByText('Original.png', { exact: true })).toBeVisible();
  await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
  expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(1);
});

test('image paste works again after returning to the visible recognition page', async ({ page, library }) => {
  await recognizeOriginal(page);
  await openMatch(page);
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
  expect(await pasteImage(page, 'Replacement.png')).toBe(true);
  await expect(page.getByText('Replacement.png', { exact: true })).toBeVisible();
  await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toHaveCount(0);
  expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(1);
  await page.getByRole('button', { name: 'Find matches with trace.moe', exact: true }).click();
  await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
  const uploads = library.requests.filter(request => request.method === 'recognition.run');
  expect(uploads).toHaveLength(2);
  expect(uploads[1].params.sourceName).toBe('Replacement.png');
});

test('ordinary text paste on Recognize keeps the current image and matches', async ({ page, library }) => {
  await recognizeOriginal(page);
  const text = 'A copied title, not another image.';
  const paste = await pasteText(page, text);
  expect(paste).toEqual({ prevented: false, text });
  await expect(page.getByText('Original.png', { exact: true })).toBeVisible();
  await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
  expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(1);
});

test('text paste remains available to a title notes editor without changing the recognition session', async ({ page, library }) => {
  await recognizeOriginal(page);
  await openMatch(page);
  await page.getByRole('button', { name: 'Add notes', exact: true }).click();
  const notes = page.getByRole('textbox', { name: 'Personal notes', exact: true });
  await notes.fill('An unsaved note.');
  const text = 'Plain clipboard text for the editor.';
  expect(await pasteText(page, text)).toEqual({ prevented: false, text });
  await expect(notes).toBeFocused();
  // Synthetic paste does not insert text; the editor must retain its own draft.
  await expect(notes).toHaveValue('An unsaved note.');
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByText('Original.png', { exact: true })).toBeVisible();
  await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
  expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(1);
});

test('a pending Paste image read cannot change a recognition session after opening a title', async ({ page, library }) => {
  await page.addInitScript(bytes => {
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {
      read: () => new Promise(resolve => {
        (window as unknown as { finishClipboardRead: () => void }).finishClipboardRead = () => resolve([{
          types: ['image/png'],
          getType: async () => new Blob([Uint8Array.from(atob(bytes), character => character.charCodeAt(0))], { type: 'image/png' }),
        }]);
      }).then(items => { (window as unknown as { clipboardReadCompleted: boolean }).clipboardReadCompleted = true; return items; }),
    } });
  }, tinyPng);
  await recognizeOriginal(page);
  await page.getByRole('button', { name: 'Paste image', exact: true }).click();
  await expect.poll(() => page.evaluate(() => typeof (window as unknown as { finishClipboardRead?: () => void }).finishClipboardRead)).toBe('function');
  await openMatch(page);
  await page.evaluate(() => (window as unknown as { finishClipboardRead: () => void }).finishClipboardRead());
  await expect.poll(() => page.evaluate(() => (window as unknown as { clipboardReadCompleted?: boolean }).clipboardReadCompleted)).toBe(true);
  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await expect(page.getByText('Original.png', { exact: true })).toBeVisible();
  await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
  expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(1);
});

for (const phase of ['read', 'image'] as const) {
  for (const outcome of ['resolve', 'reject'] as const) {
    test(`a stale clipboard ${phase} ${outcome} after Back keeps the session and allows a new explicit paste`, async ({ page, library }) => {
      await holdFirstClipboardOperation(page, phase, outcome);
      await recognizeOriginal(page);
      await startHeldClipboardPaste(page);
      await openMatch(page);
      await page.getByRole('button', { name: 'Back', exact: true }).click();
      await expect(page.getByText('Original.png', { exact: true })).toBeVisible();
      await finishHeldClipboardPaste(page);
      await expect(page.getByText('Original.png', { exact: true })).toBeVisible();
      await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
      await expect(page.getByRole('alert')).toHaveCount(0);
      expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(1);

      await page.getByRole('button', { name: 'Paste image', exact: true }).click();
      await expect(page.getByText('Clipboard image.png', { exact: true })).toBeVisible();
      await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toHaveCount(0);
      await page.getByRole('button', { name: 'Find matches with trace.moe', exact: true }).click();
      await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
      const uploads = library.requests.filter(request => request.method === 'recognition.run');
      expect(uploads).toHaveLength(2);
      expect(uploads[1].params.sourceName).toBe('Clipboard image.png');
    });
  }
}

const newerSourceCases = [
  { action: 'file choice', phase: 'read', outcome: 'resolve' },
  { action: 'file choice', phase: 'read', outcome: 'reject' },
  { action: 'file choice', phase: 'image', outcome: 'resolve' },
  { action: 'file choice', phase: 'image', outcome: 'reject' },
  { action: 'keyboard image paste', phase: 'image', outcome: 'resolve' },
  { action: 'keyboard image paste', phase: 'read', outcome: 'reject' },
  { action: 'Clear image', phase: 'read', outcome: 'resolve' },
  { action: 'Clear image', phase: 'image', outcome: 'reject' },
] as const;

for (const { action, phase, outcome } of newerSourceCases) {
  test(`a newer ${action} survives a stale clipboard ${phase} ${outcome}`, async ({ page, library }) => {
    await holdFirstClipboardOperation(page, phase, outcome);
    await recognizeOriginal(page);
    await startHeldClipboardPaste(page);
    const findMatches = page.getByRole('button', { name: 'Find matches with trace.moe', exact: true });
    let currentSource = 'Newer.png';
    if (action === 'file choice') {
      await page.getByLabel('Choose recognition image', { exact: true }).setInputFiles({ name: currentSource, mimeType: 'image/png', buffer: Buffer.from(tinyPng, 'base64') });
    } else if (action === 'keyboard image paste') {
      expect(await pasteImage(page, currentSource)).toBe(true);
    } else {
      await page.getByRole('button', { name: 'Clear recognition image', exact: true }).click();
    }
    if (action !== 'Clear image') await expect(page.getByText(currentSource, { exact: true })).toBeVisible();
    await finishHeldClipboardPaste(page);
    if (action === 'Clear image') {
      await expect(page.getByRole('heading', { name: 'Choose or drop an image', exact: true })).toBeVisible();
      await expect(findMatches).toBeDisabled();
    } else {
      await expect(page.getByText(currentSource, { exact: true })).toBeVisible();
      await expect(findMatches).toBeEnabled();
    }
    await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toHaveCount(0);
    await expect(page.getByRole('alert')).toHaveCount(0);
    expect(library.requests.filter(request => request.method === 'recognition.run')).toHaveLength(1);

    if (action === 'Clear image') {
      await page.getByRole('button', { name: 'Paste image', exact: true }).click();
      currentSource = 'Clipboard image.png';
      await expect(page.getByText(currentSource, { exact: true })).toBeVisible();
    }
    await findMatches.click();
    await expect(page.getByRole('article', { name: `Match ${frieren}`, exact: true })).toBeVisible();
    const uploads = library.requests.filter(request => request.method === 'recognition.run');
    expect(uploads).toHaveLength(2);
    expect(uploads[1].params.sourceName).toBe(currentSource);
  });
}
