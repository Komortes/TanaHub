<script lang="ts">
  import { LibraryBig, Compass, House, CalendarDays, ScanSearch, Settings2 } from '@lucide/svelte';
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
    <img src="/logo.svg" alt="" width="30" height="30" />
    <span>TanaHub</span>
  </a>
  <nav aria-label="Main navigation">
    {#each navigation as item}
      <button class:active={page === item.page} aria-current={page === item.page ? 'page' : undefined} onclick={() => onNavigate(item.page)} title={item.label} aria-label={item.label}>
        <item.icon size={20} strokeWidth={1.7} /><span>{item.label}</span>
      </button>
    {/each}
  </nav>
</aside>

<style>
  .sidebar { background: var(--ink); border-right: 1px solid var(--border); padding: 30px 12px 20px; display: flex; flex-direction: column; gap: 34px; position: sticky; top: 0; height: 100dvh; }
  .brand { display: flex; align-items: center; gap: 8px; padding: 0 6px; color: var(--text); text-decoration: none; font-size: 19px; font-weight: 650; letter-spacing: -.025em; }
  nav { display: flex; flex: 1; flex-direction: column; gap: 4px; }
  nav button { display: flex; align-items: center; gap: 12px; min-height: 44px; border: 1px solid transparent; background: transparent; color: var(--secondary); padding: 10px; text-align: left; font-size: 13px; position: relative; }
  nav button:hover { background: var(--raised); color: var(--text); }
  nav button.active { background: var(--shell); color: var(--text); }
  nav button.active::before { content: ''; position: absolute; left: -1px; height: 18px; width: 2px; background: var(--bookmark); }
  nav button:nth-child(4) { margin-top: 24px; }
  nav button:last-child { margin-top: auto; }
  @media (max-width: 1050px) and (min-width: 641px) {
    .sidebar { padding: 20px 10px 16px; gap: 18px; }
    .brand { padding: 0; justify-content: center; }
    .brand span { display: none; }
    nav button { flex-direction: column; justify-content: center; gap: 3px; padding: 6px 0; min-height: 52px; }
    nav button span { display: block; font-size: 9.5px; line-height: 13px; white-space: nowrap; }
  }
  @media (max-width: 640px) {
    .sidebar { height: auto; position: sticky; z-index: 10; flex-direction: row; align-items: center; gap: 12px; padding: 10px 16px; border-right: none; border-bottom: 1px solid var(--border); }
    .brand { font-size: 18px; padding: 0; gap: 6px; }
    .brand img { width: 30px; height: 30px; }
    nav { display: grid; grid-template-columns: repeat(6, auto); flex: 1; min-width: 0; gap: 2px; }
    nav button { flex-direction: column; justify-content: center; padding: 6px 0; min-width: 44px; min-height: 48px; gap: 3px; }
    nav button:nth-child(4), nav button:last-child { margin-top: 0; }
    nav button.active { color: var(--bookmark-bright); border-bottom-color: var(--bookmark); border-radius: 0; background: transparent; }
    nav button span { display: block; font-size: 9.5px; line-height: 13px; white-space: nowrap; }
    nav button.active::before { display: none; }
  }
  @media (max-width: 480px) {
    .brand span { display: none; }
    .brand img { width: 24px; height: 24px; }
    .sidebar { gap: 6px; padding-inline: 8px; }
    nav button span { font-size: 10px; }
  }
  @media (max-width: 359px) { .brand { display: none; } }
</style>
