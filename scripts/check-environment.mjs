import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const client = JSON.parse(readFileSync(resolve(root, 'src/TanaHub.Client/package.json'), 'utf8'));
const minimumNode = Number(client.engines.node.match(/\d+/)[0]);
const requiredSdk = JSON.parse(readFileSync(resolve(root, 'global.json'), 'utf8')).sdk.version;

export function inspectEnvironment({ nodeVersion = process.versions.node, run = (command, args) => spawnSync(command, args, {
  cwd: root, encoding: 'utf8', windowsHide: true, timeout: 10_000,
}) } = {}) {
  const report = [{ tool: 'Node.js', ok: Number(nodeVersion.split('.')[0]) >= minimumNode,
    detail: nodeVersion, help: `Install Node.js ${minimumNode} or newer: https://nodejs.org/en/download` }];
  const tools = [
    ['.NET SDK', 'dotnet', `Install an SDK compatible with global.json (${requiredSdk}): https://dotnet.microsoft.com/download`],
    ['Rust', 'rustc', 'Install Rust stable: https://rustup.rs'],
    ['Cargo', 'cargo', 'Install Rust with Cargo and restart your terminal: https://rustup.rs'],
  ];
  for (const [tool, command, help] of tools) {
    const result = run(command, ['--version']);
    const detail = result.error?.code === 'ENOENT' ? 'Not installed or not on PATH.'
      : result.error?.code === 'ETIMEDOUT' ? 'Version check timed out.'
        : String(result.stdout || result.stderr || result.error?.message || 'Version check failed.').trim().slice(0, 600);
    report.push({ tool, ok: result.status === 0 && !result.error, detail, help });
  }
  return report;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const report = inspectEnvironment();
  for (const item of report) {
    console.log(`[${item.ok ? 'OK' : 'MISSING'}] ${item.tool}: ${item.detail}`);
    if (!item.ok) console.log(`  ${item.help}`);
  }
  console.log('\nThis checks command-line tools only. Install the native prerequisites for your OS:');
  console.log('https://v2.tauri.app/start/prerequisites/');
  if (report.some(item => !item.ok)) process.exitCode = 1;
  else console.log('\nTool checks passed. Next: npm ci, then npm run desktop:dev (from src/TanaHub.Client).');
}
