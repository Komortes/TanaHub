<script lang="ts">
  import { ChevronDown, LayoutGrid, List, Search, SlidersHorizontal, X } from '@lucide/svelte';
  import { statuses, statusLabel } from '../types';
  import type { Layout, MediaType, Status } from '../types';
  let { page, type = $bindable(), status = $bindable(), search = $bindable(), sort = $bindable(), layout = $bindable(), coverSize = $bindable(), total, genre = $bindable(''), librarySort = $bindable('Updated'), tag = $bindable(''), customList = $bindable(''), tags = [], customLists = [], preset = $bindable('Popular'), emptyLibrary = false }:
    { page: 'library' | 'discover'; type: MediaType | ''; status: Status | ''; search: string; sort: string; layout: Layout; coverSize: number; total: number | null; genre?: string; librarySort?: string; tag?: string; customList?: string; tags?: string[]; customLists?: string[]; preset?: string; emptyLibrary?: boolean } = $props();
  let optionsOpen = $state(false);
  let optionsToggle = $state<HTMLButtonElement>();
  const activeFilters = $derived(page === 'library' ? Number(!!tag) + Number(!!customList) : 0);
  const hasLibraryFilters = $derived(page === 'library' && !!(tags.length || customLists.length || activeFilters));
</script>

<header class="page-header">
  <div>
    <h1>{page === 'library' ? 'My library' : 'Discover'}</h1>
    <p>{page === 'library' ? 'Your stories, at your pace.' : 'Find your next favourite story.'}</p>
  </div>
  <div class="search-box">
    <Search size={18} strokeWidth={1.7} />
    <input aria-label={page === 'library' ? 'Search your library' : 'Search the catalog'} placeholder={page === 'library' ? 'Search your library' : 'Search the catalog'} bind:value={search} type="search" autocomplete="off" />
    {#if search}<button aria-label="Clear search" onclick={() => search = ''}><X size={16} /></button>{/if}
  </div>
</header>
{#if !emptyLibrary}
<div class="type-row">
  <div class="segmented" aria-label="Media type">
    {#each ['', 'Anime', 'Manga'] as mediaType}
      <button class:selected={type === mediaType} aria-pressed={type === mediaType} onclick={() => type = mediaType as MediaType}>{mediaType || 'All'}</button>
    {/each}
  </div>
  {#if total !== null}<span class="collection-count"><span class="mono">{total}</span> {total === 1 ? 'title' : 'titles'}</span>{/if}
</div>
{#if page === 'discover'}
  <div class="browse-presets" aria-label="Discover collections">
    {#each ['Popular', 'Trending', 'This year', 'Top rated', 'Upcoming', 'Manhwa'] as item}
      <button class:selected={preset === item} aria-pressed={preset === item} onclick={() => { preset = item; search = ''; genre = ''; type = item === 'Manhwa' ? 'Manga' : item === 'Top rated' ? '' : 'Anime'; sort = item === 'Trending' ? 'Trending' : item === 'Top rated' ? 'Score' : item === 'Upcoming' ? 'Newest' : 'Popularity'; }}>{item}</button>
    {/each}
  </div>
{/if}
<div class="collection-toolbar">
  {#if page === 'library'}
    <div class="status-tabs" aria-label="Library status">
      <button class:selected={status === ''} aria-pressed={status === ''} onclick={() => status = ''}>All titles</button>
      {#each statuses as item}
        <button class:selected={status === item} aria-pressed={status === item} onclick={() => status = item}>{!type && item === 'Watching' ? 'Watching / Reading' : !type && item === 'Repeating' ? 'Rewatching / Rereading' : statusLabel(item, type || 'Anime')}</button>
      {/each}
    </div>
  {/if}
  <div class="toolbar-actions">
    {#if page === 'library'}
      <label class="sort-select"><span>Sort</span><select aria-label="Sort library" bind:value={librarySort}><option>Updated</option><option>Title</option><option>Score</option><option>Progress</option></select></label>
    {:else}
      <label class="sort-select"><span>Sort</span><select aria-label="Sort catalog" bind:value={sort}><option value="Popularity">Popularity</option><option value="Trending">Trending</option><option value="Score">Highest rated</option><option value="Newest">Newest</option></select></label>
      <select class="genre-filter" aria-label="Filter catalog genre" bind:value={genre}><option value="">All genres</option>{#each ['Action','Adventure','Comedy','Drama','Fantasy','Horror','Isekai','Mecha','Romance','Sci-Fi','Slice of Life','Thriller'] as value}<option>{value}</option>{/each}</select>
    {/if}
    <button class="options-toggle" class:active={activeFilters > 0} aria-expanded={optionsOpen} aria-controls="collection-options" aria-label={hasLibraryFilters ? `Filters and display${activeFilters ? `, ${activeFilters} active filters` : ''}` : 'Display options'} onclick={() => optionsOpen = !optionsOpen} bind:this={optionsToggle}>
      <SlidersHorizontal size={16} /><span>{hasLibraryFilters ? 'Filters' : 'Display'}</span>{#if activeFilters}<span class="filter-count">{activeFilters}</span>{/if}<ChevronDown size={13} class={optionsOpen ? 'expanded' : ''} />
    </button>
    <div class="layout-toggle" aria-label="View layout">
      <button class:selected={layout === 'grid'} aria-label="Grid view" aria-pressed={layout === 'grid'} onclick={() => layout = 'grid'}><LayoutGrid size={18} /></button>
      <button class:selected={layout === 'list'} aria-label="List view" aria-pressed={layout === 'list'} onclick={() => layout = 'list'}><List size={20} /></button>
    </div>
  </div>
  {#if optionsOpen}
    <div class="collection-options" id="collection-options">
      {#if page === 'library' && (tags.length || tag)}<label>Tag<select aria-label="Filter by tag" bind:value={tag}><option value="">All tags</option>{#if tag && !tags.includes(tag)}<option>{tag}</option>{/if}{#each tags as value}<option>{value}</option>{/each}</select></label>{/if}
      {#if page === 'library' && (customLists.length || customList)}<label>List<select aria-label="Filter by custom list" bind:value={customList}><option value="">All lists</option>{#if customList && !customLists.includes(customList)}<option>{customList}</option>{/if}{#each customLists as value}<option>{value}</option>{/each}</select></label>{/if}
      <label class="size-control" title="Change cover size"><span>Cover size</span><input type="range" aria-label="Cover size" min="140" max="220" step="10" style:--range-fill={`${(coverSize - 140) / 80 * 100}%`} bind:value={coverSize} /></label>
      {#if activeFilters}<button class="clear-filters" onclick={() => { tag = ''; customList = ''; optionsToggle?.focus({ preventScroll: true }); }}><X size={13} />Clear filters</button>{/if}
    </div>
  {/if}
</div>

{/if}

<style>
  .browse-presets { display:flex; flex-wrap:wrap; gap:6px; padding:16px 0 0; }
  .browse-presets button { background:transparent; color:var(--secondary); border:1px solid var(--border); padding:7px 12px; font-size:12px; }
  .browse-presets button.selected { color:var(--lavender); border-color:var(--border-lit); background:var(--violet-soft); }
  .toolbar-actions { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; margin-left: auto; }
  .options-toggle { display: inline-flex; align-items: center; justify-content: center; gap: 6px; min-height: 38px; padding: 8px 10px; background: transparent; color: var(--secondary); border: 1px solid var(--border); font-size: 12px; }
  .options-toggle[aria-expanded='true'], .options-toggle.active { color: var(--lavender); border-color: var(--border-lit); background: var(--violet-soft); }
  .options-toggle :global(.expanded) { transform: rotate(180deg); }
  .filter-count { min-width: 17px; padding: 1px 4px; border-radius: 4px; background: var(--violet-soft); color: var(--lavender); font-size: 10px; }
  .collection-options { display: flex; flex: 0 0 100%; flex-wrap: wrap; gap: 12px 18px; align-items: center; padding: 14px; border: 1px solid var(--border); background: var(--shell); border-radius: 8px; }
  .collection-options label { display: flex; align-items: center; gap: 8px; font-size: 12px; color: var(--secondary); min-width: 0; }
  .collection-options select, .genre-filter { font-size: 12px; min-height: 38px; padding: 6px 32px 6px 10px; max-width: 180px; min-width: 0; }
  .clear-filters { background: transparent; border: 0; color: var(--secondary); display: inline-flex; align-items: center; gap: 4px; font-size: 11px; padding: 8px 0; }
  .page-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 24px; padding-bottom: 24px; }
  h1 { font-size: 30px; font-weight: 700; line-height: 1.2; letter-spacing: -.035em; margin: 0 0 8px; }
  p { margin: 0; color: var(--secondary); font-size: 14px; }
  .search-box { display: flex; align-items: center; gap: 10px; background: var(--shell); border: 1px solid var(--border); border-radius: 9px; padding: 0 13px; height: 42px; width: 290px; color: var(--secondary); }
  .search-box:focus-within { border-color: var(--violet); background: var(--elevated); }
  .search-box input { background: none; border: none; outline: none; color: var(--text); width: 100%; min-width: 0; padding: 0; font-size: 13px; }
  .search-box button { padding: 4px; border: none; background: transparent; color: var(--secondary); display: flex; }
  .type-row { display: flex; align-items: center; gap: 16px; border-bottom: 1px solid var(--border); padding-bottom: 18px; }
  .segmented { display: flex; padding: 3px; border-radius: 9px; background: var(--shell); gap: 3px; }
  .segmented button { padding: 9px 20px; background: transparent; border: none; font-size: 13px; color: var(--secondary); }
  .segmented button.selected { background: var(--violet-soft); color: var(--lavender); }
  .collection-count { font-size: 12px; color: var(--muted); }
  .collection-toolbar { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; justify-content: space-between; padding: 16px 0 20px; }
  .status-tabs { display: flex; gap: 4px; overflow-x: auto; max-width: 100%; min-width: 0; scrollbar-width: thin; padding: 3px; margin: -3px; }
  .status-tabs button { border: 1px solid transparent; background: transparent; padding: 9px 12px; font-size: 12px; white-space: nowrap; color: var(--secondary); }
  .status-tabs button.selected { background: var(--violet-soft); border-color: #9b6ef822; color: var(--lavender); }
  .status-tabs button:hover:not(.selected) { background: var(--raised); color: var(--text); }
  .layout-toggle { display: flex; gap: 3px; }
  .layout-toggle button { padding: 8px; display: flex; border: none; background: transparent; color: var(--secondary); }
  .layout-toggle button.selected { background: var(--violet-soft); color: var(--lavender); }
  .size-control { margin-left: auto; white-space: nowrap; }
  .size-control input { width: 94px; padding: 0; height: 32px; }
  .sort-select { font-size: 12px; display: flex; align-items: center; gap: 10px; color: var(--secondary); }
  .sort-select select { min-height: 38px; padding: 6px 32px 6px 8px; font-size: 12px; min-width: 0; }
  @media (max-width: 740px) { .page-header { flex-direction: column; gap: 18px; padding-bottom: 20px; } .search-box { width: 100%; } h1 { font-size: 28px; } }
  @media (max-width: 520px) { .status-tabs { width: 100%; } .status-tabs button { padding-inline: 11px; } .collection-toolbar { gap: 10px; padding-bottom: 14px; } .toolbar-actions { width: 100%; gap: 8px; } .sort-select { gap: 6px; } .sort-select > span { display: none; } .layout-toggle { margin-left: auto; } .layout-toggle button { padding: 8px 7px; } .options-toggle { padding: 8px; } .options-toggle :global(svg:last-child) { display: none; } .collection-options label { flex: 1 1 100%; justify-content: space-between; } .collection-options select { max-width: calc(100% - 40px); flex: 1; } .size-control { margin-left: 0; } .size-control input { flex: 1; max-width: 160px; } }
</style>
