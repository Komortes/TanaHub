import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readFile, readdir, stat, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { createUpdateManifest, updateAssets } from './update-manifest.mjs';
import { readReleaseVersion } from './check-release-version.mjs';

const root = resolve(import.meta.dirname, '..');
const assets = resolve(process.argv[2]);
const key = resolve(process.argv[3]);
const version = await readReleaseVersion(root, process.argv[4]);
if (process.platform !== 'win32' && ((await stat(key)).mode & 0o077)) throw new Error('The signing key must have owner-only permissions (chmod 600).');
const cli = join(root, 'src/TanaHub.Client/node_modules/@tauri-apps/cli/tauri.js');
for (const suffix of Object.values(updateAssets)) {
  const asset = join(assets, `TanaHub-${version}-${suffix}`);
  execFileSync(process.execPath, [cli, 'signer', 'sign', '--private-key-path', key, '--app-version', version, asset], {
    stdio: 'ignore', env: { ...process.env, TAURI_SIGNING_PRIVATE_KEY_PASSWORD: process.env.TAURI_SIGNING_PRIVATE_KEY_PASSWORD ?? '' },
  });
}
const manifest = await createUpdateManifest(root, assets, `v${version}`);
await writeFile(join(assets, 'latest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
for (const platform of ['osx-arm64', 'win-x64', 'linux-x64']) {
  const stem = `TanaHub-${version}-${platform}`;
  const names = (await readdir(assets)).filter(name => name.startsWith(stem) && !name.endsWith('-SHA256SUMS.txt')).sort();
  const hashes = [];
  for (const name of names) hashes.push(`${createHash('sha256').update(await readFile(join(assets, name))).digest('hex')}  ${name}`);
  await writeFile(join(assets, `${stem}-SHA256SUMS.txt`), `${hashes.join('\n')}\n`);
}
console.log(`Signed and independently verified all three update packages for ${version}. Private key stayed local.`);
