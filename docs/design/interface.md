# TanaHub interface direction

The interface is a personal anime and manga library. It serves someone returning
for the next episode or chapter, finding a title, or recording a thought after
watching. Current titles and progress take precedence over collection statistics.

## A personal shelf

The product's vocabulary is a library, a watchlist, a volume, a season, a bookmark,
a next episode and a private note. Cover artwork supplies most of the colour.
The surrounding interface uses warm graphite, paper-coloured text, quiet rules
and a small bookmark accent. Keep the existing logo recognisable.

The signature is a progress bookmark: a narrow marker next to the episode/chapter
position, repeated on the collection shelf, Home and the tracking panel. It
should connect the screens through actual tracking information, not decoration.

## Hierarchy

- Home starts with in-progress titles and their next-episode/chapter action.
  Statistics remain available at a quieter level; charts do not displace actions.
- The collection is a shelf of covers and captions, rather than a grid of raised
  containers. List view uses row separators. Titles and quick actions keep aligned
  baselines, including long names and unknown totals.
- Home, My library and Discover form the main navigation. Schedule and Recognize
  are secondary tools. Settings sits apart on desktop and remains directly
  accessible in compact navigation.
- Filters use compact tabs and clear selected states. Reserve enclosed controls
  for text entry, menus and actions. Metadata must not resemble clickable filters.
- A title page keeps tracking close to its cover and identity. Reading, editing
  and supporting metadata use distinct levels rather than identical panels.

## Interaction contract

Preserve routes, existing accessible names, keyboard restoration and explicit
Save/Discard behavior. Do not reset filters or drafts as part of a visual change.
Do not hide a destructive operation in a hover-only affordance.

Use system fonts for native reading and navigation. Use tabular figures where
positions, scores or times are compared. Keep normal text readable without the
high-contrast theme; preserve that theme and reduced-motion support independently.
Existing persisted theme values remain compatible even if display labels change.

The current Tauri client offers System, Dark, Light and High contrast. Dark retains
the `Nebula dark` wire value and the existing default. System stores the preference
and resolves it separately for the document, with one removable OS-change observer.
Light uses paper surfaces and dark ink; text on a primary action has a separate
on-accent token. Check native controls, imagery, dialogs and focus in each palette.

Validate first-use, populated, filtered-empty, loading and failed states. Inspect
at 320/360, 768, 1024 and 1440px; verify both pointer and keyboard use. Browser
fixtures must use isolated data. Keep screenshots outside source.

## Patterns to retire

- Six equally prominent statistics cards above the titles a person came to use.
- Purple-tinted surfaces and bright progress/status colours everywhere.
- A rounded enclosing card, outline and floating hover motion for every object.
- Several rows of equally weighted pill-shaped filters before any artwork.
- Promotional subtitles that repeat what the page heading already says.

Use a controlled palette, quiet separators, flat shelves and direct copy instead.
Review the rendered result after each small increment; changing colour alone is
not a redesign. Keep a checked progress log in AUTONOMOUS_PROGRESS.md.
