import { createHash, createPublicKey, verify } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { readReleaseVersion } from './check-release-version.mjs';

export const updateAssets = {
  'darwin-aarch64': 'osx-arm64.app.tar.gz',
  'windows-x86_64': 'win-x64.exe',
  'linux-x86_64': 'linux-x64.AppImage',
};

export function compareVersions(left, right) {
  if (![left, right].every(version => /^\d+\.\d+\.\d+$/.test(version))) throw new Error('Invalid release version.');
  const a = left.split('.').map(BigInt), b = right.split('.').map(BigInt);
  for (let i = 0; i < 3; i++) { if (a[i] !== b[i]) return a[i] > b[i] ? 1 : -1; }
  return 0;
}

// Tauri uses Minisign's prehashed Ed25519 format. Verify with Node's crypto primitives;
// clients independently verify both the package and signed version in the official updater.
export function signatureMetadata(publicKey, signature, version) {
  const key = Buffer.from(Buffer.from(publicKey.trim(), 'base64').toString('utf8').trim().split(/\r?\n/)[1] ?? '', 'base64');
  const lines = Buffer.from(signature.trim(), 'base64').toString('utf8').trim().split(/\r?\n/);
  const packet = Buffer.from(lines[1] ?? '', 'base64');
  const global = Buffer.from(lines[3] ?? '', 'base64');
  const comment = lines[2]?.startsWith('trusted comment: ') ? lines[2].slice(17) : '';
  if (key.length !== 42 || packet.length !== 74 || global.length !== 64 || packet.subarray(0, 2).toString() !== 'ED'
    || !key.subarray(2, 10).equals(packet.subarray(2, 10))
    || comment.split('\t').filter(field => field.startsWith('version:')).join('') !== `version:${version}`) {
    throw new Error('Invalid update signature or signed version.');
  }
  const spki = Buffer.concat([Buffer.from('302a300506032b6570032100', 'hex'), key.subarray(10)]);
  const trustedKey = createPublicKey({ key: spki, format: 'der', type: 'spki' });
  const dataSignature = packet.subarray(10);
  if (!verify(null, Buffer.concat([dataSignature, Buffer.from(comment)]), trustedKey, global)) {
    throw new Error('Update signature trusted comment was changed.');
  }
  return { trustedKey, dataSignature };
}

export function verifyUpdateSignature(bytes, publicKey, signature, version) {
  const { trustedKey, dataSignature } = signatureMetadata(publicKey, signature, version);
  if (!verify(null, createHash('blake2b512').update(bytes).digest(), trustedKey, dataSignature)) {
    throw new Error('Update package does not match its signature.');
  }
}

export async function createUpdateManifest(root, assets, tag) {
  const version = await readReleaseVersion(root, tag);
  const config = JSON.parse(await readFile(resolve(root, 'src/TanaHub.Client/src-tauri/tauri.conf.json'), 'utf8'));
  const platforms = {};
  for (const [target, suffix] of Object.entries(updateAssets)) {
    const name = `TanaHub-${version}-${suffix}`;
    const signature = (await readFile(resolve(assets, `${name}.sig`), 'utf8')).trim();
    verifyUpdateSignature(await readFile(resolve(assets, name)), config.plugins.updater.pubkey, signature, version);
    platforms[target] = { url: `https://github.com/Komortes/TanaHub/releases/download/v${version}/${name}`, signature };
  }
  const notes = await readFile(resolve(root, `docs/releases/${version}.md`), 'utf8');
  return { version, notes, pub_date: new Date().toISOString(), platforms };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const root = resolve(import.meta.dirname, '..');
  const assets = resolve(process.argv[2]);
  const manifest = await createUpdateManifest(root, assets, process.argv[3]);
  await writeFile(resolve(assets, 'latest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
  console.log(`Verified signed update manifest for ${manifest.version} on all three platforms.`);
}
