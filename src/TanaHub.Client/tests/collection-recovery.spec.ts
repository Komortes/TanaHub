import type { Locator, Page } from '@playwright/test';
import { test, expect } from './fixtures';

const navigation = (page: Page) => page.getByRole('navigation', { name: 'Main navigation', exact: true });
async function activate(control: Locator, method: 'click' | 'Enter') {
  if (method === 'click') await control.click();
  else { await control.focus(); await control.press('Enter'); }
}

for (const activation of ['click', 'Enter'] as const) {
  test(`resetting an empty library filter returns to search after ${activation}`, async ({ page, library }) => {
    await page.goto('/#library');
    await page.getByRole('button', { name: 'Dropped', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'No matching titles', exact: true })).toBeVisible();
    await activate(page.getByRole('button', { name: 'Reset filters', exact: true }), activation);
    await expect(page.getByRole('button', { name: "Open Frieren: Beyond Journey's End", exact: true })).toBeVisible();
    await expect(page.getByRole('searchbox', { name: 'Search your library', exact: true })).toBeFocused();
    expect(library.requests.filter(request => request.method === 'library.list').at(-1)?.params.status).toBeUndefined();
  });

  test(`resetting an empty catalog filter returns to search after ${activation}`, async ({ page, library }) => {
    library.override = async request => request.method === 'catalog.search' && (request.params.genres as string[])?.includes('Horror')
      ? { items: [], page: 1, pageSize: 24, totalCount: 0, hasNextPage: false } : undefined;
    await page.goto('/#discover');
    await page.getByRole('combobox', { name: 'Filter catalog genre', exact: true }).selectOption('Horror');
    await expect(page.getByRole('heading', { name: 'No matching titles', exact: true })).toBeVisible();
    await activate(page.getByRole('button', { name: 'Reset filters', exact: true }), activation);
    await expect(page.getByRole('button', { name: "Open Frieren: Beyond Journey's End", exact: true })).toBeVisible();
    await expect(page.getByRole('searchbox', { name: 'Search the catalog', exact: true })).toBeFocused();
    await navigation(page).getByRole('button', { name: 'My library', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'My library', level: 1, exact: true })).toBeFocused();
  });
}
