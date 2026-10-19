import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export async function readReleaseVersion(root, tag) {
  const read = file => readFile(resolve(root, file), 'utf8');
  const client = 'src/TanaHub.Client';
  const [props, pkg, lock, config, cargo, cargoLock] = await Promise.all([
    read('Directory.Build.props'), read(`${client}/package.json`),
    read(`${client}/package-lock.json`), read(`${client}/src-tauri/tauri.conf.json`),
    read(`${client}/src-tauri/Cargo.toml`), read(`${client}/src-tauri/Cargo.lock`),
  ]);
  const npmLock = JSON.parse(lock);
  const versions = {
    '.NET': props.match(/<VersionPrefix>([^<]+)<\/VersionPrefix>/)?.[1],
    npm: JSON.parse(pkg).version,
    'npm lockfile': npmLock.version,
    'npm root package': npmLock.packages?.['']?.version,
    Tauri: JSON.parse(config).version,
    Cargo: cargo.match(/\[package\][\s\S]*?\nversion\s*=\s*"([^"]+)"/)?.[1],
    'Cargo lockfile': cargoLock.match(/\[\[package\]\]\nname = "tanahub-client"\nversion = "([^"]+)"/)?.[1],
  };
  const version = versions['.NET'];
  if (!version || !/^\d+\.\d+\.\d+$/.test(version)) {
    throw new Error('VersionPrefix must contain a numeric major.minor.patch version.');
  }
  for (const [manifest, value] of Object.entries(versions)) {
    if (value !== version) throw new Error(`${manifest} version ${value ?? '(missing)'} differs from .NET version ${version}.`);
  }
  if (tag !== undefined && tag !== `v${version}`) {
    throw new Error(`Release tag ${tag} must match v${version}.`);
  }
  return version;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const version = await readReleaseVersion(resolve(import.meta.dirname, '..'), process.argv[2] || undefined);
  console.log(`Release version verified: ${version}`);
}
