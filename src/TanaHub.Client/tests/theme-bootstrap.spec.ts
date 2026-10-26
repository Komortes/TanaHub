import type { Page } from '@playwright/test';
import { test, expect, type MockLibrary } from './fixtures';

const cacheKey = 'tanahub-theme';
const savedPreference = (page: Page) => page.evaluate(key => localStorage.getItem(key), cacheKey);

function settingsGate(library: MockLibrary) {
  let enabled = false;
  let release!: () => void;
  let entered!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  const pending = new Promise<void>(resolve => { entered = resolve; });
  library.override = async ({ method }) => {
    if (enabled && method === 'settings.get') { entered(); await held; }
    return undefined;
  };
  return { arm: () => { enabled = true; }, pending, release: () => { enabled = false; release(); } };
}

async function countSystemObservers(page: Page) {
  await page.addInitScript(() => {
    const tracker = { active: 0 };
    Object.assign(window, { themeObserverTracker: tracker });
    const nativeMatchMedia = window.matchMedia.bind(window);
    window.matchMedia = query => {
      const media = nativeMatchMedia(query);
      if (query !== '(prefers-color-scheme: dark)') return media;
      const listeners = new Set<EventListenerOrEventListenerObject>();
      const add = media.addEventListener.bind(media);
      const remove = media.removeEventListener.bind(media);
      Object.defineProperties(media, {
        addEventListener: { value(type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) {
          if (type === 'change' && listener && !listeners.has(listener)) { listeners.add(listener); tracker.active += 1; }
          add(type, listener, options);
        } },
        removeEventListener: { value(type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions) {
          if (type === 'change' && listeners.delete(listener)) tracker.active -= 1;
          remove(type, listener, options);
        } },
      });
      return media;
    };
  });
}
const observerCount = (page: Page) => page.evaluate(() => (window as unknown as { themeObserverTracker: { active: number } }).themeObserverTracker.active);

test('a confirmed Light preference is already applied while reload settings are pending', async ({ page, library }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  const gate = settingsGate(library);
  await page.goto('/#settings');
  await page.getByLabel('Theme', { exact: true }).selectOption('Light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  expect(library.settings.theme).toBe('Light');
  gate.arm();
  try {
    await page.reload({ waitUntil: 'domcontentloaded' });
    await gate.pending;
    await expect(page.getByText('Loading settings…', { exact: true })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
    await expect(page.locator('html')).toHaveCSS('color-scheme', 'light');
    await expect(page.locator('html')).toHaveCSS('background-color', 'rgb(244, 243, 237)');
  } finally { gate.release(); }
  await expect(page.getByLabel('Theme', { exact: true })).toHaveValue('Light');
  expect(await savedPreference(page)).toBe('Light');
});

test('cached System follows media changes during loading and removes its single observer when backend takes over', async ({ page, library }) => {
  await countSystemObservers(page);
  await page.emulateMedia({ colorScheme: 'light' });
  const gate = settingsGate(library);
  await page.goto('/#settings');
  await page.getByLabel('Theme', { exact: true }).selectOption('System');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  expect(library.settings.theme).toBe('System');
  gate.arm();
  try {
    await page.reload({ waitUntil: 'domcontentloaded' });
    await gate.pending;
    await expect(page.getByText('Loading settings…', { exact: true })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
    expect(await observerCount(page)).toBe(1);
    expect(await savedPreference(page)).toBe('System');
    await page.emulateMedia({ colorScheme: 'dark' });
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'Nebula dark');
    expect(await savedPreference(page)).toBe('System');
    library.settings.theme = 'High contrast';
  } finally { gate.release(); }
  await expect(page.getByLabel('Theme', { exact: true })).toHaveValue('High contrast');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'High contrast');
  expect(await observerCount(page)).toBe(0);
  expect(await savedPreference(page)).toBe('High contrast');
  await page.emulateMedia({ colorScheme: 'light' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'High contrast');
  expect(library.requests.filter(request => request.method === 'settings.save')).toHaveLength(1);
});

test('a rejected theme save cannot poison the confirmed startup preference', async ({ page, library }) => {
  await page.goto('/#settings');
  const theme = page.getByLabel('Theme', { exact: true });
  await theme.selectOption('Light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  expect(await savedPreference(page)).toBe('Light');
  library.failNext('settings.save', 'Theme could not be saved.');
  await theme.selectOption('High contrast');
  await expect(page.getByRole('alert')).toContainText('Theme could not be saved.');
  await expect(theme).toHaveValue('Light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  expect(await savedPreference(page)).toBe('Light');
  expect(library.settings.theme).toBe('Light');
});

test('an invalid startup preference is ignored and replaced only by authoritative settings', async ({ page, library }) => {
  await countSystemObservers(page);
  await page.addInitScript(key => localStorage.setItem(key, 'unsupported-theme'), cacheKey);
  const gate = settingsGate(library);
  gate.arm();
  try {
    await page.goto('/#settings');
    await gate.pending;
    await expect(page.getByText('Loading settings…', { exact: true })).toBeVisible();
    await expect(page.locator('html')).toHaveCSS('color-scheme', 'dark');
    await expect(page.locator('html')).not.toHaveAttribute('data-theme', 'unsupported-theme');
    expect(await observerCount(page)).toBe(0);
    expect(library.requests.filter(request => request.method === 'settings.save')).toHaveLength(0);
  } finally { gate.release(); }
  await expect(page.getByLabel('Theme', { exact: true })).toHaveValue('Nebula dark');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Nebula dark');
  expect(await savedPreference(page)).toBe('Nebula dark');
});

test('denied browser storage leaves startup usable and backend settings authoritative', async ({ page, library }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => {
    Object.defineProperty(window, 'localStorage', { configurable: true, get() { throw new DOMException('Storage is unavailable.', 'SecurityError'); } });
  });
  library.settings.theme = 'Light';
  const gate = settingsGate(library);
  gate.arm();
  try {
    await page.goto('/#settings');
    await gate.pending;
    await expect(page.getByRole('heading', { name: 'Settings', level: 1, exact: true })).toBeVisible();
    await expect(page.getByText('Loading settings…', { exact: true })).toBeVisible();
    await expect(page.locator('html')).toHaveCSS('color-scheme', 'dark');
  } finally { gate.release(); }
  await expect(page.getByLabel('Theme', { exact: true })).toHaveValue('Light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  await expect(page.locator('html')).toHaveCSS('color-scheme', 'light');
  expect(errors).toEqual([]);
  expect(library.requests.filter(request => request.method === 'settings.save')).toHaveLength(0);
});
