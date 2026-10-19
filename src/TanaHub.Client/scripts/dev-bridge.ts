import { execFileSync, spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';
import type { IncomingMessage, ServerResponse } from 'node:http';
import type { Plugin } from 'vite';

interface RpcResponse {
  id: string;
  result?: unknown;
  error?: { code?: string; message: string };
}

interface PendingRequest {
  id: string;
  resolve: (response: RpcResponse) => void;
  reject: (error: Error) => void;
}

export class BridgeRpc {
  private child: ChildProcessWithoutNullStreams | undefined;
  private pending: PendingRequest | undefined;
  private buffer = '';
  private queue = Promise.resolve();
  private queued = 0;
  private closed = false;
  private binary: string;
  private timeoutMs: number;
  private args: string[];

  constructor(binary: string, timeoutMs = 45_000, args: string[] = []) {
    this.binary = binary;
    this.timeoutMs = timeoutMs;
    this.args = args;
  }

  request(method: string, params: Record<string, unknown>): Promise<unknown> {
    if (this.closed) return Promise.reject(new Error('TanaHub development bridge is closed.'));
    if (this.queued >= 32) return Promise.reject(new Error('TanaHub core has too many pending requests.'));
    this.queued += 1;
    const request = this.queue.then(() => this.performRequest(method, params));
    this.queue = request.then(() => undefined, () => undefined);
    return request.finally(() => { this.queued -= 1; });
  }

  private start(): ChildProcessWithoutNullStreams {
    if (this.child) return this.child;
    if (!existsSync(this.binary)) {
      throw new Error('TanaHub core is missing. Run npm run prepare:bridge, then restart the development server.');
    }
    const child = spawn(this.binary, this.args, { stdio: 'pipe', windowsHide: true });
    this.child = child;
    this.buffer = '';
    child.stdout.setEncoding('utf8');
    child.stdout.on('data', (chunk: string) => {
      if (this.child !== child) return;
      this.buffer += chunk;
      if (Buffer.byteLength(this.buffer, 'utf8') > 8 * 1024 * 1024) {
        this.fail(new Error('TanaHub core response exceeded the supported limit.'));
        return;
      }
      let newline: number;
      while ((newline = this.buffer.indexOf('\n')) >= 0) {
        const line = this.buffer.slice(0, newline);
        this.buffer = this.buffer.slice(newline + 1);
        try {
          const response = JSON.parse(line) as RpcResponse;
          if (!this.pending || response.id !== this.pending.id) {
            this.fail(new Error('TanaHub core response did not match its request.'));
            return;
          }
          this.pending.resolve(response);
          this.pending = undefined;
        } catch {
          this.fail(new Error('TanaHub core returned an invalid response.'));
          return;
        }
      }
    });
    child.stderr.on('data', (chunk: Buffer) => { console.error(`[TanaHub core] ${chunk.toString().trim()}`); });
    child.stdin.on('error', (error) => {
      if (this.child === child) this.fail(new Error(`Cannot send the request to TanaHub core: ${error.message}`));
    });
    child.once('error', (error) => {
      if (this.child === child) this.fail(new Error(`TanaHub core could not start: ${error.message}`));
    });
    child.once('exit', () => {
      if (this.child === child) this.fail(new Error('TanaHub core stopped unexpectedly. Try the action again.'));
    });
    return child;
  }

  private async performRequest(method: string, params: Record<string, unknown>): Promise<unknown> {
    if (this.closed) throw new Error('TanaHub development bridge is closed.');
    const child = this.start();
    const id = randomUUID();
    const timeout = method === 'anilist.connect' ? 360000 : method === 'anilist.sync' ? 300000 : method === 'recognition.run' ? 120000 : this.timeoutMs;
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      const response = await new Promise<RpcResponse>((resolve, reject) => {
        this.pending = { id, resolve, reject };
        timer = setTimeout(() => {
          this.fail(new Error(`TanaHub core did not respond within ${timeout / 1000} seconds. Try the action again.`));
        }, timeout);
        child.stdin.write(`${JSON.stringify({ id, method, params })}\n`);
      });
      if (response.error) throw new Error(response.error.message);
      return response.result ?? null;
    } finally {
      if (timer) clearTimeout(timer);
    }
  }

  private fail(error: Error): void {
    const child = this.child;
    this.child = undefined;
    this.buffer = '';
    const pending = this.pending;
    this.pending = undefined;
    pending?.reject(error);
    child?.stdin.end();
    child?.kill();
  }

  close(): void {
    this.closed = true;
    this.fail(new Error('TanaHub development bridge is closed.'));
  }
}

export async function readRequest(request: IncomingMessage): Promise<{ method: string; params: Record<string, unknown> }> {
  let body = '';
  request.setEncoding('utf8');
  for await (const chunk of request) {
    body += chunk.toString();
    if (Buffer.byteLength(body, 'utf8') > 20 * 1024 * 1024) throw new Error('TanaHub request is too large.');
  }
  const parsed = JSON.parse(body) as { method?: unknown; params?: unknown };
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('Invalid TanaHub request.');
  if (typeof parsed.method !== 'string' || !/^[a-zA-Z][a-zA-Z0-9.]{0,99}$/.test(parsed.method)) {
    throw new Error('Invalid TanaHub request method.');
  }
  if (parsed.params !== undefined && (parsed.params === null || typeof parsed.params !== 'object' || Array.isArray(parsed.params))) {
    throw new Error('Invalid TanaHub request parameters.');
  }
  return { method: parsed.method, params: (parsed.params ?? {}) as Record<string, unknown> };
}

function send(response: ServerResponse, status: number, data: unknown): void {
  response.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  response.end(JSON.stringify(data));
}

export function bridgeDevPlugin(): Plugin {
  return {
    name: 'tanahub-development-bridge',
    apply: 'serve',
    configureServer(server) {
      const expectedHost = `127.0.0.1:${server.config.server.port ?? 1420}`;
      const expectedOrigin = `http://${expectedHost}`;
      const target = execFileSync('rustc', ['--print', 'host-tuple'], { encoding: 'utf8' }).trim();
      const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
      const extension = process.platform === 'win32' ? '.exe' : '';
      const bridge = new BridgeRpc(join(clientRoot, 'src-tauri', 'binaries', `TanaHub.Bridge-${target}${extension}`));
      const authBridge = new BridgeRpc(join(clientRoot, 'src-tauri', 'binaries', `TanaHub.Bridge-${target}${extension}`));
      const close = () => { bridge.close(); authBridge.close(); };
      process.once('exit', close);
      server.httpServer?.once('close', () => {
        close();
        process.off('exit', close);
      });
      server.middlewares.use('/api/rpc', (request, response) => {
        const origin = request.headers.origin;
        if (request.headers.host !== expectedHost || (origin && origin !== expectedOrigin)) {
          send(response, 403, { error: { message: 'The development bridge accepts local same-origin requests only.' } });
          return;
        }
        if (request.method !== 'POST' || !request.headers['content-type']?.startsWith('application/json')) {
          send(response, 405, { error: { message: 'Use a JSON POST request.' } });
          return;
        }
        void readRequest(request)
          .then(({ method, params }) => (method === 'anilist.connect' ? authBridge : bridge).request(method, params))
          .then((result) => send(response, 200, { result }))
          .catch((error: unknown) => {
            const message = error instanceof Error ? error.message : 'TanaHub core request failed.';
            send(response, 500, { error: { message } });
          });
      });
    },
  };
}
