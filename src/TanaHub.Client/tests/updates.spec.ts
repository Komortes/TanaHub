import { test, expect } from './fixtures';

async function desktop(page: import('@playwright/test').Page, options: { failDownload?: boolean; failInstall?: boolean; failRestart?: boolean; failPrepare?: boolean } = {}) {
  await page.addInitScript(({ failDownload, failInstall, failRestart, failPrepare }) => {
    const calls: string[] = [];
    Object.assign(window, { updateCalls: calls, __TAURI_INTERNALS__: {
      transformCallback: () => 1,
      unregisterCallback: () => {},
      invoke: async (command: string, args: Record<string, unknown> = {}) => {
        calls.push(command);
        if (command === 'backend_request') {
          const reply = await fetch('/api/rpc', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ method: args.method, params: args.params }) });
          return (await reply.json()).result;
        }
        if (command === 'update_support') return { supported: true, reason: null };
        if (command === 'plugin:updater|check') return { rid: 7, currentVersion: '0.1.1', version: '0.1.2', body: 'Cover loading improvements' };
        if (command === 'plugin:updater|download') {
          if (failDownload) throw new Error('Signature verification failed');
          const events = args.onEvent as { onmessage: (event: unknown) => void };
          events.onmessage({ event: 'Started', data: { contentLength: 100 } });
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
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
  await expect(page.getByText('Version 0.1.2 is available', { exact: true })).toBeVisible();
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
  await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
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
    await page.getByRole('button', { name: 'Check for updates', exact: true }).click();
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
