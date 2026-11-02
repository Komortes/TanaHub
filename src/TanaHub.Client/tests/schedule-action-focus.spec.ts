import { test, expect } from './fixtures';

const title = "Frieren: Beyond Journey's End";

for (const activation of ['click', 'keyboard Enter'] as const) {
  test(`Schedule retry transfers focus to its persistent heading after ${activation}`, async ({ page, library }) => {
    await page.clock.setFixedTime('2026-11-02T10:00:00.000Z');
    library.failNext('schedule.list', 'A temporary schedule failure.');
    await page.goto('/#schedule');
    const retry = page.getByRole('button', { name: 'Try again', exact: true });
    await expect(retry).toBeVisible();
    if (activation === 'click') await retry.click();
    else { await retry.focus(); await retry.press('Enter'); }
    await expect(page.getByRole('button', { name: `Open ${title}, episode 4`, exact: true })).toBeVisible();
    await expect(retry).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Schedule', level: 1, exact: true })).toBeFocused();
    expect(library.requests.filter(request => request.method === 'schedule.list')).toHaveLength(2);
  });
}

for (const destination of ['timezone', 'another page'] as const) {
  test(`a late Schedule retry preserves later focus on ${destination}`, async ({ page, library }) => {
    await page.clock.setFixedTime('2026-11-02T10:00:00.000Z');
    let release!: () => void;
    const pending = new Promise<void>(resolve => { release = resolve; });
    let hold = false;
    library.override = async ({ method }) => { if (hold && method === 'schedule.list') await pending; return undefined; };
    library.failNext('schedule.list', 'A temporary schedule failure.');
    await page.goto('/#schedule');
    const retry = page.getByRole('button', { name: 'Try again', exact: true });
    await expect(retry).toBeVisible();
    hold = true;
    await retry.focus(); await retry.press('Enter');
    await expect.poll(() => library.requests.filter(request => request.method === 'schedule.list').length).toBe(2);
    const response = page.waitForResponse(reply => reply.url().endsWith('/api/rpc') && reply.request().postDataJSON().method === 'schedule.list');
    try {
      if (destination === 'another page') await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Discover', exact: true }).click();
      const chosen = destination === 'timezone'
        ? page.getByRole('combobox', { name: 'Schedule timezone', exact: true })
        : page.getByRole('searchbox', { name: 'Search the catalog', exact: true });
      await chosen.focus();
      release();
      await (await response).finished();
      await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
      await expect(chosen).toBeFocused();
      if (destination === 'timezone') await expect(page.getByRole('button', { name: `Open ${title}, episode 4`, exact: true })).toBeVisible();
      else await expect(page.getByRole('heading', { name: 'Discover', level: 1, exact: true })).toBeVisible();
    } finally { release(); }
  });
}
