import { execFileSync } from 'node:child_process';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';

const client = resolve(import.meta.dirname, '../src/TanaHub.Client');
const bundles = process.argv[2];
if (!['app,dmg', 'nsis', 'deb,appimage'].includes(bundles)) throw new Error('Unsupported installer set.');
const temporary = await mkdtemp(join(tmpdir(), 'tanahub-release-config-'));
try {
  const config = join(temporary, 'signing.json');
  const signing = Boolean(process.env.TAURI_SIGNING_PRIVATE_KEY?.trim());
  await writeFile(config, JSON.stringify({ bundle: { createUpdaterArtifacts: signing } }));
  if (!signing) console.log('No CI signing key: packages must be signed locally before publishing an update manifest.');
  execFileSync(process.execPath, [join(client, 'node_modules/@tauri-apps/cli/tauri.js'), 'build', '--ci', '--bundles', bundles, '--config', config, '--', '--locked'], { cwd: client, stdio: 'inherit' });
} finally { await rm(temporary, { recursive: true, force: true }); }
