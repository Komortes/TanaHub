import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';

async function desktop(page: Page, options: { failDownload?: boolean; failInstall?: boolean } = {}) {
  await page.addInitScript(({ failDownload, failInstall }) => {
    const updateCalls: string[] = [];
    let releaseDownload!: () => void, releaseInstall!: () => void;
    const download = new Promise<void>(resolve => { releaseDownload = resolve; });
    const install = new Promise<void>(resolve => { releaseInstall = resolve; });
    Object.assign(window, { updateCalls, releaseActionDownload: releaseDownload, releaseActionInstall: releaseInstall, __TAURI_INTERNALS__: {
      transformCallback: () => 1, unregisterCallback: () => {},
      invoke: async (command: string, args: Record<string, unknown> = {}) => {
        updateCalls.push(command);
        if (command === 'backend_request') {
          const response = await fetch('/api/rpc', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ method: args.method, params: args.params }) });
          return (await response.json()).result;
        }
        if (command === 'update_support') return { supported: true, reason: null };
        if (command === 'plugin:updater|check') return { rid: 7, currentVersion: '0.1.4', version: '0.1.5', body: 'An isolated update fixture' };
        if (command === 'plugin:updater|download') {
          const events = args.onEvent as { onmessage: (event: unknown) => void };
          events.onmessage({ event: 'Started', data: { contentLength: 100 } });
          await download;
          if (failDownload) throw new Error('Signature verification failed');
          events.onmessage({ event: 'Progress', data: { chunkLength: 100 } });
          events.onmessage({ event: 'Finished' });
          return 8;
        }
        if (command === 'plugin:updater|install') {
          await install;
          if (failInstall) throw new Error('Installer is busy');
        }
        return null;
      },
    } });
  }, options);
}
const releaseDownload = (page: Page) => page.evaluate(() => (window as unknown as { releaseActionDownload: () => void }).releaseActionDownload());
const releaseInstall = (page: Page) => page.evaluate(() => (window as unknown as { releaseActionInstall: () => void }).releaseActionInstall());
const calls = (page: Page) => page.evaluate(() => (window as unknown as { updateCalls: string[] }).updateCalls);

for (const outcome of ['ready', 'download error'] as const) {
  test(`Settings download restores keyboard focus to the usable ${outcome} action`, async ({ page, library }) => {
    await desktop(page, { failDownload: outcome === 'download error' });
    await page.goto('/#settings');
    const download = page.getByRole('button', { name: 'Download update', exact: true });
    await expect(download).toBeVisible();
    await download.focus(); await download.press('Enter');
    await releaseDownload(page);
    const next = page.getByRole('button', { name: outcome === 'ready' ? 'Install and restart' : 'Check for updates', exact: true });
    await expect(next).toBeEnabled();
    await expect(next).toBeFocused();
    if (outcome === 'download error') await expect(page.getByRole('alert')).toContainText('Signature verification failed');
    expect(await calls(page)).not.toContain('prepare_update');
    expect(await calls(page)).not.toContain('plugin:updater|install');
  });
}

for (const origin of ['settings', 'library'] as const) {
  test(`a failed update installation restores its retry focus from ${origin}`, async ({ page, library }) => {
    await desktop(page, { failInstall: true });
    await page.goto(`/#${origin}`);
    await page.getByRole('button', { name: 'Download update', exact: true }).click();
    await releaseDownload(page);
    const install = page.getByRole('button', { name: 'Install and restart', exact: true });
    await expect(install).toBeVisible();
    await install.focus(); await install.press('Enter');
    const dialog = page.getByRole('dialog', { name: 'Installing update', exact: true });
    await expect(dialog).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(dialog).toBeVisible();
    await releaseInstall(page);
    await expect(page.getByRole('alert')).toContainText('Installer is busy');
    await expect(dialog).toHaveCount(0);
    await expect(install).toBeEnabled();
    await expect(install).toBeFocused();
    expect(await calls(page)).toContain('resume_after_update');
    expect(await calls(page)).not.toContain('plugin:process|restart');
  });
}

for (const destination of ['navigation control', 'another page'] as const) {
  test(`a late Settings download preserves deliberate focus on ${destination}`, async ({ page, library }) => {
    await desktop(page);
    await page.goto('/#settings');
    const download = page.getByRole('button', { name: 'Download update', exact: true });
    await expect(download).toBeVisible();
    await download.focus(); await download.press('Enter');
    const nav = page.getByRole('navigation', { name: 'Main navigation' });
    if (destination === 'another page') await nav.getByRole('button', { name: 'Discover', exact: true }).click();
    const chosen = destination === 'another page'
      ? page.getByRole('searchbox', { name: 'Search the catalog', exact: true })
      : nav.getByRole('button', { name: 'Home', exact: true });
    await chosen.focus();
    await releaseDownload(page);
    await expect(page.getByRole('button', { name: 'Install and restart', exact: true })).toBeVisible();
    await expect(chosen).toBeFocused();
    expect(await calls(page)).not.toContain('prepare_update');
  });
}

for (const width of [320, 360]) {
  test(`a failed Settings installation keeps its retry action fully visible at ${width}px`, async ({ page, library }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    await desktop(page, { failInstall: true });
    await page.goto('/#settings');
    await page.getByRole('button', { name: 'Download update', exact: true }).click();
    await releaseDownload(page);
    const install = page.getByRole('button', { name: 'Install and restart', exact: true });
    await expect(install).toBeVisible();
    await install.click();
    await expect(page.getByRole('dialog', { name: 'Installing update', exact: true })).toBeVisible();
    await releaseInstall(page);
    await expect(page.getByRole('alert')).toContainText('Installer is busy');
    await expect(install).toBeFocused();
    await page.screenshot({ path: testInfo.outputPath('visible-install-retry.png') });
    await expect(install).toBeInViewport({ ratio: 1 });
    expect(await calls(page)).toContain('resume_after_update');
    expect(await calls(page)).not.toContain('plugin:process|restart');
  });
}
