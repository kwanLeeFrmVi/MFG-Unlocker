# MFG Launcher

A native Windows desktop app for finding installed games, reviewing MFG Unlocker compatibility, installing/removing an owned mod DLL, and launching games.

## Run

Double-click `MFGLauncher.exe`. Windows 10/11 x64 with .NET Framework 4.8 is required (included in current Windows installations). No Node.js, Electron, SDK, administrator mode, or installer is needed. The executable is unsigned.

The app keeps a single window; launching it again brings that window forward. Steam discovery starts automatically. It reads Steam registry locations, conventional Steam folders on accessible fixed drives, `libraryfolders.vdf`, and installed-game manifests. DSX, Lossless Scaling, and Steamworks Common Redistributables are excluded by their Steam app IDs. Search and status filters narrow the results. Click a game card or **Launch** to start it; **Details** opens evidence and executable selection.

Use **Add folder** for a game from another store or an undiscovered library. Choose whether it contains one game or several game folders. **Manage folders** removes a scan root from the library without deleting any game files; **Rescan** refreshes discovery. **Scan report** explains inaccessible paths and incomplete scans.

## Install

1. Select **Check for updates** to obtain the official [MFG Unlocker release](https://github.com/dashdogy/MFG-Unlocker/releases). If GitHub cannot be reached, download its ZIP yourself and use **Import ZIP**.
2. Open **Details** and verify the actual game executable. Unreal titles commonly use `Binaries\Win64`. Ambiguous games remain in **Needs review** until a runtime is selected.
3. Select **Install** on an eligible game or in **Details**. Review the release, source, destination, and available proxy filenames. An unlisted candidate requires explicitly checking the review box. Anti-cheat installations and manual updates require a fresh risk acknowledgment; the launcher does not disable anti-cheat.
4. Close the game before installing. Then launch it, enable Frame Generation in its settings, and press **Backspace** for the mod menu.

For The Witcher 3, the launcher prioritizes `bin\x64_dx12\witcher3.exe`. Its DX11 and backup executables require review. If the in-game FG option is unavailable, open the mod menu, select **Game FG option: Unlock**, restart the game, then enable DLSS Frame Generation.

**Compatibility guide** renders the current upstream Markdown inside the launcher and provides **Reload** to fetch it again.

The launcher does not bundle MFG Unlocker. An imported ZIP is labeled as a local import; selecting a filename does not prove its origin. Published archive SHA-256 values are verified when GitHub provides them. The app also validates archive paths, DLL architecture, and cached DLL hashes.

## Status meanings

- **Reported compatible**: current local FG files plus an embedded upstream game/proxy report and an identified x64 runtime.
- **Candidate**: Streamline FG files found, without a matching game report. Working compatibility is unverified.
- **Needs review**: runtime ambiguity, missing evidence, proxy conflict, incomplete scan, or altered ownership.
- **Anti-cheat warning**: an unsupported anti-cheat title/component; an otherwise eligible direct route can be installed manually after acknowledging the disclaimer.
- **Blocked**: an unsupported installation route or missing requirements that acknowledgment cannot waive.
- **No FG evidence**: normal-route Streamline Frame Generation was not found. Ordinary DLSS Super Resolution is insufficient.
- **Installed**: an ownership receipt matches the installed DLL and selected runtime.

Detection does not guarantee working support for a particular patch, GPU, graphics API, or settings. This version supports only the direct DLL / existing Streamline DLSS FG route. It excludes Bink, OptiScaler plugin, and experimental standalone XeMFG installation. Rules were checked against [upstream compatibility documentation](https://github.com/dashdogy/MFG-Unlocker/blob/main/docs/compatibility.md) on 2026-11-08. Anti-cheat titles are recognized even when local markers are absent. Automatic updates skip them; each manual install/update requires acknowledgment.

## Remove and update

**Uninstall** removes only the launcher's owned, unchanged DLL and its receipt. Altered DLLs and unrelated mods are preserved. Existing filenames are never overwritten during a fresh installation. Close running games first; the app never terminates them.

To update, load the newer release, open **Details**, and select **Update release**. The previous DLL and receipt are retained outside the game loading folders for rollback. If an operation fails, its error is shown and automatic rollback is attempted. Settings and mod-generated logs are preserved. Permission failures are reported without automatic elevation.

Launcher settings, package cache, and update rollback data are in `%LOCALAPPDATA%\MFG Launcher`. Each managed game has an ownership receipt in its root folder. Keep the receipt to preserve safe uninstall/update ownership. The app does not change graphics settings or install GPU runtime providers.

## Build and test

Open PowerShell in the extracted source folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Version 1.0.2
```

`build\MFGLauncher.exe` is the portable application. The script uses the C# compiler shipped with Windows .NET Framework and standard framework assemblies; it downloads no dependencies. Tests use temporary synthetic game directories and DLL fixtures, not real installed games. The launch test starts a copied test process with special characters in its path to verify argument safety and working-directory handling.

For an isolated UI fixture library:

```powershell
.\build\MFGLauncher.Tests.exe --ui-smoke
```

The source is split into discovery, compatibility, package/installation, desktop UI, and launch services. See `docs\VALIDATION.md` for observed test results and limitations.

This is an independent launcher, not an official MFG Unlocker or Valve product. MFG Unlocker remains a separate upstream project and download.

## Automatic updates

Automatic DLL and launcher updates are enabled by default, as requested. Checks run after startup discovery and every six hours while open. **Check for updates** runs a check immediately; **Update settings** lets you disable automatic changes or change the launcher release repository.

The two update channels are separate:

- Launcher executable: `kwanLeeFrmVi/MFG-Unlocker`, asset `MFGLauncher.exe`, stable tag `launcher-vMAJOR.MINOR.PATCH`.
- Mod DLL: official `dashdogy/MFG-Unlocker` release ZIP.

Owned DLLs update only after receipt/hash validation and running-game checks. Incomplete scans, changed runtimes, modified files, and uncomparable/newer imported versions are skipped and listed in **Scan report**. Automatic updates never install into a game for the first time.

The launcher downloads a newer release to staging, requires its published SHA-256, validates an x64 executable, waits for the current process to close, replaces it atomically, keeps a `.previous-*.bak` copy, and restarts. Failed replacement or launch attempts restore the previous file when possible. Keep the portable executable in a writable folder; the app never elevates itself.

To publish a launcher update in your fork, push a `launcher-v1.0.2` tag or run the **Build MFG Launcher** workflow with version `1.0.2`. It stamps the executable's assembly version, runs the fixture tests, builds the portable app, and publishes `MFGLauncher.exe` and `SHA256SUMS.txt`. The version stamp prevents repeat-update loops. Upstream mod source/build files are kept separate in the fork; the launcher lives under `launcher/`.



