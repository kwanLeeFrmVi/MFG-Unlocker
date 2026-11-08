# Validation — 2026-11-08

- Built the x64 Windows Forms application with the Windows .NET Framework compiler, warnings treated as errors.
- 141 fixture assertions passed: modern/legacy VDF, manifests and traversal, library/root deduplication, custom parent folders, nested Unreal executables, cancellation, FG false positives, anti-cheat blocking, proxy conflicts, ZIP paths/links/duplicates/x86 rejection, archive digests, cached package tampering, exclusive install rollback, owned update rollback, safe uninstall, preserved settings, stable app versions, trusted release URLs/digests, real self-update replacement/backup/restart, running-game refusal, and an actual launch process with special characters in its path.
- The actual desktop app started and discovered 11 Steam entries from two available libraries. A stale third library and incomplete scans were reported. Anti-cheat blocking and search were inspected through Windows UI automation.
- Automatic update settings were inspected on screen with both requested defaults enabled and the correct launcher fork configured.
- The official v1.7.0 ZIP downloaded and imported through the native package code with its published archive digest verified. DLL SHA-256: e6b6e6b70a6ed3af839c9647a32fc22660ae6b2d655062c5c072246c91fc10c7.
- One live transport request failed before a later native retry succeeded. Bounded transport retries were added. TLS/certificate validation and download host restrictions remain enforced.

Initial v1.0.0 validation used only generated fixtures. Real-game installation and testing were subsequently performed as recorded below. The launcher excludes Bink and OptiScaler plugin installation and standalone XeMFG routes.

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

## v1.0.2

- Clean Windows release workflow passed all 181 checks: https://github.com/kwanLeeFrmVi/MFG-Unlocker/actions/runs/42461756263
- Locally ran 111 targeted discovery, compatibility, installer, and Markdown assertions, all passing.
- Witcher discovery prefers bin/x64_dx12/witcher3.exe; DX11 and backup executable choices cannot borrow sibling Frame Generation evidence. Details supports Install and refreshes the library when runtime selection changes.
- Anti-cheat install/manual update requires a fresh unchecked risk acknowledgment. Missing FG, invalid executables, incomplete scans, unsupported OptiScaler routes, and proxy conflicts remain ineligible. Automatic updates do not reuse acknowledgments.
- Compatibility guide downloads and renders the current upstream Markdown, including its three tables and issue links. HTML is escaped and executable content is not loaded.
- Published executable SHA-256: 8860f181f4b2710dbe17b54fbf29f942ff89af18a271a86eb3f7f5185f84feec.

## v1.0.3

- Clean Windows release workflow passed all 181 checks: https://github.com/kwanLeeFrmVi/MFG-Unlocker/actions/runs/42462353970
- Fixed native guide header layout so Reload remains visible. Confirmed the live Markdown tables and links render inside the app, and Reload refreshes the displayed fetch timestamp.
- Inspected the production anti-cheat acknowledgment dialog with an isolated fixture: Install stays disabled while unchecked and becomes enabled after acknowledgment. Canceled without installing the fixture.
- Added the requested D:\Games parent through the actual launcher. It persisted as a parent root and discovered three game subfolders; Manage folders provides removal without deleting games.
- Installed official MFG Unlocker v1.7.0 into the Witcher 3 DX12 runtime and verified its hash and ownership receipt. Runtime testing is documented in the separate local test report.
- Delivered executable SHA-256: 5ba7e7de010894e12681cb80c8059991f05dc455c43da3be0479696ec57904bd.

- Subsequent real Witcher 3 DX12 / RTX 3080 Ti smoke test succeeded at 2x: game and applied DLSS FG enabled, local route ready, 2,364 submitted batches with zero failed batches/provider failures, sampled ~29.6 rendered / ~59.2 output FPS. Backed up graphics settings and corrected conflicting FSR FG/AMD Anti-Lag settings; the test scene loaded successfully. The game exited through its normal Quit dialog. This is a stationary smoke test, not a sustained benchmark or a test of higher multipliers.
