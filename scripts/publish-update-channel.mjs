import { execFileSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { compareVersions, signatureMetadata, updateAssets } from './update-manifest.mjs';

const repository = 'Komortes/TanaHub';
const tag = process.argv[2];
if (!/^v\d+\.\d+\.\d+$/.test(tag ?? '')) throw new Error('Pass a numeric release tag.');
function api(path, method = 'GET', body) {
  const args = ['api', `repos/${repository}/${path}`, '--method', method];
  if (body) args.push('--input', '-');
  return JSON.parse(execFileSync('gh', args, { encoding: 'utf8', input: body ? JSON.stringify(body) : undefined, stdio: ['pipe', 'pipe', 'pipe'] }));
}
function optional(path) {
  try { return api(path); }
  catch (error) { if (String(error.stderr).includes('HTTP 404')) return null; throw error; }
}
const release = api(`releases/tags/${tag}`);
if (release.draft) throw new Error('A draft must never become an update channel.');
const manifestAsset = release.assets.find(asset => asset.name === 'latest.json');
if (!manifestAsset) {
  console.log(`${tag} has no signed update manifest; leaving update channels unchanged.`);
  process.exit(0);
}
if (manifestAsset.size > 128 * 1024) throw new Error('Update metadata is too large.');
const response = await fetch(manifestAsset.browser_download_url);
if (!response.ok) throw new Error('Could not download the published update manifest.');
const manifest = await response.json();
if (manifest.version !== tag.slice(1)) throw new Error('Update manifest version differs from the published tag.');
const config = JSON.parse(await readFile(resolve(import.meta.dirname, '../src/TanaHub.Client/src-tauri/tauri.conf.json'), 'utf8'));
for (const [target, suffix] of Object.entries(updateAssets)) {
  const name = `TanaHub-${manifest.version}-${suffix}`;
  const platform = manifest.platforms?.[target];
  const expected = `https://github.com/${repository}/releases/download/${tag}/${name}`;
  if (platform?.url !== expected || !release.assets.some(asset => asset.name === name && asset.size > 0)) {
    throw new Error(`Missing published update package for ${target}.`);
  }
  signatureMetadata(config.plugins.updater.pubkey, platform.signature, manifest.version);
}
manifest.pub_date = release.published_at;
if (!optional('git/ref/heads/updates')) {
  const commit = execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
  api('git/refs', 'POST', { ref: 'refs/heads/updates', sha: commit });
}
for (const channel of release.prerelease ? ['preview'] : ['stable', 'preview']) {
  const path = `contents/${channel}.json?ref=updates`;
  const previous = optional(path);
  if (previous) {
    const old = JSON.parse(Buffer.from(previous.content, 'base64').toString('utf8'));
    if (compareVersions(old.version, manifest.version) >= 0) {
      console.log(`${channel} already points to ${old.version}; avoiding a downgrade.`);
      continue;
    }
  }
  api(`contents/${channel}.json`, 'PUT', {
    message: `release: advance ${channel} updates to ${tag}`, branch: 'updates',
    content: Buffer.from(`${JSON.stringify(manifest, null, 2)}\n`).toString('base64'),
    ...(previous ? { sha: previous.sha } : {}),
  });
  console.log(`${channel} update channel now points to ${tag}.`);
}
