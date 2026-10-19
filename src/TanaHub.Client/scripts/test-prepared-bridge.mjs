import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { BridgeRpc } from './dev-bridge.ts';

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const target = execFileSync('rustc', ['--print', 'host-tuple'], { encoding: 'utf8' }).trim();
const extension = process.platform === 'win32' ? '.exe' : '';
const binary = process.env.TANAHUB_TEST_BRIDGE_BINARY ?? join(clientRoot, 'src-tauri', 'binaries', `TanaHub.Bridge-${target}${extension}`);
const temporary = await mkdtemp(join(tmpdir(), 'tanahub-prepared-bridge-'));
const previousData = process.env.TANAHUB_DATA_DIRECTORY;
const previousExtraction = process.env.DOTNET_BUNDLE_EXTRACT_BASE_DIR;
process.env.TANAHUB_DATA_DIRECTORY = join(temporary, 'data');
process.env.DOTNET_BUNDLE_EXTRACT_BASE_DIR = join(temporary, 'runtime');
const bridge = new BridgeRpc(binary);
let restarted;
try {
  const empty = await bridge.request('library.list', {});
  assert.equal(empty.items.length, 0);
  const added = await bridge.request('library.add', { mediaId: 'anilist:154587', status: 'Watching' });
  assert.equal(added.media.title, "Frieren: Beyond Journey's End");
  await Promise.all([
    bridge.request('library.progress', { mediaId: added.mediaId, amount: 4 }),
    bridge.request('library.score', { mediaId: added.mediaId, score: 9 }),
    bridge.request('library.notes', { mediaId: added.mediaId, notes: 'Продолжить вечером' }),
  ]);
  await assert.rejects(bridge.request('library.score', { mediaId: added.mediaId, score: 11 }), /score|оцен|0.*10/i);
  bridge.close();
  restarted = new BridgeRpc(binary);
  const saved = await restarted.request('library.get', { mediaId: added.mediaId });
  assert.equal(saved.progress, 4);
  assert.equal(saved.score, 9);
  assert.equal(saved.notes, 'Продолжить вечером');
  assert.equal(saved.status, 'Watching');
  await restarted.request('library.progress.set', { mediaId: added.mediaId, progress: 2147483647 });
  await assert.rejects(restarted.request('library.progress', { mediaId: added.mediaId, amount: 1 }), /range|progress/i);
  const summary = await restarted.request('home.get', {});
  assert.equal(summary.insights.episodesWatched, 2147483647);
  assert.ok(summary.insights.estimatedWatchMinutes > 2147483647);
  await restarted.request('library.dates', { mediaId: added.mediaId, startedAt: '2026-10-01T07:00:00Z', completedAt: '2026-10-11T07:00:00Z' });
  await restarted.request('library.review', { mediaId: added.mediaId, review: 'a'.repeat(4000) });
  await assert.rejects(restarted.request('library.review', { mediaId: added.mediaId, review: 'a'.repeat(4001) }), /4,000/i);
  const boundaries = await restarted.request('library.get', { mediaId: added.mediaId });
  assert.equal(boundaries.progress, 2147483647);
  assert.equal(boundaries.review.length, 4000);
  assert.equal(Date.parse(boundaries.startedAt), Date.parse('2026-10-01T07:00:00Z'));
  assert.equal(boundaries.score, 9);
  assert.equal(boundaries.status, 'Watching');
  await restarted.request('library.status', { mediaId: added.mediaId, status: 'Completed' });
  const completed = await restarted.request('library.get', { mediaId: added.mediaId });
  assert.equal(completed.status, 'Completed');
  assert.equal(Date.parse(completed.completedAt), Date.parse('2026-10-11T07:00:00Z'));
  assert.equal(completed.progress, 2147483647);
} finally {
  bridge.close();
  restarted?.close();
  if (previousData === undefined) delete process.env.TANAHUB_DATA_DIRECTORY;
  else process.env.TANAHUB_DATA_DIRECTORY = previousData;
  if (previousExtraction === undefined) delete process.env.DOTNET_BUNDLE_EXTRACT_BASE_DIR;
  else process.env.DOTNET_BUNDLE_EXTRACT_BASE_DIR = previousExtraction;
  // Windows keeps extracted SQLite DLLs locked briefly while the killed cores exit.
  await rm(temporary, { recursive: true, force: true, maxRetries: 10, retryDelay: 100 });
}
console.log('Prepared .NET sidecar passed: native runtime/SQLite load, real RPC mutations, validation, persistence after restart and cleanup. User data was isolated.');
