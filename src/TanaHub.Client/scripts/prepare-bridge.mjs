import { execFileSync } from 'node:child_process';
import { chmod, copyFile, mkdir, rename, rm } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const target = execFileSync('rustc', ['--print', 'host-tuple'], { encoding: 'utf8' }).trim();
const runtimes = {
  'aarch64-apple-darwin': 'osx-arm64',
  'x86_64-apple-darwin': 'osx-x64',
  'aarch64-unknown-linux-gnu': 'linux-arm64',
  'x86_64-unknown-linux-gnu': 'linux-x64',
  'aarch64-pc-windows-msvc': 'win-arm64',
  'x86_64-pc-windows-msvc': 'win-x64',
};
const runtime = runtimes[target];
if (!runtime) throw new Error(`No .NET sidecar runtime is configured for ${target}. Build on a supported host.`);
if (process.env.CARGO_BUILD_TARGET && process.env.CARGO_BUILD_TARGET !== target) {
  throw new Error('Cross compilation of the .NET sidecar is not supported. Build on the target platform.');
}

const binaries = join(clientRoot, 'src-tauri', 'binaries');
const publishDir = join(binaries, '.publish', runtime);
await mkdir(publishDir, { recursive: true });
const project = resolve(clientRoot, '..', 'TanaHub.Bridge', 'TanaHub.Bridge.csproj');
console.log(`Publishing TanaHub core for ${runtime}…`);
execFileSync('dotnet', [
  'publish', project, '-c', 'Release', '-r', runtime, '--self-contained', 'true',
  '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
  '-p:EnableCompressionInSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false',
  '-o', publishDir,
], { cwd: resolve(clientRoot, '..', '..'), stdio: 'inherit' });

const extension = process.platform === 'win32' ? '.exe' : '';
const destination = join(binaries, `TanaHub.Bridge-${target}${extension}`);
const staged = `${destination}.pending-${process.pid}`;
try {
  // Replace the file atomically. Overwriting a running macOS executable in place
  // invalidates its mapped code/signature and can stall subsequent launches in dyld.
  await copyFile(join(publishDir, `TanaHub.Bridge${extension}`), staged);
  if (process.platform !== 'win32') await chmod(staged, 0o755);
  await rename(staged, destination);
} finally {
  await rm(staged, { force: true });
}
await rm(publishDir, { recursive: true, force: true });
console.log(`Sidecar ready: ${destination}`);
