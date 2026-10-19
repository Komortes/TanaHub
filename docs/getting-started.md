# Start using TanaHub

TanaHub keeps your library on your computer. You do not need an account, an AniList developer application or a recognition API key to browse titles and track progress.

## Install or run

Download **TanaHub 0.1.0 Preview** from the [release page](https://github.com/Komortes/TanaHub/releases/tag/v0.1.0). Use the package for your OS and architecture. Packaged builds include the .NET runtime; you do not need Node, Rust or a .NET SDK.

- **Apple Silicon macOS 15+:** open the `.dmg`, then drag TanaHub to Applications. The preview is ad-hoc signed and not notarized. If Gatekeeper blocks it, attempt to open it once, then use **System Settings → Privacy & Security → Open Anyway** for TanaHub if you trust this download. Do not disable Gatekeeper globally. Intel Macs are not included in this preview.
- **Windows x64:** run the `.exe` installer. It is unsigned, so SmartScreen may warn about an unrecognized app; **More info → Run anyway** is available when allowed by your system policy. WebView2 may be downloaded during setup if missing.
- **Linux x64:** install the `.deb` with `sudo apt install ./TanaHub-0.1.0-linux-x64.deb`, or mark the `.AppImage` executable with `chmod +x TanaHub-0.1.0-linux-x64.AppImage` and launch it. AppImage requires FUSE 2 support; on Ubuntu 22.04 install `libfuse2` if needed. On systems without FUSE, `./TanaHub-0.1.0-linux-x64.AppImage --appimage-extract-and-run` is an alternative.

Each platform has a `SHA256SUMS.txt` asset; [verify checksums](release.md#signing-and-validation-limits) if desired. macOS startup and editing have been exercised interactively. Windows/Linux packages have automated native build and core checks; interactive installation and full end-to-end validation remain preview limitations.

Before using an existing library, close the app and [back up its data](#export-and-backup).

To run from a development checkout, install [the requirements](../README.md#requirements), then from the repository root:

```sh
node scripts/check-environment.mjs
cd src/TanaHub.Client
npm ci
npm run desktop:dev
```

You can also run `npm run doctor` from `src/TanaHub.Client`. It checks Node, SDK selection through `global.json`, Rust and Cargo, and gives installation guidance when a tool is missing. OS build prerequisites are a separate requirement; the doctor does not claim that a native compiler, Linux libraries or Windows build tools are ready.

## Your first title

1. Open **Discover**, select Anime or Manga, and search for a title. Catalog access needs an internet connection. AniList is the default source for both types; MangaDex is an optional source for manga in Settings.
2. Open the title and choose **Add to library**. New entries start in Planning.
3. In **Your tracking**, set the status to Watching or Reading when you start. Use **+1 episode/chapter** or type a progress value and choose **Set progress**.
4. Open the rating control, choose a score and save it. Notes, reviews, organisation and tracking dates also have explicit Save actions; typing a draft does not save it.
5. Return to **My library**. All shows anime and manga together. Each collection remembers its own search and media type when you switch sections.

Tracking is saved locally. Catalog and recognition providers can fail or apply rate limits without deleting your library. Previously loaded catalog data is available through the offline cache when enabled; titles not yet cached still need a connection.

## Bring an existing list

From an empty library, **Import a list** opens **Settings → Your library data** directly. Otherwise open that section in Settings yourself.

- **MAL XML:** choose a MyAnimeList XML export, review the preview, then explicitly import it. Choosing a file alone does not apply the import.
- **AniList:** connecting an account is optional. Expand **How to connect AniList**, configure your own AniList application and connect through your browser. **Import from AniList** asks for confirmation before changing local entries. This imports data from AniList; local changes are not sent back.

Both imports can replace progress, status, scores, notes and tracking dates for matching titles. Reviews, tags and custom lists are preserved. Other local titles are retained. Review the confirmation before proceeding.

## Export and backup

**Export CSV** creates a readable copy of the library. TanaHub does not import CSV. **Export MAL XML** creates an exchange file for titles with a MyAnimeList mapping; the export reports skipped titles without that mapping.

For a complete backup, close TanaHub and copy its entire `TanaHub/` app-data folder, including SQLite files, settings and the local encryption key. If you set `TANAHUB_DATA_DIRECTORY`, back up that directory instead. Do not share the backup: it contains your personal data and encrypted credentials together with their key.

## If something does not work

| What you see | What to do |
|---|---|
| No matching titles | Reset filters, check Anime/Manga/All and clear the search. |
| Library or settings could not load | Use Try again. Avoid importing or editing data until the library is readable. |
| No native sidecar in browser development | Run `npm run prepare:bridge`, then `npm run dev`; desktop startup prepares it automatically. |
| Missing SDK, Rust or Cargo | Run the doctor and follow the reported installation step. Restart the terminal after installing tools. |
| Recognition needs an image | Choose a supported image under 5 MB, then explicitly choose Find matches. The selected image is sent to the named provider. |
| Update check does not offer this preview | The updater checks stable releases; prereleases are downloaded manually from the release page. |

For a reproducible bug, include the version/commit, OS, architecture and steps in a [bug report](https://github.com/Komortes/TanaHub/issues/new/choose). Remove credentials and personal data from attachments.
