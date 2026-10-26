# TanaHub interface direction

The interface is a personal anime and manga library. It serves someone returning
for the next episode or chapter, finding a title, or recording a thought after
watching. Current titles and progress take precedence over collection statistics.

This document describes the current development branch. Its interface and feature
changes are unreleased; published 0.1.4 Preview installers remain unchanged.

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
  Its Statistics button opens `#home/statistics`, with Back to Home and normal
  browser history. Statistics shares Home's Anime/Manga/All filter; returning
  restores the entry point and retained Home context. Keep the six global
  navigation destinations rather than adding a seventh compact-navigation item.
  Statistics reads the local summary without recommendations or airing checks.
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

Cache only one of the four confirmed preferences, never System's resolved Light
or Dark palette. Apply that preference synchronously before mounting so a delayed
settings read does not repaint a saved Light choice as Dark. Bootstrap creates no
observer; the App effect owns one System observer, including while settings load,
and removes it on preference changes or disposal. Backend settings take precedence
over the cache. Invalid or unavailable browser storage must leave the app usable;
a rejected save must leave the confirmed preference intact.

## Settings feedback

Desktop About TanaHub distinguishes Last attempt, Last successful check and Check
status. Persist completed check outcomes rather than an in-flight Checking state;
failed or unsupported attempts preserve the prior successful-check time. Download
or installation failures do not rewrite a successful check as a failed one. This
history is optional when browser storage is unavailable. It is feedback, not a
restored update payload or permission to skip a fresh check before downloading.

The Notifications switch waits for the read-only platform capability check.
Loading, unsupported and failed checks disable it without changing the saved
preference; a failed check offers Retry reminder check. Current macOS and Linux
adapters are supported; Windows reports unsupported in this build. Platform
support does not establish OS permission or guarantee visible delivery. Explain
that reminders are checked when Home's All/Anime view or today's Schedule loads
or refreshes, while the app runs; do not imply a timer or a service running after
the app closes.

## Verification

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
