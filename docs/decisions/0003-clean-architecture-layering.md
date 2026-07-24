# 0003. Clean Architecture layering: Domain / Application / Infrastructure / UI

## Status

Accepted

## Context

The solution is split into `TanaHub.Domain`, `TanaHub.Application`, `TanaHub.Infrastructure`, `TanaHub.UI`, and `TanaHub.Desktop` (the DI composition root). `PRODUCT.md` states that "every architectural decision should be legible and defensible" to a reviewer evaluating the codebase — this layering is one of the decisions most worth making explicit, since it's the one a code reviewer is most likely to inspect directly.

## Decision

Dependency direction points inward only: `TanaHub.Domain` has no dependency on anything else in the solution; `TanaHub.Application` depends only on `TanaHub.Domain` and defines service interfaces (`IMediaCatalogService`, `IUserLibraryService`, `IAiringScheduleService`, etc.) without referencing any concrete implementation; `TanaHub.Infrastructure` implements those interfaces against real external systems (AniList GraphQL, MangaDex REST, trace.moe, local JSON files) and is the only layer that knows those systems exist; `TanaHub.UI` depends on `TanaHub.Application`'s interfaces, never on `TanaHub.Infrastructure` directly; `TanaHub.Desktop` is the only project allowed to reference every layer, because it's where DI wiring happens (`InfrastructureServiceCollectionExtensions`).

This is proven in the code, not just asserted: `RoutedMediaCatalogService` and `ICatalogSourceSelector` let the app route between AniList and MangaDex behind the single `IMediaCatalogService` interface. Adding a new catalog source is implementing one interface in `TanaHub.Infrastructure` — it touches neither `TanaHub.Domain` nor `TanaHub.UI`.

## Consequences

Easier: swapping or adding an external data source is isolated to `TanaHub.Infrastructure`; `TanaHub.Application` and `TanaHub.Domain` are testable with no HTTP or filesystem dependencies (see `TanaHub.Application.Tests`, which has zero infrastructure references); a reviewer can audit the dependency rule by checking project references alone.

Harder: every new capability that crosses layers (e.g. a new external API) requires touching an interface in `TanaHub.Application` plus an implementation in `TanaHub.Infrastructure` plus DI registration in `TanaHub.Desktop` — more ceremony than a single-project app, accepted as the cost of the isolation above.
