# Validation — 2026-11-08

- Built the x64 Windows Forms application with the Windows .NET Framework compiler, warnings treated as errors.
- 141 fixture assertions passed: modern/legacy VDF, manifests and traversal, library/root deduplication, custom parent folders, nested Unreal executables, cancellation, FG false positives, anti-cheat blocking, proxy conflicts, ZIP paths/links/duplicates/x86 rejection, archive digests, cached package tampering, exclusive install rollback, owned update rollback, safe uninstall, preserved settings, stable app versions, trusted release URLs/digests, real self-update replacement/backup/restart, running-game refusal, and an actual launch process with special characters in its path.
- The actual desktop app started and discovered 11 Steam entries from two available libraries. A stale third library and incomplete scans were reported. Anti-cheat blocking and search were inspected through Windows UI automation.
- Automatic update settings were inspected on screen with both requested defaults enabled and the correct launcher fork configured.
- The official v1.7.0 ZIP downloaded and imported through the native package code with its published archive digest verified. DLL SHA-256: e6b6e6b70a6ed3af839c9647a32fc22660ae6b2d655062c5c072246c91fc10c7.
- One live transport request failed before a later native retry succeeded. Bounded transport retries were added. TLS/certificate validation and download host restrictions remain enforced.

Tests modified only generated fixture directories. No mod was installed into a real game and no real game was launched during validation. Runtime mod compatibility in a real game is therefore unverified. This build intentionally excludes Bink and OptiScaler plugin installation and standalone XeMFG routes.
