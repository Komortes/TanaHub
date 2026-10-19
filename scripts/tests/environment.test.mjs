import assert from 'node:assert/strict';
import { test } from 'node:test';
import { inspectEnvironment } from '../check-environment.mjs';

const installed = command => ({ status: 0, stdout: command === 'dotnet' ? '10.0.201\n' : `${command} 1.93.1\n`, stderr: '' });

test('environment check accepts supported Node and working build tools', () => {
  const report = inspectEnvironment({ nodeVersion: '24.0.0', run: installed });
  assert.ok(report.every(item => item.ok));
});

test('old Node and missing Rust get actionable installation guidance', () => {
  const report = inspectEnvironment({ nodeVersion: '22.0.0', run: command => command === 'rustc'
    ? { status: null, error: { code: 'ENOENT' } } : installed(command) });
  assert.equal(report[0].ok, false);
  assert.match(report[0].help, /24/);
  const rust = report.find(item => item.tool === 'Rust');
  assert.equal(rust.ok, false);
  assert.match(rust.help, /rustup.rs/);
});

test('SDK selection failures retain the useful message and point to global.json', () => {
  const report = inspectEnvironment({ nodeVersion: '26.0.0', run: command => command === 'dotnet'
    ? { status: 1, stdout: '', stderr: 'Requested SDK version: 10.0.201\nA compatible SDK was not found.' } : installed(command) });
  const dotnet = report.find(item => item.tool === '.NET SDK');
  assert.equal(dotnet.ok, false);
  assert.match(dotnet.detail, /compatible SDK/);
  assert.match(dotnet.help, /global.json/);
});

test('a stalled version check reports a timeout rather than success', () => {
  const report = inspectEnvironment({ nodeVersion: '24.0.0', run: command => command === 'cargo'
    ? { status: null, error: { code: 'ETIMEDOUT' } } : installed(command) });
  const cargo = report.find(item => item.tool === 'Cargo');
  assert.equal(cargo.ok, false);
  assert.match(cargo.detail, /timed out/i);
});
