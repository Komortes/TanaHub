# Interface screenshots

These captures show the current TanaHub interface with a separate sample library.
They use the real Svelte client and .NET/SQLite core through the local development
adapter. No RPC fixtures or altered interface elements are used.

| File | View | Image size |
|---|---|---|
| [library.png](library.png) | Anime and manga in Watching / Reading, with progress and ratings | 1440 × 780 |
| [tracking.png](tracking.png) | Tracking panel on the Frieren title page, captured directly as an element | 885 × 260 |

Captured on 19 October 2026 using Chromium and Playwright. The application UI is
the same as the `v0.1.0` preview. Catalog metadata and artwork are provided by
[AniList](https://anilist.co); the progress, ratings, notes and lists are sample data.

To refresh the images, start the client with `TANAHUB_DATA_DIRECTORY` set to a new,
absolute temporary directory and add sample titles from Discover. Use a 1440 × 780
viewport and the Nebula dark theme: capture the full library viewport and the
tracking section as an element screenshot. Wait for covers and fonts to finish
loading and dismiss transient feedback before capturing. Keep browser chrome,
developer tools and credentials out of the frame.

Save raw captures under the ignored `output/playwright/` directory. Promote only
the selected screenshots here and check them at the width of GitHub's README.
Do not use a personal library or an app-data backup as screenshot material.
