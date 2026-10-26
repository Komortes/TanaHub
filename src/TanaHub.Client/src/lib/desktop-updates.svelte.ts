import type { Update } from '@tauri-apps/plugin-updater';
import { isDesktop, openExternal } from './native';

type Phase = 'idle' | 'checking' | 'available' | 'downloading' | 'ready' | 'installing' | 'installed';
type CheckOutcome = 'never' | 'checking' | 'available' | 'current' | 'failed' | 'unsupported';
type CompletedCheckOutcome = Exclude<CheckOutcome, 'never' | 'checking'>;
interface CheckMetadata { version: 1; lastAttempt: number; lastSuccessfulCheck: number | null; outcome: CompletedCheckOutcome }
interface UpdateReminder { version: string; until: number }
const checkMetadataKey = 'tanahub-update-check';
const reminderKey = 'tanahub-update-reminder';
const reminderDuration = 86_400_000;
const validReminder = (value: Partial<UpdateReminder> | null, now: number): value is UpdateReminder => !!value
  && typeof value.version === 'string' && value.version.length <= 100
  && /^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/.test(value.version)
  && typeof value.until === 'number' && Number.isSafeInteger(value.until)
  && value.until > now && value.until <= now + reminderDuration;
async function invoke<T>(command: string): Promise<T> { return (await import('@tauri-apps/api/core')).invoke<T>(command); }

class DesktopUpdates {
  phase = $state<Phase>('idle');
  update = $state<Update | null>(null);
  message = $state('');
  error = $state('');
  downloaded = $state(0);
  total = $state<number | undefined>();
  dismissed = $state(false);
  lastAttempt = $state<number | null>(null);
  lastSuccessfulCheck = $state<number | null>(null);
  checkOutcome = $state<CheckOutcome>('never');
  checkFailure = $state('');
  private reminder = $state<UpdateReminder | null>(null);
  private reminderTimer: ReturnType<typeof setTimeout> | undefined;
  private started = false;
  private disposed = false;
  constructor() {
    try {
      const saved = JSON.parse(localStorage.getItem(reminderKey) ?? 'null') as Partial<UpdateReminder> | null;
      if (validReminder(saved, Date.now())) this.reminder = { version: saved.version, until: saved.until };
    } catch { /* A reminder is optional when storage is unavailable. */ }
    try {
      const saved = JSON.parse(localStorage.getItem(checkMetadataKey) ?? 'null') as Partial<CheckMetadata> | null;
      const now = Date.now();
      const validDate = (value: unknown): value is number => typeof value === 'number' && Number.isSafeInteger(value) && value > 0 && value <= now;
      if (saved?.version !== 1 || !validDate(saved.lastAttempt) || (saved.lastSuccessfulCheck !== null && !validDate(saved.lastSuccessfulCheck)) || !['available', 'current', 'failed', 'unsupported'].includes(saved.outcome ?? '')) return;
      this.lastAttempt = saved.lastAttempt;
      this.lastSuccessfulCheck = saved.lastSuccessfulCheck ?? null;
      this.checkOutcome = saved.outcome as CompletedCheckOutcome;
    } catch { /* Update history is optional when storage is unavailable. */ }
  }
  private rememberCheck() {
    if (this.lastAttempt === null || this.checkOutcome === 'never' || this.checkOutcome === 'checking') return;
    const saved: CheckMetadata = { version: 1, lastAttempt: this.lastAttempt, lastSuccessfulCheck: this.lastSuccessfulCheck, outcome: this.checkOutcome };
    try { localStorage.setItem(checkMetadataKey, JSON.stringify(saved)); }
    catch { /* The current check remains usable without persisted history. */ }
  }
  get working() { return ['checking', 'downloading', 'installing'].includes(this.phase); }
  get percent() { return this.total ? Math.min(100, Math.round(this.downloaded / this.total * 100)) : undefined; }

  private get reminderActive() { return validReminder(this.reminder, Date.now()) && this.reminder.version === this.update?.version; }
  private availableMessage() {
    const message = `Version ${this.update?.version} is available`;
    return this.reminderActive
      ? `${message}. Reminder paused until ${new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(this.reminder!.until)}. You can download it here at any time.`
      : message;
  }
  private clearReminder() {
    clearTimeout(this.reminderTimer); this.reminderTimer = undefined;
    this.reminder = null;
    try { localStorage.removeItem(reminderKey); } catch { /* The current session can still clear its reminder. */ }
    if (this.phase === 'available' && this.update) this.message = this.availableMessage();
  }
  private scheduleReminderExpiry() {
    clearTimeout(this.reminderTimer); this.reminderTimer = undefined;
    if (validReminder(this.reminder, Date.now())) {
      this.reminderTimer = setTimeout(() => this.clearReminder(), Math.min(reminderDuration, Math.max(0, this.reminder.until - Date.now())));
    } else if (this.reminder) this.clearReminder();
  }
  remindIn24Hours() {
    if (this.phase !== 'available' || !this.update || this.error) return;
    const now = Date.now();
    const reminder = { version: this.update.version, until: now + reminderDuration };
    if (!validReminder(reminder, now)) { this.dismissed = true; return; }
    this.reminder = reminder;
    try { localStorage.setItem(reminderKey, JSON.stringify(reminder)); } catch { /* Keep the reminder for this session without storage. */ }
    this.scheduleReminderExpiry();
    this.message = this.availableMessage();
  }
  get showNotice() { return !this.dismissed && !['installing', 'installed'].includes(this.phase) && (['available', 'downloading', 'ready'].includes(this.phase) || !!this.error) && !(this.phase === 'available' && !this.error && this.reminderActive); }
  start() {
    this.disposed = false;
    if (!isDesktop()) return;
    this.scheduleReminderExpiry();
    if (this.started) return;
    this.started = true;
    void this.check(true);
  }

  private async release() { const previous = this.update; this.update = null; await previous?.close().catch(() => {}); }
  dispose() { this.disposed = true; clearTimeout(this.reminderTimer); this.reminderTimer = undefined; if (!this.working) void this.release(); }

  async check(silent = false) {
    if (!isDesktop() || this.working || this.phase === 'installed') return;
    if (!silent) { this.dismissed = false; this.clearReminder(); }
    this.lastAttempt = Date.now(); this.checkOutcome = 'checking'; this.checkFailure = '';
    this.phase = 'checking'; this.error = ''; this.message = '';
    try {
      await this.release();
      const support = await invoke<{ supported: boolean; reason: string | null }>('update_support');
      if (!support.supported) { this.message = support.reason ?? 'Download an installer from Browse releases.'; this.checkOutcome = 'unsupported'; this.phase = 'idle'; return; }
      this.update = await (await import('@tauri-apps/plugin-updater')).check({ timeout: 20_000 });
      if (this.update && this.reminder && this.update.version !== this.reminder.version) this.clearReminder();
      this.lastSuccessfulCheck = Date.now(); this.checkOutcome = this.update ? 'available' : 'current';
      this.message = this.update ? this.availableMessage() : 'You are up to date';
      this.phase = this.update ? 'available' : 'idle';
    } catch (reason) {
      this.checkOutcome = 'failed'; this.checkFailure = `Could not check for updates. ${String(reason)}`;
      if (!silent) this.error = this.checkFailure;
      this.phase = 'idle';
    }
    finally { this.rememberCheck(); if (this.disposed) await this.release(); }
  }
  async download() {
    if (!this.update || this.phase !== 'available') return;
    this.clearReminder();
    this.dismissed = false;
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
