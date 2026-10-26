type ResolvedTheme = 'Nebula dark' | 'Light' | 'High contrast';
type ThemePreference = ResolvedTheme | 'System';
const cacheKey = 'tanahub-theme';
const isPreference = (value: unknown): value is ThemePreference => value === 'System' || value === 'Nebula dark' || value === 'Light' || value === 'High contrast';
const setTheme = (theme: ResolvedTheme) => { document.documentElement.dataset.theme = theme; };

export function readThemePreference(): ThemePreference {
  try {
    const value = localStorage.getItem(cacheKey);
    if (isPreference(value)) return value;
  } catch { /* Private WebViews may disable storage. */ }
  return 'Nebula dark';
}

/** Cache only a preference confirmed by the settings backend. */
export function cacheThemePreference(preference: string): void {
  if (!isPreference(preference)) return;
  try { localStorage.setItem(cacheKey, preference); } catch { /* Theme application must still work without storage. */ }
}

/** Set the first palette before mounting; the App effect owns any observer. */
export function bootstrapTheme(): void {
  const preference = readThemePreference();
  setTheme(preference === 'System'
    ? window.matchMedia('(prefers-color-scheme: dark)').matches ? 'Nebula dark' : 'Light'
    : preference);
}

/** Apply a saved preference without replacing System with its temporary palette. */
export function applyTheme(preference: string): () => void {
  if (preference !== 'System') {
    setTheme(preference === 'Light' || preference === 'High contrast' ? preference : 'Nebula dark');
    return () => {};
  }

  const system = window.matchMedia('(prefers-color-scheme: dark)');
  const update = () => setTheme(system.matches ? 'Nebula dark' : 'Light');
  update();
  system.addEventListener('change', update);
  return () => system.removeEventListener('change', update);
}
