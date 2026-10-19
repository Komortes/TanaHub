import { test, expect } from './fixtures';

async function desktop(page: import('@playwright/test').Page, options: { failDownload?: boolean; failInstall?: boolean; failRestart?: boolean; failPrepare?: boolean; failChecks?: number; noUpdate?: boolean; unsupported?: boolean; deferCheck?: boolean; deferDownload?: boolean } = {}) {
  await page.addInitScript(({ failDownload, failInstall, failRestart, failPrepare, failChecks = 0, noUpdate, unsupported, deferCheck, deferDownload }) => {
    const calls: string[] = [];
    let releaseCheck!: () => void, releaseDownload!: () => void;
    const checkGate = new Promise<void>(resolve => { releaseCheck = resolve; });
    const downloadGate = new Promise<void>(resolve => { releaseDownload = resolve; });
    Object.assign(window, { updateCalls: calls, releaseUpdateCheck: releaseCheck, releaseUpdateDownload: releaseDownload, __TAURI_INTERNALS__: {
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
  expect(library.requests.some(request => request.method === 'updates.check')).toBe(false);
});
