import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile, rm, copyFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, join, resolve } from 'node:path';
import { test } from 'node:test';
import { createHash } from 'node:crypto';
import { readReleaseVersion } from '../check-release-version.mjs';
import { collectReleaseAssets } from '../collect-release-assets.mjs';

const root = resolve(import.meta.dirname, '../..');
const manifests = [
  'Directory.Build.props', 'src/TanaHub.Client/package.json',
  'src/TanaHub.Client/package-lock.json', 'src/TanaHub.Client/src-tauri/tauri.conf.json',
  'src/TanaHub.Client/src-tauri/Cargo.toml', 'src/TanaHub.Client/src-tauri/Cargo.lock',
];

async function fixture(run) {
  const temporary = await mkdtemp(join(tmpdir(), 'tanahub-release-test-'));
  try {
    for (const file of manifests) {
      const destination = join(temporary, file);
      await mkdir(resolve(destination, '..'), { recursive: true });
      await copyFile(join(root, file), destination);
    }
    await run(temporary);
  } finally {
    await rm(temporary, { recursive: true, force: true });
  }
}

test('release rejects a different tag and accepts the matching version', async () => {
  const version = await readReleaseVersion(root);
  assert.equal(await readReleaseVersion(root, `v${version}`), version);
  await assert.rejects(readReleaseVersion(root, 'v99.0.0'), /tag/i);
  await assert.rejects(readReleaseVersion(root, '../0.1.0'), /tag/i);
});

test('release detects version drift in each manifest and lockfile', async () => {
  for (const file of manifests) {
    await fixture(async temporary => {
      const destination = join(temporary, file);
      const content = await readFile(destination, 'utf8');
      const version = await readReleaseVersion(root);
      const changed = file.endsWith('Cargo.lock')
        ? content.replace(`name = "tanahub-client"\nversion = "${version}"`, 'name = "tanahub-client"\nversion = "99.0.0"')
        : content.replace(version, '99.0.0');
      assert.notEqual(changed, content);
      await writeFile(destination, changed);
      await assert.rejects(readReleaseVersion(temporary), /version/i);
    });
  }
});

test('release rejects a stale npm root package version', async () => {
  await fixture(async temporary => {
    const destination = join(temporary, 'src/TanaHub.Client/package-lock.json');
    const lock = JSON.parse(await readFile(destination, 'utf8'));
    lock.packages[''].version = '99.0.0';
    await writeFile(destination, JSON.stringify(lock));
    await assert.rejects(readReleaseVersion(temporary), /version/i);
  });
});

test('packaging requires every configured installer and produces verifiable hashes', async () => {
  await fixture(async temporary => {
    const bundle = join(temporary, 'bundle');
    const output = join(temporary, 'assets');
    await mkdir(join(bundle, 'deb'), { recursive: true });
    await mkdir(join(bundle, 'appimage'), { recursive: true });
    await writeFile(join(bundle, 'deb', 'installer.deb'), 'debian fixture');
    await assert.rejects(collectReleaseAssets(temporary, 'linux-x64', bundle, output), /AppImage/i);
    await writeFile(join(bundle, 'appimage', 'installer.AppImage'), 'appimage fixture');
    const files = await collectReleaseAssets(temporary, 'linux-x64', bundle, output);
    const checksums = await readFile(files.at(-1), 'utf8');
    for (const file of files.slice(0, -1)) {
      const hash = createHash('sha256').update(await readFile(file)).digest('hex');
      assert.ok(checksums.includes(`${hash}  ${basename(file)}`));
    }
    await assert.rejects(collectReleaseAssets(temporary, '../linux-x64', bundle, output), /platform/i);
  });
});

test('Windows asset collection uses only the NSIS installer', async () => {
  await fixture(async temporary => {
    const bundle = join(temporary, 'bundle');
    await mkdir(join(bundle, 'nsis'), { recursive: true });
    await writeFile(join(bundle, 'nsis', 'installer.exe'), 'windows fixture');
    const files = await collectReleaseAssets(temporary, 'win-x64', bundle, join(temporary, 'assets'));
    assert.equal(files.length, 2);
    assert.ok(files[0].endsWith('-win-x64.exe'));
    await writeFile(join(bundle, 'nsis', 'duplicate.exe'), 'duplicate fixture');
    await assert.rejects(collectReleaseAssets(temporary, 'win-x64', bundle, join(temporary, 'assets')), /found 2/);
  });
});

test('signed macOS updater archives are copied without regenerating their bytes', async () => {
  await fixture(async temporary => {
    const bundle = join(temporary, 'bundle');
    await mkdir(join(bundle, 'dmg'), { recursive: true });
    await mkdir(join(bundle, 'macos'), { recursive: true });
    await writeFile(join(bundle, 'dmg', 'installer.dmg'), 'dmg fixture');
    await writeFile(join(bundle, 'macos', 'TanaHub.app.tar.gz'), 'exact signed archive bytes');
    await writeFile(join(bundle, 'macos', 'TanaHub.app.tar.gz.sig'), 'signature fixture');
    const files = await collectReleaseAssets(temporary, 'osx-arm64', bundle, join(temporary, 'assets'));
    const archive = files.find(file => file.endsWith('.app.tar.gz'));
    assert.equal(await readFile(archive, 'utf8'), 'exact signed archive bytes');
    assert.equal(await readFile(`${archive}.sig`, 'utf8'), 'signature fixture');
    assert.ok((await readFile(files.at(-1), 'utf8')).includes(`${basename(archive)}.sig`));
  });
});
