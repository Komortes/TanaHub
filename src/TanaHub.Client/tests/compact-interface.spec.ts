import { test, expect } from './fixtures';

const title = "Frieren: Beyond Journey's End";
const id = 'anilist:154587';

test('narrow library uses two compact columns and a short primary toolbar', async ({ page, library }) => {
  await page.setViewportSize({ width: 320, height: 800 });
  await page.goto('/#library');
  const cards = page.getByRole('article');
  await expect(cards).toHaveCount(4);
  const first = await cards.nth(0).boundingBox();
  const second = await cards.nth(1).boundingBox();
  expect(first && second).toBeTruthy();
  expect(first!.width).toBeGreaterThanOrEqual(130);
  expect(first!.width).toBeLessThanOrEqual(170);
  expect(Math.abs(first!.y - second!.y)).toBeLessThan(1);
  expect(second!.x).toBeGreaterThan(first!.x + first!.width);
  expect(await page.locator('.collection-toolbar').evaluate(node => node.getBoundingClientRect().height)).toBeLessThan(135);
  await expect(page.getByRole('combobox', { name: 'Filter by tag' })).toHaveCount(0);
  await expect(page.getByRole('combobox', { name: 'Filter by custom list' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Display options', exact: true }).click();
  await expect(page.getByRole('slider', { name: 'Cover size' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Filter by tag' })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
});

test('collapsed filters retain an active indication and can be cleared', async ({ page, library }) => {
  library.entries.get(id)!.tags = ['comfort'];
  await page.goto('/#library');
  await page.getByRole('button', { name: 'Filters and display', exact: true }).click();
  await page.getByRole('combobox', { name: 'Filter by tag' }).selectOption('comfort');
  await expect(page.getByRole('article')).toHaveCount(1);
  const toggle = page.getByRole('button', { name: 'Filters and display, 1 active filters' });
  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  await expect(toggle).toContainText('1');
  await toggle.click();
  await page.getByRole('button', { name: 'Clear filters', exact: true }).click();
  await expect(page.getByRole('article')).toHaveCount(4);
  await expect(page.getByRole('combobox', { name: 'Filter by tag' })).toHaveValue('');
  await expect(page.getByRole('button', { name: 'Filters and display', exact: true })).toBeFocused();
});

test('journal drafts survive collapse, unrelated saves and errors, with explicit discard', async ({ page, library }) => {
  await page.goto('/#library');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  const journal = page.getByRole('region', { name: 'Your space', exact: true });
  await expect(journal.getByRole('textbox')).toHaveCount(0);
  await journal.getByRole('button', { name: 'Add notes', exact: true }).click();
  const notes = journal.getByRole('textbox', { name: 'Personal notes' });
  await expect(notes).toBeFocused();
  await notes.fill('Remember the quiet moments 🌙');
  await journal.getByRole('button', { name: 'Close notes editor', exact: true }).click();
  await expect(journal.getByText('Unsaved draft', { exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: 'Library status', exact: true }).selectOption('Paused');
  await expect.poll(() => library.entries.get(id)!.status).toBe('Paused');
  await journal.getByRole('button', { name: 'Continue editing notes', exact: true }).click();
  await expect(notes).toHaveValue('Remember the quiet moments 🌙');
  library.failNext('library.notes', 'Could not save this note.');
  await journal.getByRole('button', { name: 'Save notes', exact: true }).click();
  await expect(journal.getByRole('alert')).toHaveText('Could not save this note.');
  await expect(notes).toHaveValue('Remember the quiet moments 🌙');
  await journal.getByRole('button', { name: 'Save notes', exact: true }).click();
  await expect.poll(() => library.entries.get(id)!.notes).toBe('Remember the quiet moments 🌙');
  await journal.getByRole('button', { name: 'Close notes editor', exact: true }).click();
  await expect(journal.getByText('Remember the quiet moments 🌙', { exact: true })).toBeVisible();
  await journal.getByRole('button', { name: 'Edit notes', exact: true }).click();
  await notes.fill('Accidental replacement');
  await journal.getByRole('button', { name: 'Discard notes draft', exact: true }).click();
  await expect(journal.getByRole('button', { name: 'Edit notes', exact: true })).toBeFocused();
  await expect(journal.getByText('Remember the quiet moments 🌙', { exact: true })).toBeVisible();
  expect(library.entries.get(id)!.notes).toBe('Remember the quiet moments 🌙');
});

test('saved review and organization remain readable before opening their editors', async ({ page, library }) => {
  Object.assign(library.entries.get(id)!, { review: 'A thoughtful story.\nWorth coming back to.', tags: ['comfort'], customLists: ['Friday queue'] });
  await page.goto('/#library');
  await page.getByRole('button', { name: `Open ${title}`, exact: true }).click();
  const journal = page.getByRole('region', { name: 'Your space', exact: true });
  await expect(journal.getByRole('textbox')).toHaveCount(0);
  await expect(journal.getByText('A thoughtful story.\nWorth coming back to.', { exact: true })).toBeVisible();
  await expect(journal.getByText('comfort', { exact: true })).toBeVisible();
  await expect(journal.getByText('Friday queue', { exact: true })).toBeVisible();
  await journal.getByRole('button', { name: 'Edit review', exact: true }).click();
  await expect(journal.getByRole('textbox', { name: 'Review', exact: true })).toHaveValue('A thoughtful story.\nWorth coming back to.');
  await journal.getByRole('button', { name: 'Edit organization', exact: true }).click();
  await journal.getByRole('textbox', { name: 'Tags', exact: true }).fill('comfort, rewatch');
  await journal.getByRole('button', { name: 'Close organization editor', exact: true }).click();
  await journal.getByRole('button', { name: 'Continue editing organization', exact: true }).click();
  await expect(journal.getByRole('textbox', { name: 'Tags', exact: true })).toHaveValue('comfort, rewatch');
  await journal.getByRole('button', { name: 'Discard organization draft', exact: true }).click();
  await expect(journal.getByRole('button', { name: 'Edit organization', exact: true })).toBeFocused();
  await expect(journal.getByText('rewatch', { exact: true })).toHaveCount(0);
});
