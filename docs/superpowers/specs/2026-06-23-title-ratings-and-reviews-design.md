# Ratings and Reviews Design

## Goal

Let a user store a 1–10 rating and one personal text review for every title in
their local library.

## Scope

- The existing `UserMediaEntry.Score` remains the single rating field.
- Add a distinct optional `Review` field. Existing `Notes` stay private
  free-form notes and are not repurposed as a review.
- A review is local-only in this increment. AniList synchronization is
  unchanged.
- A review is edited on the title-detail page. The library list continues to
  show only the numeric score.
- An empty submitted review removes the stored review.
- Reject input longer than 4,000 characters before persistence.
- Removing a library entry removes its review because the review is owned by
  that entry.

## Data and Persistence

`UserMediaEntry` gains `string? Review`. The JSON DTO used by
`FileUserLibraryService` serializes and deserializes this optional value. Old
library files remain valid because an absent JSON property is read as `null`.

`IUserLibraryService` gains an `UpdateReviewAsync(mediaId, review,
cancellationToken)` operation. Both library-service implementations normalize
whitespace-only input to `null`, reject text over 4,000 characters, update
`UpdatedAt`, and return the updated entry.

## UI and Flow

`MediaDetailViewModel` exposes the saved review and an async save command.
`TitleDetailView` adds a multiline "Review" field next to the existing Notes
field. Saving calls `UpdateReviewAsync`; success refreshes the library and the
open detail model, while a failure is displayed through the existing
`SearchStatus` feedback path.

The existing score controls continue to set scores from 1 to 10. No new score
control or list-column is added.

## Error Handling

- A missing library entry produces the existing not-found failure result.
- Review text over 4,000 characters returns a validation failure and leaves
  the stored review unchanged.
- Failed persistence returns the existing infrastructure failure and leaves
  the UI model untouched until the next successful refresh.

## Testing

- Domain tests cover construction and backward-compatible default `Review`.
- In-memory service tests cover save, clear, validation, and a missing entry.
- File service tests cover persistence round-trip and loading a JSON file that
  has no `review` property.

## Non-goals

- Public reviews, social sharing, or moderation.
- Separate reviews per source or per language.
- Multi-axis ratings.
- AniList review synchronization.
