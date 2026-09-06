# Baldur's Gate 3 Mod Manager Redux

This is a development fork of **[circleainn/BG3ModManager-Redux](https://github.com/circleainn/BG3ModManager-Redux)**.
Please refer to the [upstream README](https://github.com/circleainn/BG3ModManager-Redux#readme)
for the project overview, requirements, installation, and general usage. This page documents only
how our `main` branch differs from that Redux baseline.

**Upstream:** [Repository](https://github.com/circleainn/BG3ModManager-Redux) |
[Website](https://bg3mm-redux.com/) |
[Nexus Mods listing](https://www.nexusmods.com/baldursgate3/mods/23799)

## Baseline

This branch includes upstream Redux through
[`7147cd9`](https://github.com/circleainn/BG3ModManager-Redux/commit/7147cd9), including its
save-workflow protections, unsaved-change prompts, and Windows-protected API-key storage.
Our changes are merged on top without rewriting the shared history.

[View this branch's changes since that baseline](https://github.com/HydraSquid/BG3ModManager-Redux/compare/7147cd9...main).
This is development source, not a separate stable release.

## Current Differences

- **Nexus download links:** optional per-user `nxm://` handling, preserving the previous handler.
  Enable online integrations and configure a personal Nexus API key, then opt in through
  **Tools > Handle Nexus Mod Manager Download links**.
- **Persistent Downloads pane:** bounded concurrent transfers with pause, resume, retry,
  cancellation, and removal. Short-lived authorization and signed download URLs are not stored
  in the queue; expired free-account authorization requires a fresh Nexus link.
- **Separate, reviewed installation:** downloaded archives are staged and their PAKs validated
  before installation. Replaced files receive recovery copies. Downloading does not activate,
  reorder, or export mods, and missing dependencies block installation rather than being installed
  automatically.
- **Descriptive failures and recovery:** persisted failure details name missing dependencies and
  distinguish unreadable archives, unsupported layouts, validation failures, and file-access
  problems. **Details** can inspect older generic failures without installing them.
  **Download Again** preserves the previous archive and avoids reusing old partial data.
- **Dependency assistance:** Details and install review distinguish installed, bundled, identified
  queued, and possible same-project requirements. Reviewed UUID mappings offer **Open Nexus Files**;
  unknown UUIDs get **Copy UUID** instead of a guessed link. Files and variants are never chosen or
  downloaded automatically. Queue identities come from this session's package inspections.
- **Supported native components:** Tools > Native Mod Loader and Plugins installs reviewed ZIP
  layouts for [Native Mod Loader](https://www.nexusmods.com/baldursgate3/mods/944),
  [WASD Character Movement](https://www.nexusmods.com/baldursgate3/mods/781), and
  [Native Camera Tweaks](https://www.nexusmods.com/baldursgate3/mods/945). Their Nexus downloads
  use the same native installer, with explicit game-directory review, backups, and guarded restore.
  Selected batches put the loader before its plugins. These are DLL components, not PAK load-order
  entries; other DLL mods and arbitrary archive layouts are not supported.
- **Native requirements:** Downloads and Native Mods tools distinguish a Redux-verified loader,
  an external/unverified pair, and missing or changed files. No loader version or universal game
  compatibility is inferred from filenames. Supported plugin installs require BG3 Hotfix 34
  (`4.1.1.6931813`) or newer and the loader; Script Extender is not their prerequisite.
- **Source-association fixes:** explicit Nexus installs remain Nexus even before metadata is
  fetched or when the PAK also contains a mod.io identifier. Provider-neutral **Link Mod Page**
  validates BG3 Nexus/mod.io URLs and supports manual mod.io links without an API key.
- **Table readability and selection:** theme-aware alternating rows across tables, including
  custom themes, plus deferred cross-list selection clearing to avoid a WPF selection crash.
- **Shutdown handling:** after the upstream unsaved-change confirmation, closing waits for Nexus
  work and settings persistence. A failed shutdown keeps the window visible and allows another
  attempt instead of leaving a hidden process.

## Limits and Verification

Keep backups before testing and keep runtime `Data`, `Orders`, and `_Logs` folders private.
Downloads require a declared HTTP response length and are limited to 32 GiB per archive.
Native installation is deliberately narrower: ZIP archives up to 128 MiB, only the catalogued
DLL/TOML paths, and bounded extraction. Keep BG3 closed and obtain native code from its author;
format checks do not authenticate a publisher. An initial loader installation requires the current
game Bink DLL to match the archive's packaged original; unknown replacements and mismatches after
game updates require manual review rather than a forced overwrite. Existing TOML settings are kept.
Restore refuses externally modified files and will not remove the loader while plugin DLLs remain.
Private native ownership records and recovery copies live under `Data/NativeInstalls`; preserve them.

The fork includes regression coverage for the changes above. From a Windows checkout with the
upstream build prerequisites and submodules installed, run `./Test-Redux.ps1`.
Windows CI targets `main`.

For a problem specific to these changes, use [this fork's issue tracker](https://github.com/HydraSquid/BG3ModManager-Redux/issues).
If it also reproduces on the unmodified baseline, report it upstream with that distinction.
Never include credentials or private data in reports.

## Credits and License

The application and its branding come from [circleainn's Redux](https://github.com/circleainn/BG3ModManager-Redux)
and [LaughingLeader's original BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
Their copyright and attribution are retained under the [MIT License](LICENSE).
Dependency and asset licenses remain in [Third-Party Notices](licenses/Third-Party-Notices.md).
