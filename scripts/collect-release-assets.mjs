import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFile, mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { basename, join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { readReleaseVersion } from './check-release-version.mjs';

const installers = {
  'osx-arm64': [['dmg', '.dmg']],
  'win-x64': [['nsis', '.exe']],
  'linux-x64': [['deb', '.deb'], ['appimage', '.AppImage']],
};

export async function collectReleaseAssets(root, platform, bundle, output) {
  const required = installers[platform];
  if (!required) throw new Error(`Unsupported release platform: ${platform}`);
  const version = await readReleaseVersion(root);
  const stem = `TanaHub-${version}-${platform}`;
  const selected = [];
  for (const [folder, extension] of required) {
    const files = await readdir(join(bundle, folder), { withFileTypes: true }).catch(error => {
      if (error.code === 'ENOENT') return [];
      throw error;
    });
    const matches = files.filter(file => file.isFile() && file.name.endsWith(extension));
    if (matches.length !== 1) throw new Error(`Expected one ${extension} installer in ${folder}, found ${matches.length}.`);
    selected.push([join(bundle, folder, matches[0].name), `${stem}${extension}`]);
  }
  await mkdir(output, { recursive: true });
  const assets = [];
  for (const [source, name] of selected) {
    const destination = join(output, name);
    await copyFile(source, destination);
    assets.push(destination);
  }
  if (platform === 'osx-arm64') {
    const archive = join(output, `${stem}.app.tar.gz`);
    // tar retains bundle symlinks and executable permissions across artifact uploads.
    execFileSync('tar', ['-czf', archive, '-C', join(bundle, 'macos'), 'TanaHub.app'], { stdio: 'inherit' });
    assets.push(archive);
  }
  const hashes = await Promise.all(assets.map(async file => {
    const hash = createHash('sha256').update(await readFile(file)).digest('hex');
    return `${hash}  ${basename(file)}`;
  }));
  const checksums = join(output, `${stem}-SHA256SUMS.txt`);
  await writeFile(checksums, `${hashes.join('\n')}\n`);
  return [...assets, checksums];
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const root = resolve(import.meta.dirname, '..');
  const bundle = resolve(process.argv[3] ?? join(root, 'src/TanaHub.Client/src-tauri/target/release/bundle'));
  const output = resolve(process.argv[4] ?? join(root, 'artifacts/release', process.argv[2] ?? 'unknown'));
  const assets = await collectReleaseAssets(root, process.argv[2], bundle, output);
  console.log(assets.join('\n'));
}
