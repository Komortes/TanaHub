import type { Update } from '@tauri-apps/plugin-updater';
import { isDesktop, openExternal } from './native';

type Phase = 'idle' | 'checking' | 'available' | 'downloading' | 'ready' | 'installing' | 'installed';
async function invoke<T>(command: string): Promise<T> { return (await import('@tauri-apps/api/core')).invoke<T>(command); }

class DesktopUpdates {
  phase = $state<Phase>('idle');
  update = $state<Update | null>(null);
  message = $state('');
  error = $state('');
  downloaded = $state(0);
  total = $state<number | undefined>();
  private disposed = false;
  get working() { return ['checking', 'downloading', 'installing'].includes(this.phase); }
  get percent() { return this.total ? Math.min(100, Math.round(this.downloaded / this.total * 100)) : undefined; }

  private async release() { const previous = this.update; this.update = null; await previous?.close().catch(() => {}); }
  dispose() { this.disposed = true; if (!this.working) void this.release(); }

  async check() {
    if (!isDesktop() || this.working || this.phase === 'installed') return;
    this.phase = 'checking'; this.error = ''; this.message = '';
    try {
      await this.release();
      const support = await invoke<{ supported: boolean; reason: string | null }>('update_support');
      if (!support.supported) { this.message = support.reason ?? 'Download an installer from Browse releases.'; this.phase = 'idle'; return; }
      this.update = await (await import('@tauri-apps/plugin-updater')).check({ timeout: 20_000 });
      this.message = this.update ? `Version ${this.update.version} is available` : 'You are up to date';
      this.phase = this.update ? 'available' : 'idle';
    } catch (reason) { this.error = `Could not check for updates. ${String(reason)}`; this.phase = 'idle'; }
    finally { if (this.disposed) await this.release(); }
  }
  async download() {
    if (!this.update || this.working) return;
    this.phase = 'downloading'; this.error = ''; this.downloaded = 0; this.total = undefined;
    try {
      await this.update.download(event => {
        if (event.event === 'Started') this.total = event.data.contentLength;
        if (event.event === 'Progress') this.downloaded += event.data.chunkLength;
      }, { timeout: 300_000 });
      this.phase = 'ready'; this.message = 'Update downloaded and signature verified.';
    } catch (reason) { this.error = `Could not download a trusted update. ${String(reason)}`; this.phase = 'idle'; await this.release(); }
    finally { if (this.disposed) await this.release(); }
  }
  async install() {
    if (!this.update || this.phase !== 'ready') return;
    this.phase = 'installing'; this.error = '';
    try {
      await invoke('prepare_update');
      await this.update.install();
      this.phase = 'installed'; this.message = 'Update installed. Restart TanaHub to finish.';
      await this.restart();
    } catch (reason) {
      await invoke('resume_after_update').catch(() => {});
      this.error = `Could not install the update. ${String(reason)} Close other copies of TanaHub and try again. On macOS, move TanaHub to Applications first.`;
      this.phase = 'ready';
    }
  }
  async restart() {
    try { await (await import('@tauri-apps/plugin-process')).relaunch(); }
    catch (reason) { this.error = `Update installed. Quit and reopen TanaHub to finish, or try Restart now. ${String(reason)}`; }
  }
  async browse() {
    try { await openExternal('https://github.com/Komortes/TanaHub/releases'); }
    catch { this.error = 'Could not open GitHub. Please try again.'; }
  }
}

// One native update resource for the whole window, including navigation away from Settings.
export const desktopUpdates = new DesktopUpdates();
