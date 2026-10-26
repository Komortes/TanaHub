import { test, expect } from './fixtures';

async function desktop(page: import('@playwright/test').Page, options: { failDownload?: boolean; failInstall?: boolean; failRestart?: boolean; failPrepare?: boolean; failChecks?: number; failChecksOnReload?: boolean; noUpdate?: boolean; unsupported?: boolean; deferCheck?: boolean; deferDownload?: boolean } = {}) {
  await page.addInitScript(({ failDownload, failInstall, failRestart, failPrepare, failChecks = 0, failChecksOnReload, noUpdate, unsupported, deferCheck, deferDownload }) => {
    const calls: string[] = [];
    const previousLaunch = sessionStorage.getItem('fixture-update-launched') === 'yes';
    sessionStorage.setItem('fixture-update-launched', 'yes');
    if (failChecksOnReload && previousLaunch) failChecks = 1;
    let releaseCheck!: () => void, releaseDownload!: () => void;
    const checkGate = new Promise<void>(resolve => { releaseCheck = resolve; });
    const downloadGate = new Promise<void>(resolve => { releaseDownload = resolve; });
    Object.assign(window, { updateCalls: calls, releaseUpdateCheck: releaseCheck, releaseUpdateDownload: releaseDownload, failNextUpdateCheck: () => { failChecks = 1; }, __TAURI_INTERNALS__: {
      transformCallback: () => 1,
      unregisterCallback: () => {},
      invoke: async (command: string, args: Record<string, unknown> = {}) => {
        calls.push(command);
        if (command === 'backend_request') {
          const reply = await fetch('/api/rpc', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ method: args.method, params: args.params }) });
          return (await reply.json()).result;
        }
        if (command === 'update_support') return { supported: !unsupported, reason: unsupported ? 'In-app updates on Linux require the AppImage. Update the DEB version through your package manager or Browse releases.' : null };
        if (command === 'plugin:updater|check') {
          if (deferCheck) await checkGate;
          if (failChecks-- > 0) throw new Error('Network unavailable');
          return noUpdate ? null : { rid: 7, currentVersion: '0.1.2', version: '0.1.3', body: 'Cover loading improvements' };
        }
        if (command === 'plugin:updater|download') {
          if (failDownload) throw new Error('Signature verification failed');
          const events = args.onEvent as { onmessage: (event: unknown) => void };
          events.onmessage({ event: 'Started', data: { contentLength: 100 } });
          if (deferDownload) await downloadGate;
          events.onmessage({ event: 'Progress', data: { chunkLength: 100 } });
          events.onmessage({ event: 'Finished' });
          return 8;
        }
        if (command === 'prepare_update' && failPrepare) throw new Error('Finish the current library operation');
        if (command === 'plugin:updater|install' && failInstall) throw new Error('Installer is busy');
        if (command === 'plugin:process|restart' && failRestart) throw new Error('Restart is unavailable');
        return null;
      },
    } });
  }, options);
}

test('desktop updates download separately and install only after explicit confirmation', async ({ page, library }, testInfo) => {
  await page.setViewportSize({ width: 320, height: 900 });
  await desktop(page);
  await page.goto('/#settings');
  await expect(page.getByText('Version 0.1.3 is available', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Download update', exact: true }).click();
  const install = page.getByRole('button', { name: 'Install and restart', exact: true });
  await expect(install).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('updater-mobile.png') });
  expect(await page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls)).not.toContain('prepare_update');
  await install.click();
  await expect.poll(() => page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls)).toContain('plugin:process|restart');
  const calls = await page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls);
  expect(calls.indexOf('prepare_update')).toBeLessThan(calls.indexOf('plugin:updater|install'));
  expect(library.requests.some(request => request.method === 'library.import.apply')).toBe(false);
});

test('an invalid update signature never reaches installation and allows retry', async ({ page, library }) => {
  await desktop(page, { failDownload: true });
  await page.goto('/#settings');
  await page.getByRole('button', { name: 'Download update', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Signature verification failed');
  await expect(page.getByRole('button', { name: 'Check for updates', exact: true })).toBeEnabled();
  const calls = await page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls);
  expect(calls).not.toContain('prepare_update');
  expect(calls).not.toContain('plugin:updater|install');
});

for (const failure of ['failPrepare', 'failInstall', 'failRestart'] as const) {
  test(`${failure} leaves a recoverable update state`, async ({ page, library }) => {
    await desktop(page, { [failure]: true });
    await page.goto('/#settings');
      await page.getByRole('button', { name: 'Download update', exact: true }).click();
    await page.getByRole('button', { name: 'Install and restart', exact: true }).click();
    if (failure === 'failRestart') {
      await expect(page.getByRole('dialog').getByRole('button', { name: 'Restart now', exact: true })).toBeVisible();
      await expect(page.getByRole('dialog').getByRole('alert')).toContainText('Quit and reopen');
    } else {
      await expect(page.getByRole('button', { name: 'Install and restart', exact: true })).toBeEnabled();
      await expect.poll(() => page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls)).toContain('resume_after_update');
    }
    const calls = await page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls);
    if (failure === 'failPrepare') expect(calls).not.toContain('plugin:updater|install');
  });
}

const notice = (page: import('@playwright/test').Page) => page.getByRole('region', { name: 'Application update', exact: true });
const checkCount = (page: import('@playwright/test').Page) => page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls.filter(call => call === 'plugin:updater|check').length);

test('startup check announces an available update without interrupting focus or checking again on navigation', async ({ page, library }) => {
  await desktop(page, { deferCheck: true });
  await page.goto('/');
  const search = page.getByRole('searchbox', { name: 'Search your library', exact: true });
  await search.fill('Frieren');
  await expect.poll(() => checkCount(page)).toBe(1);
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Checking…', exact: true })).toBeDisabled();
  await expect(page.getByRole('switch', { name: 'Notifications', exact: true })).toBeEnabled();
  expect(await checkCount(page)).toBe(1);
  await page.getByRole('button', { name: 'My library', exact: true }).click();
  await search.focus();
  await page.evaluate(() => (window as unknown as { releaseUpdateCheck: () => void }).releaseUpdateCheck());
  await expect(notice(page)).toContainText('TanaHub 0.1.3 is available');
  await expect(search).toBeFocused();
  await expect(notice(page).getByRole('button', { name: 'Download update', exact: true })).toBeVisible();
  expect(await page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls)).not.toContain('plugin:updater|download');
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByText('Version 0.1.3 is available', { exact: true })).toBeVisible();
  await expect(notice(page)).toHaveCount(0);
  await page.getByRole('button', { name: 'My library', exact: true }).click();
  await expect(notice(page)).toBeVisible();
  expect(await checkCount(page)).toBe(1);
});

test('Later dismisses the startup notification for this launch and a new launch checks again', async ({ page, library }) => {
  await desktop(page);
  await page.goto('/');
  await notice(page).getByRole('button', { name: 'Later', exact: true }).click();
  await expect(notice(page)).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeFocused();
  await page.getByRole('button', { name: 'Discover', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Discover', exact: true })).toBeVisible();
  await expect(notice(page)).toHaveCount(0);
  expect(await checkCount(page)).toBe(1);
  await page.reload();
  await expect(notice(page)).toBeVisible();
  expect(await checkCount(page)).toBe(1);
});

test('a failed automatic check stays quiet and Settings can retry it manually', async ({ page, library }) => {
  await desktop(page, { failChecks: 1 });
  await page.goto('/');
  await expect.poll(() => checkCount(page)).toBe(1);
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Check for updates', exact: true })).toBeEnabled();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(notice(page)).toHaveCount(0);
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(page.getByText('Version 0.1.3 is available', { exact: true })).toBeVisible();
  expect(await checkCount(page)).toBe(2);
});

test('an up-to-date launch stays quiet and does not recheck on opening Settings', async ({ page, library }) => {
  await desktop(page, { noUpdate: true });
  await page.goto('/');
  await expect.poll(() => checkCount(page)).toBe(1);
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByText('You are up to date', { exact: true })).toBeVisible();
  await expect(notice(page)).toHaveCount(0);
  expect(await checkCount(page)).toBe(1);
});

test('unsupported packages do not check the feed and explain the package-manager path in Settings', async ({ page, library }) => {
  await desktop(page, { unsupported: true });
  await page.goto('/');
  await expect.poll(() => page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls.includes('update_support'))).toBe(true);
  await expect(notice(page)).toHaveCount(0);
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByText(/In-app updates on Linux require the AppImage/)).toBeVisible();
  expect(await checkCount(page)).toBe(0);
});

test('startup notification keeps a download across pages and installs only on explicit confirmation', async ({ page, library }) => {
  await desktop(page, { deferDownload: true, failRestart: true });
  await page.goto('/');
  await notice(page).getByRole('button', { name: 'Download update', exact: true }).click();
  await expect(notice(page).getByRole('progressbar')).toBeVisible();
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByRole('progressbar', { name: 'Update download progress', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Home', exact: true }).click();
  await page.evaluate(() => (window as unknown as { releaseUpdateDownload: () => void }).releaseUpdateDownload());
  await expect(notice(page)).toContainText('Save your changes before restarting');
  await expect(page.getByRole('heading', { name: 'Home', exact: true })).toBeFocused();
  const before = await page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls);
  expect(before.filter(call => call === 'plugin:updater|download')).toHaveLength(1);
  expect(before).not.toContain('prepare_update');
  expect(before).not.toContain('plugin:resources|close');
  await notice(page).getByRole('button', { name: 'Install and restart', exact: true }).click();
  await expect(page.getByRole('dialog').getByRole('button', { name: 'Restart now', exact: true })).toBeVisible();
  const after = await page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls);
  expect(after.indexOf('prepare_update')).toBeLessThan(after.indexOf('plugin:updater|install'));
  expect(await checkCount(page)).toBe(1);
});

test('startup notification fits narrow and desktop screens', async ({ page, library }, testInfo) => {
  await desktop(page);
  await page.goto('/');
  for (const width of [320, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    await expect(notice(page)).toBeInViewport();
    await expect(notice(page).getByRole('button', { name: 'Download update', exact: true })).toBeInViewport();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await expect(page.locator('vite-error-overlay')).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath(`notice-${width}.png`) });
  }
  await notice(page).getByRole('button', { name: 'Download update', exact: true }).click();
  await expect(notice(page).getByRole('button', { name: 'Install and restart', exact: true })).toBeFocused();
  await page.screenshot({ path: testInfo.outputPath('ready-1440.png') });
});


test('browser preview does not start native or legacy update checks automatically', async ({ page, library }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'My library', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Check for updates', exact: true })).toBeEnabled();
  await expect(notice(page)).toHaveCount(0);
  await expect(page.getByRole('group', { name: 'Update check status', exact: true })).toHaveCount(0);
  expect(library.requests.some(request => request.method === 'updates.check')).toBe(false);
});

const updateStatus = (page: import('@playwright/test').Page) => page.getByRole('group', { name: 'Update check status', exact: true });
const checkTime = (page: import('@playwright/test').Page, label: 'Last attempt' | 'Last successful check') => updateStatus(page).getByText(label, { exact: true }).locator('..').locator('time');
const attemptAt = '2026-10-27T08:00:00.000Z';
const completedAt = '2026-10-27T09:00:00.000Z';
const retriedAt = '2026-10-27T10:00:00.000Z';

test('update check status separates a deferred attempt from successful completion and survives navigation', async ({ page, library }) => {
  await page.clock.setFixedTime(attemptAt);
  await desktop(page, { noUpdate: true, deferCheck: true });
  await page.goto('/#library');
  await expect.poll(() => checkCount(page)).toBe(1);
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(updateStatus(page)).toContainText('Checking for updates');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', attemptAt);
  await expect(updateStatus(page).getByText('Last successful check', { exact: true }).locator('..')).toContainText('Not yet');
  await page.getByRole('button', { name: 'My library', exact: true }).click();
  const search = page.getByRole('searchbox', { name: 'Search your library', exact: true });
  await search.focus();
  await page.clock.setFixedTime(completedAt);
  await page.evaluate(() => (window as unknown as { releaseUpdateCheck: () => void }).releaseUpdateCheck());
  await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(search).toBeFocused();
  await expect(notice(page)).toHaveCount(0);
  await expect(updateStatus(page)).toHaveCount(0);
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(updateStatus(page)).toContainText('Up to date');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', attemptAt);
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', completedAt);
  expect(await checkCount(page)).toBe(1);
});

test('update check status explains a quiet startup failure in Settings and records a successful manual retry', async ({ page, library }) => {
  await page.clock.setFixedTime(attemptAt);
  await desktop(page, { failChecks: 1 });
  await page.goto('/#library');
  await expect.poll(() => checkCount(page)).toBe(1);
  await expect(notice(page)).toHaveCount(0);
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(updateStatus(page)).toHaveCount(0);
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(updateStatus(page)).toContainText('Check failed');
  await expect(updateStatus(page)).toContainText('Network unavailable');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', attemptAt);
  await expect(updateStatus(page).getByText('Last successful check', { exact: true }).locator('..')).toContainText('Not yet');
  await expect(page.getByRole('alert')).toHaveCount(0);
  await page.clock.setFixedTime(completedAt);
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(updateStatus(page)).toContainText('Update available');
  await expect(updateStatus(page)).not.toContainText('Network unavailable');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', completedAt);
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', completedAt);
  expect(await checkCount(page)).toBe(2);
});

test('update check status keeps the earlier successful time after a manual failure and clears failure on retry', async ({ page, library }) => {
  await page.clock.setFixedTime(attemptAt);
  await desktop(page, { noUpdate: true });
  await page.goto('/#settings');
  await expect(updateStatus(page)).toContainText('Up to date');
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', attemptAt);
  await page.evaluate(() => (window as unknown as { failNextUpdateCheck: () => void }).failNextUpdateCheck());
  await page.clock.setFixedTime(completedAt);
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(updateStatus(page)).toContainText('Check failed');
  await expect(page.getByRole('alert')).toContainText('Network unavailable');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', completedAt);
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', attemptAt);
  await page.clock.setFixedTime(retriedAt);
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(updateStatus(page)).toContainText('Up to date');
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', retriedAt);
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', retriedAt);
  expect(await checkCount(page)).toBe(3);
});

test('update check status explains unsupported packages without claiming a successful feed check', async ({ page, library }) => {
  await page.clock.setFixedTime(attemptAt);
  await desktop(page, { unsupported: true });
  await page.goto('/#settings');
  await expect(updateStatus(page)).toContainText('Updates unavailable for this package');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', attemptAt);
  await expect(updateStatus(page).getByText('Last successful check', { exact: true }).locator('..')).toContainText('Not yet');
  await expect(page.getByText(/In-app updates on Linux require the AppImage/)).toBeVisible();
  expect(await checkCount(page)).toBe(0);
  await page.getByRole('button', { name: 'My library', exact: true }).click();
  await expect(notice(page)).toHaveCount(0);
  await expect(updateStatus(page)).toHaveCount(0);
});

test('update check status stays tied to the check while a download becomes ready', async ({ page, library }, testInfo) => {
  await page.setViewportSize({ width: 320, height: 900 });
  await page.clock.setFixedTime(attemptAt);
  await desktop(page);
  await page.goto('/#settings');
  await expect(updateStatus(page)).toContainText('Update available');
  await page.clock.setFixedTime(completedAt);
  await page.getByRole('button', { name: 'Download update', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Install and restart', exact: true })).toBeVisible();
  await expect(updateStatus(page)).toContainText('Update available');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', attemptAt);
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', attemptAt);
  expect(await checkCount(page)).toBe(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  expect(await page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls)).not.toContain('prepare_update');
  await page.screenshot({ path: testInfo.outputPath('update-check-status-320.png'), fullPage: true });
});

const checkMetadataKey = 'tanahub-update-check';
const readCheckMetadata = (page: import('@playwright/test').Page) => page.evaluate(key => JSON.parse(localStorage.getItem(key) ?? 'null'), checkMetadataKey);

test('update check metadata retains the last successful check after an offline relaunch without persisting failure details', async ({ page, library }) => {
  await page.clock.setFixedTime(attemptAt);
  await desktop(page, { noUpdate: true, failChecksOnReload: true });
  await page.goto('/#settings');
  await expect(updateStatus(page)).toContainText('Up to date');
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', attemptAt);
  await page.clock.setFixedTime(completedAt);
  await page.reload();
  await expect(updateStatus(page)).toContainText('Check failed');
  await expect(updateStatus(page)).toContainText('Network unavailable');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', completedAt);
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', attemptAt);
  await expect(page.getByRole('alert')).toHaveCount(0);
  expect(await readCheckMetadata(page)).toEqual({ version: 1, lastAttempt: Date.parse(completedAt), lastSuccessfulCheck: Date.parse(attemptAt), outcome: 'failed' });
  expect(await checkCount(page)).toBe(1);
  await page.getByRole('button', { name: 'My library', exact: true }).click();
  await expect(notice(page)).toHaveCount(0);
  await expect(updateStatus(page)).toHaveCount(0);
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('update check metadata restores completed history while a new native check is pending', async ({ page, library }) => {
  const saved = { version: 1, lastAttempt: Date.parse(attemptAt), lastSuccessfulCheck: Date.parse(attemptAt), outcome: 'current' };
  await page.clock.setFixedTime(completedAt);
  await desktop(page, { noUpdate: true, deferCheck: true });
  await page.addInitScript(({ key, value }) => { if (localStorage.getItem(key) === null) localStorage.setItem(key, JSON.stringify(value)); }, { key: checkMetadataKey, value: saved });
  await page.goto('/#settings');
  await expect(updateStatus(page)).toContainText('Checking for updates');
  await expect(checkTime(page, 'Last attempt')).toHaveAttribute('datetime', completedAt);
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', attemptAt);
  expect(await readCheckMetadata(page)).toEqual(saved);
  await expect(notice(page)).toHaveCount(0);
  await page.clock.setFixedTime(retriedAt);
  await page.evaluate(() => (window as unknown as { releaseUpdateCheck: () => void }).releaseUpdateCheck());
  await expect(updateStatus(page)).toContainText('Up to date');
  expect(await readCheckMetadata(page)).toEqual({ version: 1, lastAttempt: Date.parse(completedAt), lastSuccessfulCheck: Date.parse(retriedAt), outcome: 'current' });
  expect(await checkCount(page)).toBe(1);
  await page.reload();
  await expect(updateStatus(page)).toContainText('Checking for updates');
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', retriedAt);
  expect(await readCheckMetadata(page)).toEqual({ version: 1, lastAttempt: Date.parse(completedAt), lastSuccessfulCheck: Date.parse(retriedAt), outcome: 'current' });
  await page.evaluate(() => (window as unknown as { releaseUpdateCheck: () => void }).releaseUpdateCheck());
  await expect(updateStatus(page)).toContainText('Up to date');
  expect(await checkCount(page)).toBe(1);
});

const invalidCheckMetadata = [
  { label: 'malformed JSON', value: '{"version":' },
  { label: 'unknown version', value: JSON.stringify({ version: 9, lastAttempt: Date.parse(attemptAt), lastSuccessfulCheck: Date.parse(attemptAt), outcome: 'current' }) },
  { label: 'a non-numeric date', value: JSON.stringify({ version: 1, lastAttempt: Date.parse(attemptAt), lastSuccessfulCheck: 'yesterday', outcome: 'current' }) },
  { label: 'a future date', value: JSON.stringify({ version: 1, lastAttempt: Date.parse(retriedAt) + 86_400_000, lastSuccessfulCheck: Date.parse(retriedAt) + 86_400_000, outcome: 'current' }) },
  { label: 'pending native state', value: JSON.stringify({ version: 1, lastAttempt: Date.parse(attemptAt), lastSuccessfulCheck: Date.parse(attemptAt), outcome: 'checking', phase: 'downloading', update: { rid: 7 } }) },
];

for (const { label, value } of invalidCheckMetadata) {
  test(`update check metadata ignores ${label} and replaces it with a completed check`, async ({ page, library }) => {
    await page.clock.setFixedTime(completedAt);
    await desktop(page, { noUpdate: true, deferCheck: true });
    await page.addInitScript(({ key, value }) => localStorage.setItem(key, value), { key: checkMetadataKey, value });
    await page.goto('/#settings');
    await expect(updateStatus(page)).toContainText('Checking for updates');
    await expect(updateStatus(page).getByText('Last successful check', { exact: true }).locator('..')).toContainText('Not yet');
    await expect(notice(page)).toHaveCount(0);
    await page.evaluate(() => (window as unknown as { releaseUpdateCheck: () => void }).releaseUpdateCheck());
    await expect(updateStatus(page)).toContainText('Up to date');
    expect(await readCheckMetadata(page)).toEqual({ version: 1, lastAttempt: Date.parse(completedAt), lastSuccessfulCheck: Date.parse(completedAt), outcome: 'current' });
    expect(await checkCount(page)).toBe(1);
  });
}

test('update check metadata tolerates denied storage while checking and retrying remain usable', async ({ page, library }) => {
  await page.clock.setFixedTime(attemptAt);
  await desktop(page, { noUpdate: true });
  await page.addInitScript(key => {
    const read = Storage.prototype.getItem;
    const write = Storage.prototype.setItem;
    Storage.prototype.getItem = function (name) { if (name === key) throw new DOMException('Storage is blocked', 'SecurityError'); return read.call(this, name); };
    Storage.prototype.setItem = function (name, value) { if (name === key) throw new DOMException('Storage is blocked', 'SecurityError'); write.call(this, name, value); };
  }, checkMetadataKey);
  await page.goto('/#settings');
  await expect(updateStatus(page)).toContainText('Up to date');
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', attemptAt);
  await expect(page.getByRole('alert')).toHaveCount(0);
  await page.clock.setFixedTime(completedAt);
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(checkTime(page, 'Last successful check')).toHaveAttribute('datetime', completedAt);
  expect(await checkCount(page)).toBe(2);
});
