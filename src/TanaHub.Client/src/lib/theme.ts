type ResolvedTheme = 'Nebula dark' | 'Light' | 'High contrast';

/** Apply a saved preference without replacing System with its temporary palette. */
export function applyTheme(preference: string): () => void {
  const apply = (theme: ResolvedTheme) => { document.documentElement.dataset.theme = theme; };
  if (preference !== 'System') {
    apply(preference === 'Light' || preference === 'High contrast' ? preference : 'Nebula dark');
    return () => {};
  }

  const system = window.matchMedia('(prefers-color-scheme: dark)');
  const update = () => apply(system.matches ? 'Nebula dark' : 'Light');
  update();
  system.addEventListener('change', update);
  return () => system.removeEventListener('change', update);
}
