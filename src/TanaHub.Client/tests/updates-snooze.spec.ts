import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';

const day = 86_400_000;
const now = Date.parse('2026-10-28T08:00:00.000Z');
const reminderKey = 'tanahub-update-reminder';
const metadataKey = 'tanahub-update-check';
const notice = (page: Page) => page.getByRole('region', { name: 'Application update', exact: true });
const remind = (page: Page) => notice(page).getByRole('button', { name: 'Remind in 24 hours', exact: true });
const readReminder = (page: Page) => page.evaluate(key => JSON.parse(localStorage.getItem(key) ?? 'null'), reminderKey);
const calls = (page: Page) => page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls);
const checkCount = async (page: Page) => (await calls(page)).filter(call => call === 'plugin:updater|check').length;

async function desktop(page: Page, options: { noUpdate?: boolean; deferCheck?: boolean; failInstall?: boolean } = {}) {
  await page.addInitScript(({ noUpdate, deferCheck, failInstall }) => {
    const updateCalls: string[] = [];
    let failCheck = false;
    let releaseCheck!: () => void;
    const gate = new Promise<void>(resolve => { releaseCheck = resolve; });
    Object.assign(window, {
      updateCalls, releaseUpdateCheck: releaseCheck,
      setUpdateVersion: (version: string | null) => sessionStorage.setItem('fixture-update-version', version ?? 'none'),
      failNextUpdateCheck: () => { failCheck = true; },
      __TAURI_INTERNALS__: {
        transformCallback: () => 1,
        unregisterCallback: () => {},
        invoke: async (command: string, args: Record<string, unknown> = {}) => {
          updateCalls.push(command);
          if (command === 'backend_request') {
            const response = await fetch('/api/rpc', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ method: args.method, params: args.params }) });
            return (await response.json()).result;
          }
          if (command === 'update_support') return { supported: true, reason: null };
          if (command === 'plugin:updater|check') {
            if (deferCheck) await gate;
            if (failCheck) { failCheck = false; throw new Error('Network unavailable'); }
            const version = sessionStorage.getItem('fixture-update-version') ?? (noUpdate ? 'none' : '0.1.3');
            return version === 'none' ? null : { rid: 7, currentVersion: '0.1.2', version, body: 'A newer preview' };
          }
          if (command === 'plugin:updater|download') {
            const events = args.onEvent as { onmessage: (event: unknown) => void };
            events.onmessage({ event: 'Started', data: { contentLength: 100 } });
            events.onmessage({ event: 'Progress', data: { chunkLength: 100 } });
            events.onmessage({ event: 'Finished' });
            return 8;
          }
          if (command === 'plugin:updater|install' && failInstall) throw new Error('Installer is busy');
          return null;
        },
      },
    });
  }, options);
}

async function seedReminder(page: Page, value: string) {
  await page.addInitScript(({ key, value }) => localStorage.setItem(key, value), { key: reminderKey, value });
}
async function snooze(page: Page) {
  await expect(remind(page)).toBeVisible();
  await remind(page).click();
  await expect(notice(page)).toHaveCount(0);
}

test('an explicit 24-hour reminder survives same-version reload and Settings keeps the download visible', async ({ page, library }) => {
  await page.clock.setFixedTime(now);
  await desktop(page);
  await page.goto('/#library');
  await snooze(page);
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeFocused();
  expect(await readReminder(page)).toEqual({ version: '0.1.3', until: now + day });
  await page.reload();
  await expect.poll(() => checkCount(page)).toBe(1);
  await page.getByRole('navigation').getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByText(/Version 0\.1\.3 is available/)).toBeVisible();
  await expect(page.getByText(/Reminder.*until/i)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Download update', exact: true })).toBeEnabled();
  await page.getByRole('navigation').getByRole('button', { name: 'My library', exact: true }).click();
  await expect(notice(page)).toHaveCount(0);
  expect(await checkCount(page)).toBe(1);
});

test('a different announced version bypasses the old reminder and clears its deadline', async ({ page, library }) => {
  await page.clock.setFixedTime(now);
  await desktop(page);
  await page.goto('/#library');
  await snooze(page);
  await page.evaluate(() => (window as unknown as { setUpdateVersion: (version: string) => void }).setUpdateVersion('0.1.4'));
  await page.reload();
  await expect(notice(page)).toContainText('TanaHub 0.1.4 is available');
  expect(await readReminder(page)).toBeNull();
  expect(await checkCount(page)).toBe(1);
});

test('an expired reminder allows the same version on a later launch', async ({ page, library }) => {
  await page.clock.setFixedTime(now);
  await desktop(page);
  await page.goto('/#library');
  await snooze(page);
  await page.clock.setFixedTime(now + day + 1);
  await page.reload();
  await expect(notice(page)).toContainText('TanaHub 0.1.3 is available');
  expect(await checkCount(page)).toBe(1);
});

test('an open-window reminder expires without taking focus or rechecking the feed', async ({ page, library }) => {
  await page.clock.install({ time: now });
  await desktop(page);
  await page.goto('/#library');
  await snooze(page);
  const until = (await readReminder(page)).until as number;
  const search = page.getByRole('searchbox', { name: 'Search your library', exact: true });
  await search.focus();
  await page.clock.fastForward(until - await page.evaluate(() => Date.now()) + 1);
  await expect(notice(page)).toContainText('TanaHub 0.1.3 is available');
  await expect(search).toBeFocused();
  expect(await readReminder(page)).toBeNull();
  expect(await checkCount(page)).toBe(1);
});

test('an accepted manual check overrides and clears a matching reminder', async ({ page, library }) => {
  await page.clock.setFixedTime(now);
  await desktop(page, { noUpdate: true });
  await seedReminder(page, JSON.stringify({ version: '0.1.3', until: now + day }));
  await page.goto('/#settings');
  await expect(page.getByText('You are up to date', { exact: true })).toBeVisible();
  await page.evaluate(() => (window as unknown as { setUpdateVersion: (version: string) => void }).setUpdateVersion('0.1.3'));
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(page.getByText('Version 0.1.3 is available', { exact: true })).toBeVisible();
  expect(await readReminder(page)).toBeNull();
  await page.getByRole('navigation').getByRole('button', { name: 'My library', exact: true }).click();
  await expect(notice(page)).toBeVisible();
  expect(await checkCount(page)).toBe(2);
});

for (const history of ['missing', 'corrupt', 'valid'] as const) {
  test(`a restored reminder is independent of ${history} completed-check history`, async ({ page, library }) => {
    await page.clock.setFixedTime(now);
    await desktop(page, { deferCheck: true });
    await seedReminder(page, JSON.stringify({ version: '0.1.3', until: now + day }));
    if (history !== 'missing') {
      await page.addInitScript(({ key, value }) => localStorage.setItem(key, value), { key: metadataKey, value: history === 'corrupt' ? '{' : JSON.stringify({ version: 1, lastAttempt: now - 3_600_000, lastSuccessfulCheck: now - 3_600_000, outcome: 'current' }) });
    }
    await page.goto('/#settings');
    await expect(page.getByRole('group', { name: 'Update check status', exact: true })).toContainText('Checking for updates');
    const lastSuccess = page.getByText('Last successful check', { exact: true }).locator('..');
    if (history === 'valid') await expect(lastSuccess.locator('time')).toHaveAttribute('datetime', new Date(now - 3_600_000).toISOString());
    else await expect(lastSuccess).toContainText('Not yet');
    await page.evaluate(() => (window as unknown as { releaseUpdateCheck: () => void }).releaseUpdateCheck());
    await expect(page.getByText(/Version 0\.1\.3 is available/)).toBeVisible();
    await expect(page.getByText(/Reminder.*until/i)).toBeVisible();
    await page.getByRole('navigation').getByRole('button', { name: 'My library', exact: true }).click();
    await expect(notice(page)).toHaveCount(0);
    expect(await checkCount(page)).toBe(1);
  });
}

test('downloading from Settings overrides the reminder and does not start installation', async ({ page, library }) => {
  await page.clock.setFixedTime(now);
  await desktop(page);
  await page.goto('/#library');
  await snooze(page);
  await page.getByRole('navigation').getByRole('button', { name: 'Settings', exact: true }).click();
  await page.getByRole('button', { name: 'Download update', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Install and restart', exact: true })).toBeVisible();
  expect(await readReminder(page)).toBeNull();
  expect(await calls(page)).not.toContain('prepare_update');
  expect(await calls(page)).not.toContain('plugin:updater|install');
  await page.getByRole('navigation').getByRole('button', { name: 'My library', exact: true }).click();
  await expect(notice(page).getByRole('button', { name: 'Later', exact: true })).toBeVisible();
});

test('denied reminder storage still dismisses this session without breaking the next launch', async ({ page, library }) => {
  await page.clock.setFixedTime(now);
  await desktop(page);
  await page.addInitScript(key => {
    const read = Storage.prototype.getItem, write = Storage.prototype.setItem, remove = Storage.prototype.removeItem;
    Storage.prototype.getItem = function (name) { if (name === key) throw new DOMException('Storage blocked', 'SecurityError'); return read.call(this, name); };
    Storage.prototype.setItem = function (name, value) { if (name === key) throw new DOMException('Storage blocked', 'SecurityError'); write.call(this, name, value); };
    Storage.prototype.removeItem = function (name) { if (name === key) throw new DOMException('Storage blocked', 'SecurityError'); remove.call(this, name); };
  }, reminderKey);
  await page.goto('/#library');
  await snooze(page);
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeFocused();
  await page.reload();
  await expect(notice(page)).toContainText('TanaHub 0.1.3 is available');
  await expect(page.getByRole('alert')).toHaveCount(0);
});

const invalidReminders = [
  { label: 'malformed JSON', value: '{' },
  { label: 'an invalid version', value: JSON.stringify({ version: '../invalid', until: now + day }) },
  { label: 'a nonnumeric deadline', value: JSON.stringify({ version: '0.1.3', until: 'tomorrow' }) },
  { label: 'a deadline beyond 24 hours or a rolled-back clock', value: JSON.stringify({ version: '0.1.3', until: now + day + 1 }) },
  { label: 'an expired deadline', value: JSON.stringify({ version: '0.1.3', until: now - 1 }) },
];
for (const { label, value } of invalidReminders) {
  test(`update reminders ignore ${label}`, async ({ page, library }) => {
    await page.clock.setFixedTime(now);
    await desktop(page);
    await seedReminder(page, value);
    await page.goto('/#library');
    await expect(notice(page)).toContainText('TanaHub 0.1.3 is available');
    await expect(page.getByRole('alert')).toHaveCount(0);
    expect(await checkCount(page)).toBe(1);
  });
}

for (const installError of [false, true]) {
  test(`${installError ? 'an install error' : 'a downloaded update'} keeps Later session-only`, async ({ page, library }) => {
    await desktop(page, { failInstall: installError });
    await page.goto('/#library');
    await notice(page).getByRole('button', { name: 'Download update', exact: true }).click();
    await expect(notice(page).getByRole('button', { name: 'Install and restart', exact: true })).toBeVisible();
    if (installError) {
      await notice(page).getByRole('button', { name: 'Install and restart', exact: true }).click();
      await expect(notice(page).getByRole('alert')).toContainText('Installer is busy');
    }
    await expect(remind(page)).toHaveCount(0);
    await notice(page).getByRole('button', { name: 'Later', exact: true }).click();
    await expect(notice(page)).toHaveCount(0);
    expect(await readReminder(page)).toBeNull();
    await page.reload();
    await expect(notice(page)).toContainText('TanaHub 0.1.3 is available');
  });
}

test('a manual-check error keeps Later session-only', async ({ page, library }) => {
  await desktop(page, { noUpdate: true });
  await page.goto('/#settings');
  await expect(page.getByText('You are up to date', { exact: true })).toBeVisible();
  await page.evaluate(() => (window as unknown as { failNextUpdateCheck: () => void }).failNextUpdateCheck());
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await page.getByRole('navigation').getByRole('button', { name: 'My library', exact: true }).click();
  await expect(notice(page).getByRole('alert')).toContainText('Network unavailable');
  await expect(remind(page)).toHaveCount(0);
  await notice(page).getByRole('button', { name: 'Later', exact: true }).click();
  await expect(notice(page)).toHaveCount(0);
  expect(await readReminder(page)).toBeNull();
});
