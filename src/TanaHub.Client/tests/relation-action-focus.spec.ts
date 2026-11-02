import type { Page } from '@playwright/test';
import { test, expect, type MockLibrary } from './fixtures';

const title = "Frieren: Beyond Journey's End";
const id = 'anilist:154587';
const relatedTitle = 'Frieren: The Next Journey';

async function openTitle(page: Page) {
  await page.goto('/#library');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Library status', exact: true })).toBeEnabled();
  await expect(page.getByRole('button', { name: 'Refresh related titles', exact: true })).toBeEnabled();
}

async function holdRelated(page: Page, library: MockLibrary, outcome: 'success' | 'failure') {
  let release!: () => void;
  const ready = new Promise<void>(resolve => { release = resolve; });
  let requested!: () => void;
  const started = new Promise<void>(resolve => { requested = resolve; });
  await page.route('**/api/rpc', async route => {
    const request = route.request().postDataJSON();
    if (request.method !== 'media.related' || !request.params.forceRefresh) { await route.fallback(); return; }
    requested();
    await ready;
    await route.fulfill({ json: outcome === 'success'
      ? { result: library.relations.get(id) }
      : { error: { code: 'provider_unavailable', message: 'Related titles are temporarily unavailable.' } } });
  });
  return { release, started };
}

for (const action of ['Refresh', 'Retry'] as const) {
  for (const outcome of ['success', 'failure'] as const) {
    test(`related-title ${action} keeps focus on its heading through ${outcome}`, async ({ page, library }) => {
      if (action === 'Retry') library.failNext('media.related', 'The initial connections request failed.');
      await openTitle(page);
      const region = page.getByRole('region', { name: 'Related titles', exact: true });
      const heading = region.getByRole('heading', { name: 'Related titles', exact: true });
      if (action === 'Retry') await expect(region.getByRole('alert')).toContainText('The initial connections request failed.');
      const held = await holdRelated(page, library, outcome);
      const control = action === 'Retry' ? region.getByRole('button', { name: 'Try again', exact: true })
        : region.getByRole('button', { name: 'Refresh related titles', exact: true });
      if (action === 'Retry') { await control.focus(); await control.press('Enter'); }
      else await control.click();
      await held.started;
      try {
        await expect(region).toHaveAttribute('aria-busy', 'true');
        await expect(heading).toBeFocused();
      } finally { held.release(); }

      await expect(region).toHaveAttribute('aria-busy', 'false');
      if (outcome === 'failure') await expect(region.getByRole('alert')).toContainText('Related titles are temporarily unavailable.');
      else await expect(region.getByRole('button', { name: new RegExp(`^Open ${relatedTitle},`) })).toBeVisible();
      await expect(heading).toBeFocused();
    });
  }
}

test('late related-title responses preserve later editor and navigation focus', async ({ page, library }) => {
  await openTitle(page);
  const region = page.getByRole('region', { name: 'Related titles', exact: true });
  const first = await holdRelated(page, library, 'success');
  await region.getByRole('button', { name: 'Refresh related titles', exact: true }).click();
  await first.started;
  await page.getByRole('button', { name: 'Add notes', exact: true }).click();
  const notes = page.getByRole('textbox', { name: 'Personal notes', exact: true });
  await notes.fill('Keep this unfinished note.');
  first.release();
  await expect(region).toHaveAttribute('aria-busy', 'false');
  await expect(notes).toBeFocused();
  await expect(notes).toHaveValue('Keep this unfinished note.');

  const second = await holdRelated(page, library, 'failure');
  await region.getByRole('button', { name: 'Refresh related titles', exact: true }).click();
  await second.started;
  await page.getByRole('navigation', { name: 'Main navigation', exact: true }).getByRole('button', { name: 'Discover', exact: true }).click();
  const search = page.getByRole('searchbox', { name: 'Search the catalog', exact: true });
  await search.focus();
  const response = page.waitForResponse(result => result.url().endsWith('/api/rpc') && result.request().postDataJSON().method === 'media.related');
  second.release();
  await (await response).finished();
  await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(search).toBeFocused();
  await expect(page.getByRole('heading', { name: 'Discover', exact: true, level: 1 })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
