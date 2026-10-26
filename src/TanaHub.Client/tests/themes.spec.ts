import { test, expect } from './fixtures';

test('System resolves the OS preference and follows changes without saving settings', async ({ page, library }) => {
  library.settings.theme = 'System';
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/#settings');
  await expect(page.getByLabel('Theme', { exact: true })).toHaveValue('System');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  await expect(page.locator('html')).toHaveCSS('color-scheme', 'light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Nebula dark');
  await expect(page.locator('html')).toHaveCSS('color-scheme', 'dark');
  await expect(page.getByLabel('Theme', { exact: true })).toHaveValue('System');
  expect(library.requests.filter(request => request.method === 'settings.save')).toHaveLength(0);
});

for (const preference of ['Light', 'Nebula dark', 'High contrast']) {
  test(`explicit ${preference} ignores OS preference changes`, async ({ page, library }) => {
    library.settings.theme = preference;
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/#settings');
    await expect(page.locator('html')).toHaveAttribute('data-theme', preference);
    await expect(page.locator('html')).toHaveCSS('color-scheme', preference === 'Light' ? 'light' : 'dark');
    await page.emulateMedia({ colorScheme: 'light' });
    await page.emulateMedia({ colorScheme: 'dark' });
    await expect(page.locator('html')).toHaveAttribute('data-theme', preference);
    expect(library.requests.filter(request => request.method === 'settings.save')).toHaveLength(0);
  });
}

test('a saved Light choice survives reload and System remains the saved preference', async ({ page, library }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/#settings');
  const theme = page.getByLabel('Theme', { exact: true });
  await expect(theme.locator('option').filter({ hasText: /^Light$/ })).toHaveCount(1);
  await theme.selectOption('Light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  await page.reload();
  await expect(page.getByLabel('Theme', { exact: true })).toHaveValue('Light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  await page.getByLabel('Theme', { exact: true }).selectOption('System');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Nebula dark');
  expect(library.settings.theme).toBe('System');
  await page.reload();
  await expect(page.getByLabel('Theme', { exact: true })).toHaveValue('System');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Nebula dark');
});

test('a rejected theme save restores the previous select and palette before retry', async ({ page, library }) => {
  library.settings.theme = 'High contrast';
  await page.emulateMedia({ colorScheme: 'light' });
  library.failNext('settings.save', 'Theme could not be saved.');
  await page.goto('/#settings');
  const theme = page.getByLabel('Theme', { exact: true });
  await expect(theme.locator('option').filter({ hasText: /^System$/ })).toHaveCount(1);
  await theme.selectOption('System');
  await expect(page.getByRole('alert')).toContainText('Theme could not be saved.');
  await expect(theme).toHaveValue('High contrast');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'High contrast');
  await theme.selectOption('System');
  await expect(theme).toHaveValue('System');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
});

test('leaving System removes its observer so OS changes cannot override an explicit theme', async ({ page, library }) => {
  library.settings.theme = 'System';
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/#settings');
  await expect(page.getByLabel('Theme', { exact: true }).locator('option').filter({ hasText: /^System$/ })).toHaveCount(1);
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Nebula dark');
  await page.getByLabel('Theme', { exact: true }).selectOption('High contrast');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'High contrast');
  await page.emulateMedia({ colorScheme: 'light' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'High contrast');
  expect(library.settings.theme).toBe('High contrast');
});

for (const width of [320, 1440]) {
  test(`Light first-use actions retain contrast and fit at ${width}px`, async ({ page, library }) => {
    library.settings.theme = 'Light'; library.entries.clear();
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/#library');
    await expect(page.locator('html')).toHaveCSS('color-scheme', 'light');
    const discover = page.getByRole('button', { name: 'Discover titles', exact: true });
    await expect(discover).toBeVisible();
    const contrast = await discover.evaluate(node => {
      const css = getComputedStyle(node);
      const luminance = (color: string) => {
        const channels = color.match(/[\d.]+/g)!.slice(0, 3).map(Number).map(n => n / 255).map(n => n <= .04045 ? n / 12.92 : ((n + .055) / 1.055) ** 2.4);
        return channels[0] * .2126 + channels[1] * .7152 + channels[2] * .0722;
      };
      const foreground = luminance(css.color), background = luminance(css.backgroundColor);
      return (Math.max(foreground, background) + .05) / (Math.min(foreground, background) + .05);
    });
    expect(contrast).toBeGreaterThanOrEqual(4.5);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
    await discover.click();
    await expect(page.getByRole('heading', { name: 'Discover', exact: true, level: 1 })).toBeVisible();
    await expect(page.getByRole('article').first()).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
  });
}
