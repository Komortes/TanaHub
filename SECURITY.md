# Security

TanaHub is pre-release software. Security fixes target the current development branch; there are no published stable versions to maintain yet.

## Reporting a vulnerability

Use [GitHub private vulnerability reporting](https://github.com/Komortes/TanaHub/security/advisories/new) when available. If it is unavailable, contact the maintainer through [their GitHub profile](https://github.com/Komortes) to arrange a private report before sharing exploit details publicly.

Include the affected commit/version, OS, reproduction steps and expected impact. Do not include tokens, client secrets, API keys, `.settings.key`, personal images or a copy of your app-data folder.

## Local data and integrations

The SQLite library, settings and caches live in the user's app-data directory. Sensitive settings are encrypted with a key stored in that same directory. Encryption does not protect against an attacker who can read both the settings and the key under your OS account.

Online catalogs contact their providers. Recognition uploads the selected image when the user confirms a search. AniList import requires an explicitly connected account. Use an isolated profile for tests and revoke provider credentials if they may have leaked.

Preview binaries are not a signed, notarized distribution channel. SHA-256 checksums verify file integrity; they do not establish publisher identity.
