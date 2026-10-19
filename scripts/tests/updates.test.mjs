import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtemp, readFile, writeFile, rm, mkdir, copyFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { test } from 'node:test';
import { signatureMetadata, verifyUpdateSignature, createUpdateManifest, compareVersions, updateAssets } from '../update-manifest.mjs';
import { readReleaseVersion } from '../check-release-version.mjs';

test('actual Tauri signatures bind the package, trusted comment and announced version', async () => {
  const temporary = await mkdtemp(join(tmpdir(), 'tanahub-update-signature-'));
  const cli = resolve(import.meta.dirname, '../../src/TanaHub.Client/node_modules/@tauri-apps/cli/tauri.js');
  const run = args => execFileSync(process.execPath, [cli, 'signer', ...args], { stdio: 'ignore' });
  try {
    const key = join(temporary, 'test.key');
    const asset = join(temporary, 'update.bin');
    const data = Buffer.from('Isolated signed update fixture');
    await writeFile(asset, data);
    run(['generate', '--ci', '--write-keys', key]);
    run(['sign', '--private-key-path', key, '--password', '', '--app-version', '1.2.3', asset]);
    const publicKey = await readFile(`${key}.pub`, 'utf8');
    const signature = await readFile(`${asset}.sig`, 'utf8');
    verifyUpdateSignature(data, publicKey, signature, '1.2.3');
    assert.throws(() => verifyUpdateSignature(Buffer.from('tampered package'), publicKey, signature, '1.2.3'), /package/i);
    assert.throws(() => verifyUpdateSignature(data, publicKey, signature, '1.2.4'), /version/i);
    assert.throws(() => signatureMetadata(publicKey, 'not a signature', '1.2.3'), /signature/i);
    const changed = Buffer.from(Buffer.from(signature.trim(), 'base64').toString().replace('version:1.2.3', 'version:9.9.9')).toString('base64');
    assert.throws(() => signatureMetadata(publicKey, changed, '9.9.9'), /comment/i);
  } finally { await rm(temporary, { recursive: true, force: true }); }
});

test('update channels compare numeric versions and never mistake 1.9 for newer than 1.10', () => {
  assert.equal(compareVersions('1.10.0', '1.9.9'), 1);
  assert.equal(compareVersions('0.1.1', '0.1.2'), -1);
  assert.equal(compareVersions('0.1.2', '0.1.2'), 0);
  assert.throws(() => compareVersions('v0.1.2', '0.1.2'), /version/i);
});

test('update metadata requires three complete, correctly signed platform packages', async () => {
  const root = resolve(import.meta.dirname, '../..');
  const temporary = await mkdtemp(join(tmpdir(), 'tanahub-update-manifest-'));
  const version = await readReleaseVersion(root);
  const cli = join(root, 'src/TanaHub.Client/node_modules/@tauri-apps/cli/tauri.js');
  const run = args => execFileSync(process.execPath, [cli, 'signer', ...args], { stdio: 'ignore' });
  try {
    for (const file of ['Directory.Build.props', 'src/TanaHub.Client/package.json', 'src/TanaHub.Client/package-lock.json', 'src/TanaHub.Client/src-tauri/tauri.conf.json', 'src/TanaHub.Client/src-tauri/Cargo.toml', 'src/TanaHub.Client/src-tauri/Cargo.lock', `docs/releases/${version}.md`]) {
      await mkdir(resolve(temporary, file, '..'), { recursive: true });
      await copyFile(join(root, file), join(temporary, file));
    }
    const key = join(temporary, 'test.key');
    run(['generate', '--ci', '--write-keys', key]);
    const configPath = join(temporary, 'src/TanaHub.Client/src-tauri/tauri.conf.json');
    const config = JSON.parse(await readFile(configPath, 'utf8'));
    config.plugins.updater.pubkey = (await readFile(`${key}.pub`, 'utf8')).trim();
    await writeFile(configPath, JSON.stringify(config));
    for (const suffix of Object.values(updateAssets)) {
      const asset = join(temporary, `TanaHub-${version}-${suffix}`);
      await writeFile(asset, 'signed package fixture');
      run(['sign', '--private-key-path', key, '--password', '', '--app-version', version, asset]);
    }
    const manifest = await createUpdateManifest(temporary, temporary, `v${version}`);
    assert.deepEqual(Object.keys(manifest.platforms).sort(), Object.keys(updateAssets).sort());
    assert.equal(manifest.version, version);
    assert.ok(manifest.platforms['darwin-aarch64'].url.endsWith(`v${version}/TanaHub-${version}-osx-arm64.app.tar.gz`));
    await writeFile(join(temporary, `TanaHub-${version}-win-x64.exe`), 'tampered');
    await assert.rejects(createUpdateManifest(temporary, temporary, `v${version}`), /package/i);
    await rm(join(temporary, `TanaHub-${version}-win-x64.exe.sig`));
    await assert.rejects(createUpdateManifest(temporary, temporary, `v${version}`), /ENOENT/);
  } finally { await rm(temporary, { recursive: true, force: true }); }
});
