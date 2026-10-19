import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { Readable } from 'node:stream';
import { BridgeRpc, readRequest } from './dev-bridge.ts';

const temporary = await mkdtemp(join(tmpdir(), 'tanahub-transport-'));
const fixture = join(temporary, 'bridge-fixture.mjs');
await writeFile(fixture, `
import { createInterface } from 'node:readline';
for await (const line of createInterface({ input: process.stdin })) {
  const { id, method, params } = JSON.parse(line);
  if (method === 'exit') process.exit(1);
  if (method === 'timeout') { await new Promise(resolve => setTimeout(resolve, 10000)); continue; }
  if (method === 'mismatch') { console.log(JSON.stringify({ id: 'wrong', result: null })); continue; }
  if (method === 'invalid') { console.log('not json'); continue; }
  if (method === 'error') { console.log(JSON.stringify({ id, error: { code: 'validation', message: 'Invalid score' } })); continue; }
  if (params.delay) await new Promise(resolve => setTimeout(resolve, params.delay));
  console.log(JSON.stringify({ id, result: params }));
}
`);

const bridge = new BridgeRpc(process.execPath, 250, [fixture]);
try {
  const unicode = Buffer.from(JSON.stringify({ method: 'library.notes', params: { notes: 'Продолжить вечером 日本語' } }));
  const splitChunks = Readable.from(Array.from(unicode, byte => Buffer.from([byte])));
  const decoded = await readRequest(splitChunks);
  assert.equal(decoded.params.notes, 'Продолжить вечером 日本語');
  await assert.rejects(readRequest(Readable.from(['null'])), /Invalid TanaHub request/);
  const imageRequest = JSON.stringify({ method: 'recognition.run', params: { imageBase64: 'a'.repeat(1024 * 1024) } });
  assert.equal((await readRequest(Readable.from([imageRequest]))).params.imageBase64.length, 1024 * 1024);
  await assert.rejects(readRequest(Readable.from(['a'.repeat(20 * 1024 * 1024 + 1)])), /too large/);
  const results = await Promise.all([
    bridge.request('echo', { value: 1, delay: 60 }),
    bridge.request('echo', { value: 2 }),
    bridge.request('echo', { value: 3 }),
  ]);
  assert.deepEqual(results.map(result => result.value), [1, 2, 3]);
  await assert.rejects(bridge.request('error', {}), /Invalid score/);
  assert.deepEqual(await bridge.request('echo', { after: 'backend error' }), { after: 'backend error' });
  await assert.rejects(bridge.request('exit', {}), /stopped unexpectedly/);
  assert.deepEqual(await bridge.request('echo', { after: 'restart' }), { after: 'restart' });
  await assert.rejects(bridge.request('timeout', {}), /did not respond/);
  assert.deepEqual(await bridge.request('echo', { after: 'timeout' }), { after: 'timeout' });
  await assert.rejects(bridge.request('mismatch', {}), /did not match/);
  await assert.rejects(bridge.request('invalid', {}), /invalid response/);
  bridge.close();
  await assert.rejects(bridge.request('echo', {}), /closed/);
  const missing = new BridgeRpc(join(temporary, 'missing'));
  await assert.rejects(missing.request('echo', {}), /prepare:bridge/);
  missing.close();
  console.log('Bridge transport passed: UTF-8 HTTP chunks, serialized responses, backend errors, EOF recovery, timeout recovery, invalid response handling, cleanup, missing binary.');
} finally {
  bridge.close();
  await rm(temporary, { recursive: true, force: true });
}
