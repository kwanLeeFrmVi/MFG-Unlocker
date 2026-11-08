# Validation — 2026-11-08

- Built the x64 Windows Forms application with the Windows .NET Framework compiler, warnings treated as errors.
- 141 fixture assertions passed: modern/legacy VDF, manifests and traversal, library/root deduplication, custom parent folders, nested Unreal executables, cancellation, FG false positives, anti-cheat blocking, proxy conflicts, ZIP paths/links/duplicates/x86 rejection, archive digests, cached package tampering, exclusive install rollback, owned update rollback, safe uninstall, preserved settings, stable app versions, trusted release URLs/digests, real self-update replacement/backup/restart, running-game refusal, and an actual launch process with special characters in its path.
- The actual desktop app started and discovered 11 Steam entries from two available libraries. A stale third library and incomplete scans were reported. Anti-cheat blocking and search were inspected through Windows UI automation.
- Automatic update settings were inspected on screen with both requested defaults enabled and the correct launcher fork configured.
- The official v1.7.0 ZIP downloaded and imported through the native package code with its published archive digest verified. DLL SHA-256: e6b6e6b70a6ed3af839c9647a32fc22660ae6b2d655062c5c072246c91fc10c7.
- One live transport request failed before a later native retry succeeded. Bounded transport retries were added. TLS/certificate validation and download host restrictions remain enforced.

Tests modified only generated fixture directories. No mod was installed into a real game and no real game was launched during validation. Runtime mod compatibility in a real game is therefore unverified. This build intentionally excludes Bink and OptiScaler plugin installation and standalone XeMFG routes.

Final release verification:

- Clean Windows GitHub Actions build, all fixture tests, and publication succeeded: https://github.com/kwanLeeFrmVi/MFG-Unlocker/actions/runs/42457070935
- Published launcher-v1.0.0 was downloaded through LauncherUpdates.Check/Download, validated as x64, and matched GitHub's published SHA-256: 3e27114b60f7aaeee89c57edd4078701c89836b0f79c692fc90935b56bffb86b.
- The delivered MFGLauncher.exe is the verified CI release binary. Compiler timestamps mean the earlier locally compiled binary had a different checksum.
- The final native DLL downloader, with bounded retries, downloaded and verified official v1.7.0 again.
- A later local rebuild of MFGLauncher.Tests.exe was denied by Windows (CS0016 access denied). The existing 141-pass local result and the clean final-source Windows CI pass are recorded; no system protection settings were changed.

## v1.0.1

- Removed card outlines and status-edge stripes; removed button, text input, and list input borders.
- Applied Discord-inspired graphite surfaces, blurple actions, and rounded buttons. Native keyboard focus cues remain available.
- Added a per-session single-instance guard. A second launch exited successfully while the original process and window remained.
- Excluded DSX (1812620), Lossless Scaling (993090), and Steamworks Common Redistributables (228980), including matching custom roots. The live library now shows 8 games.
- All 16 targeted discovery checks passed locally. The clean Windows release build passed all 143 checks: https://github.com/kwanLeeFrmVi/MFG-Unlocker/actions/runs/42458956421
- The delivered executable matches the published v1.0.1 SHA-256: f7ff62482702377989391b777ba910cbd5b7bc11a3a85ad9f2454ea0de128acd.
