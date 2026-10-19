<script lang="ts">
  import { BookOpen, LibraryBig, Compass, House, CalendarDays, ScanSearch, Settings2 } from '@lucide/svelte';
  import type { PageName } from '../types';
  const navigation: { page: PageName; label: string; icon: typeof House }[] = [
    { page: 'home', label: 'Home', icon: House }, { page: 'library', label: 'My library', icon: LibraryBig },
    { page: 'discover', label: 'Discover', icon: Compass }, { page: 'schedule', label: 'Schedule', icon: CalendarDays },
    { page: 'recognize', label: 'Recognize', icon: ScanSearch }, { page: 'settings', label: 'Settings', icon: Settings2 },
  ];
  let { page, onNavigate }: { page: PageName; onNavigate: (page: PageName) => void } = $props();
</script>

<aside class="sidebar">
  <a class="brand" href="#library" onclick={(event) => { event.preventDefault(); onNavigate('library'); }} aria-label="TanaHub, open library">
    <img src="/logo.svg" alt="" width="38" height="38" />
    <span>TanaHub</span>
  </a>
  <nav aria-label="Main navigation">
    {#each navigation as item}
      <button class:active={page === item.page} aria-current={page === item.page ? 'page' : undefined} onclick={() => onNavigate(item.page)} title={item.label} aria-label={item.label}>
        <item.icon size={20} strokeWidth={1.7} /><span>{item.label}</span>
      </button>
    {/each}
  </nav>
  <div class="sidebar-footer"><BookOpen size={20} strokeWidth={1.6} /><span>Anime &amp; manga</span></div>
</aside>

<style>
  .sidebar { background: var(--shell); border-right: 1px solid var(--border); padding: 26px 12px 24px; display: flex; flex-direction: column; gap: 28px; position: sticky; top: 0; height: 100dvh; }
  .brand { display: flex; align-items: center; gap: 10px; padding: 0 10px; color: var(--text); text-decoration: none; font-size: 22px; font-weight: 700; letter-spacing: -.04em; }
  nav { display: flex; flex-direction: column; gap: 6px; }
  nav button { display: flex; align-items: center; gap: 14px; min-height: 48px; border: 1px solid transparent; background: transparent; color: var(--secondary); padding: 12px 16px; text-align: left; font-size: 14px; position: relative; }
  nav button:hover { background: var(--raised); color: var(--text); }
  nav button.active { background: var(--violet-soft); border-color: #9b6ef82e; color: var(--lavender); }
  nav button.active::before { content: ''; position: absolute; left: -3px; height: 24px; width: 3px; background: var(--violet); border-radius: 4px; }
  .sidebar-footer { margin-top: auto; display: flex; gap: 12px; align-items: center; padding: 0 14px; color: var(--secondary); font-size: 12px; }
  @media (max-width: 1050px) and (min-width: 641px) {
    .sidebar { padding: 20px 10px 16px; gap: 18px; }
    .brand { padding: 0; justify-content: center; }
    .brand span, .sidebar-footer span { display: none; }
    nav button { flex-direction: column; justify-content: center; gap: 3px; padding: 6px 0; min-height: 52px; }
    nav button span { display: block; font-size: 9.5px; line-height: 13px; white-space: nowrap; }
    .sidebar-footer { justify-content: center; padding: 0; }
  }
  @media (max-width: 640px) {
    .sidebar { height: auto; position: sticky; z-index: 10; flex-direction: row; align-items: center; gap: 12px; padding: 10px 16px; border-right: none; border-bottom: 1px solid var(--border); }
    .brand { font-size: 18px; padding: 0; gap: 6px; }
    .brand img { width: 30px; height: 30px; }
    nav { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); flex: 1; min-width: 0; gap: 2px; }
    nav button { flex-direction: column; justify-content: center; padding: 6px 0; min-width: 0; min-height: 48px; gap: 3px; }
    nav button span { display: block; font-size: 9.5px; line-height: 13px; white-space: nowrap; }
    nav button.active::before, .sidebar-footer { display: none; }
  }
  @media (max-width: 480px) {
    .brand span { display: none; }
    .brand img { width: 24px; height: 24px; }
    .sidebar { gap: 6px; padding-inline: 8px; }
  }
</style>
