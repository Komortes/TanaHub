# Interface stability pass

Button text, background, border, and pressed/disabled feedback use short eased transitions. Keyboard focus is drawn in a separate adorner, so it cannot shrink or move button contents. Selection indicators reserve their space in Discover, Schedule, and Settings.

Library refresh updates existing observable cards by media ID and reconciles collections with insert/move/remove rather than reset. This preserves their commands and rendered controls. UI library writes, including import, share one queue; a progress read/write cannot overwrite a later status, score, or organization change. The queue includes the fast local detail update and releases before catalog refresh.

Discover pagination retains existing results and its Load more button while requesting the next page. Loading feedback appears inside a fixed button slot. Re-selecting the current sidebar section also performs navigation and cancels pending title loads; mouse and Enter use the same command.

Title detail separates daily tracking from the hero and places synopsis/details before related titles. The hero is more compact, and removal has an explicit label in the organization section. Overview, tracking, and personal fields adapt to the available width while keeping controls and drafts alive.

Library and Schedule move filters above content when the page width is at most 850 px. Compact library rows put progress, score, and actions on a second line. Schedule combines day/time, keeps the title readable, and moves its library label under the title. Page and row grid definitions, positions, and visibility are updated together in view code, including explicit restoration of the wide layout. No controls are reconstructed during a size change.

Six new regression tests reproduce card replacement, collection resets, disappearing pagination, overlapping library writes, and re-selection of the current section. The complete solution test run passed **279 tests** (100 Application, 179 Infrastructure).

Desktop build completed with zero warnings or errors. Native macOS checks used an isolated profile: Library and Schedule passed 1180 → 920 → 1180 px resizing, and their wider row layout was checked at 1440 px. Title detail retained an unsaved notes draft through resizing. Keyboard focus remained visible without shifting a poster card, progress changed in place, and re-selecting Home closed its title detail.
