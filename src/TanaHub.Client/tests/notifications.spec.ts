import { test, expect } from './fixtures';

const unsupported = 'System reminders are not supported on Windows in this build.';
const supportCalls = (requests: Array<{ method: string }>) => requests.filter(request => request.method === 'notifications.support');

test('unsupported reminders explain the limitation without clearing the saved preference', async ({ page, library }) => {
  library.settings.notificationsEnabled = true;
  library.override = async ({ method }) => method === 'notifications.support' ? { supported: false, reason: unsupported } : undefined;
  await page.goto('/#settings');
  const notifications = page.getByRole('switch', { name: 'Notifications', exact: true });
  await expect(notifications).toBeDisabled();
  await expect(notifications).toBeChecked();
  await expect(page.getByText(unsupported, { exact: true })).toBeVisible();
  await page.getByLabel('Theme', { exact: true }).selectOption('Light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  expect(library.settings.notificationsEnabled).toBe(true);
  expect(library.requests.filter(request => request.method === 'settings.save').some(request => 'notificationsEnabled' in request.params)).toBe(false);
  expect(supportCalls(library.requests)).toHaveLength(1);
});

test('supported reminders remain editable and capability is fetched once per Settings mount', async ({ page, library }) => {
  library.settings.notificationsEnabled = false;
  library.override = async ({ method }) => method === 'notifications.support' ? { supported: true, reason: null } : undefined;
  await page.goto('/#settings');
  const notifications = page.getByRole('switch', { name: 'Notifications', exact: true });
  await expect(notifications).toBeEnabled();
  await notifications.check();
  await expect.poll(() => library.settings.notificationsEnabled).toBe(true);
  await page.getByLabel('Theme', { exact: true }).selectOption('High contrast');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'High contrast');
  expect(supportCalls(library.requests)).toHaveLength(1);
});

test('a failed reminder capability check stays safe and offers a focused retry', async ({ page, library }) => {
  library.failNext('notifications.support', 'Test capability service is temporarily unavailable.');
  library.override = async ({ method }) => method === 'notifications.support' ? { supported: true, reason: null } : undefined;
  await page.goto('/#settings');
  const notifications = page.getByRole('switch', { name: 'Notifications', exact: true });
  await expect(page.getByText('Could not check reminder availability.', { exact: true })).toBeVisible();
  await expect(notifications).toBeDisabled();
  await page.getByRole('button', { name: 'Retry reminder check', exact: true }).click();
  await expect(notifications).toBeEnabled();
  await expect(page.getByRole('heading', { name: 'Appearance & preferences', exact: true })).toBeFocused();
  await expect(page.getByRole('button', { name: 'Retry reminder check', exact: true })).toHaveCount(0);
});

test('an old capability response cannot replace the current Settings instance or take focus', async ({ page, library }) => {
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  let calls = 0;
  library.override = async ({ method }) => {
    if (method !== 'notifications.support') return undefined;
    calls += 1;
    if (calls === 1) { await gate; return { supported: false, reason: unsupported }; }
    return { supported: true, reason: null };
  };
  await page.goto('/#settings');
  await expect.poll(() => calls).toBe(1);
  const nav = page.getByRole('navigation', { name: 'Main navigation' });
  await nav.getByRole('button', { name: 'Home', exact: true }).click();
  await nav.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByRole('switch', { name: 'Notifications', exact: true })).toBeEnabled();
  const theme = page.getByLabel('Theme', { exact: true }); await theme.focus();
  release();
  await expect.poll(() => library.requests.filter(request => request.method === 'notifications.support').length).toBe(2);
  await expect(page.getByRole('switch', { name: 'Notifications', exact: true })).toBeEnabled();
  await expect(page.getByText(unsupported, { exact: true })).toHaveCount(0);
  await expect(theme).toBeFocused();
});

test('pending reminder support does not block an unrelated appearance save', async ({ page, library }) => {
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  library.override = async ({ method }) => {
    if (method !== 'notifications.support') return undefined;
    await gate; return { supported: true, reason: null };
  };
  await page.goto('/#settings');
  const notifications = page.getByRole('switch', { name: 'Notifications', exact: true });
  await expect(notifications).toBeDisabled();
  const theme = page.getByLabel('Theme', { exact: true }); await expect(theme).toBeEnabled();
  await theme.selectOption('Light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'Light');
  release();
  await expect(notifications).toBeEnabled();
  expect(supportCalls(library.requests)).toHaveLength(1);
});
