# Library Recommendations Design

## Goal

Finish the roadmap item for recommendations based on the user's library with a local-first recommendation strip on Home.

## Scope

Recommendations are heuristic and local-first:

- Read the user's library entries.
- Load known catalog metadata for those entries.
- Build a small genre profile from titles the user is watching, reading, completed, or scored.
- Search the existing catalog for high-scoring titles in those genres.
- Exclude titles already in the library.
- Show the ranked results on Home.

This slice does not add remote account-based recommendations or machine-learning personalization.

## Ranking

Genre weights come from library entries:

- Completed and current titles contribute more than planning/paused.
- Dropped titles do not contribute.
- User score adds more weight when present.

Candidate score is the sum of matching genre weights, with catalog average score as a tie-breaker.

## UI

Home gets a `RECOMMENDED FOR YOU` strip using existing `MediaSearchResultViewModel` cards. Empty libraries or libraries with no genre metadata simply hide the strip.

## Testing

Unit-test that the recommendation builder:

- extracts top genres from library metadata;
- excludes titles already in the library;
- ranks matching candidates above unrelated candidates.
