# MFG Launcher design

The user authorized a functional Windows desktop app and requested parallel implementation. This native C# / Windows Forms implementation uses the Windows .NET Framework 4.8 compiler and standard libraries, with no downloaded SDK or runtime. Electron was considered but adds a large runtime and dependency packaging; native Windows controls provide direct filesystem access and a small portable executable.

The library scans Steam registry and drive locations, parses Steam library manifests, and scans explicitly added single-game or parent folders. Workers are bounded and cancellable. Compatibility evidence distinguishes reported normal-route support from unverified candidates. DLSS Super Resolution alone is insufficient. Anti-cheat titles/markers, unsupported routes, incomplete scans, and unresolved executables disable installation.

The desktop uses slate blue surfaces (#101925, #182536, #213248), warm copper actions (#f4a261), light text (#f1f5f9), and muted labels (#afbed0). Segoe UI keeps native Windows legibility. A left rail groups folder/release controls; the main panel provides search, status filtering, and responsive game cards. Clicking a card launches, while Details opens evidence and executable selection. Keyboard-accessible native buttons and explicit text communicate status.

The package layer downloads official release assets or validates an imported ZIP. It rejects dangerous archive paths and wrong architecture. Installation uses exclusive creation and hash-bound ownership receipts; update retains rollback data; removal refuses changed files. User game files are never modified by development fixtures.

Deliver a portable x64 executable, source archive with build/test scripts, usage instructions, and actual validation results. Bink, OptiScaler plugin, and experimental XeMFG standalone routes are excluded from this release.

The user additionally requested automatic app and DLL updates, a fork under kwanLeeFrmVi, and autonomous completion while AFK. Default updates run at startup/six-hour intervals. The app update source is kwanLeeFrmVi/MFG-Unlocker; DLL releases remain dashdogy/MFG-Unlocker. Launcher tags are launcher-vMAJOR.MINOR.PATCH and builds stamp the corresponding assembly version. Auto DLL changes apply only to unchanged owned installations after a complete scan and matching runtime; they do not perform initial game installation.
