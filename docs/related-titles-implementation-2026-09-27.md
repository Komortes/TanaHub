# Related titles implementation

The title detail page now loads direct relations from AniList and MangaDex independently of the main catalog card. It groups previous/next parts, sources/adaptations, side stories/spin-offs, other versions/connections, and fan works. The last two groups start collapsed. Each card shows its relation, format, year/count when available, source, and matching local library status.

## Navigation and identity

Opening a related title uses its provider ID, or an exact existing library alias when available. MAL anime and manga IDs have separate namespaces. Distinct MangaDex UUIDs remain distinct even when their external catalog IDs match; alternative editions do not borrow the original's local progress. Back walks the related-title history before returning to the original page. A successful title change resets the main scroll position.

Every navigation cancels the previous relation request. Late responses are ignored by both cancellation and page identity checks. Errors stay inside the related-title section, with a retry action. A failed title navigation leaves the current page usable and restarts its interrupted relation load. Library mutations continue to update the existing detail model.

## Providers and cache

- AniList requests direct `relations.edges` with `relationType(version: 2)`, typed target IDs, display metadata, and the existing adult-content filter. Unused GraphQL lookup variables are omitted; sending explicit null lookup values was verified to return no title for otherwise valid IDs.
- MangaDex reads its expanded relation endpoint and exact `links.al`/`links.mal` mapping. It can enrich its own links with AniList anime adaptations without matching titles by name. Its safe/suggestive filter also applies to bridged identities.
- Confirmed complete results, including empty lists, are stored separately in `relations_cache.json` for 24 hours, up to 250 source titles. Partial results remain retryable and do not overwrite complete snapshots. On temporary source failure, saved data is marked as such. The existing offline-cache preference applies to this file as well.
- Missing required relation data produces a partial-data notice rather than a cached assertion that no relationships exist.
- On macOS, MangaDex metadata GETs use the system `/usr/bin/curl` transport because the tested .NET TLS implementation could not negotiate the server's required TLS version. The handler accepts only the public MangaDex manga endpoints, disables user curl configuration, retains certificate verification, does not follow redirects, bounds responses and timeouts, and terminates cancelled requests. Other platforms retain the regular HTTP transport.

## Scope

This is navigation between catalog titles, not an episode/chapter reader or a complete recommended watch order. Only direct links are fetched: following a season reveals its own links. No recursive franchise crawl, title-name matching, automatic library merging, or additional metadata provider is introduced.

MangaDex does not guarantee covers or chapter totals for every relation. Missing data remains absent; `lastChapter` is not treated as a total count. Year sorting is a display aid, not a claim about story chronology.

## Verification

Regression tests cover provider responses, typed IDs and editions, partial failures, content filtering, cache lifecycle, background navigation races, retry after failed navigation, back history, and type-specific progress language. The native smoke workflow is documented in [smoke-test.md](smoke-test.md#related-titles).

Final solution test run: **273 passed** (94 Application, 179 Infrastructure). Desktop build: zero warnings and errors. Actual services resolved through production DI returned complete fresh results for AniList anime `16498` (11 relations), manga `53390` (21), and MangaDex `304ceac3-8cdb-4fe7-acf7-2b6ff7a60613` (75 after content filtering and exact-ID composition, including 13 anime adaptations). Tests and live probes used a separate temporary profile.

The native macOS smoke pass verified opening Attack on Titan, following its manga source with existing reading progress, going Back to the anime, and opening Season 2 with its own previous/next season links. Each successful transition started at the page heading. The initial visual pass exposed default gray expander panels; group headers were replaced with controls using the existing TanaHub theme. A second pass confirmed the updated styling and the MangaDex-to-AniList adaptation link, which opened the existing anime library entry with its saved 8/25 progress and its own 11 relations.
