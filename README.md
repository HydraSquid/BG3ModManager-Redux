# Baldur's Gate 3 Mod Manager Redux

This is a development fork of **[circleainn/BG3ModManager-Redux](https://github.com/circleainn/BG3ModManager-Redux)**.
Please refer to the [upstream README](https://github.com/circleainn/BG3ModManager-Redux#readme)
for the project overview, requirements, installation, and general usage. This page documents only
how our `hydra-dev` branch differs from that Redux baseline.

**Upstream:** [Repository](https://github.com/circleainn/BG3ModManager-Redux) |
[Website](https://bg3mm-redux.com/) |
[Nexus Mods listing](https://www.nexusmods.com/baldursgate3/mods/23799)

## Baseline

This branch is based on Redux commit
[`74995a2`](https://github.com/circleainn/BG3ModManager-Redux/commit/74995a2).
Later upstream changes have not yet been merged, including the newer save-workflow protections,
unsaved-change prompts, and Windows-protected API-key storage.

[View this branch's changes since that baseline](https://github.com/HydraSquid/BG3ModManager-Redux/compare/74995a2...hydra-dev).
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
- **Source-association fixes:** explicit Nexus installs remain Nexus even before metadata is
  fetched or when the PAK also contains a mod.io identifier. Provider-neutral **Link Mod Page**
  validates BG3 Nexus/mod.io URLs and supports manual mod.io links without an API key.
- **Table readability and selection:** theme-aware alternating rows across tables, including
  custom themes, plus deferred cross-list selection clearing to avoid a WPF selection crash.
- **Shutdown handling:** closing waits for Nexus work and settings persistence. A failed shutdown
  keeps the window visible and allows another attempt instead of leaving a hidden process.

## Limits and Verification

Keep backups before testing. Personal API keys in this branch are still stored in local portable
`Data/settings.json`; do not share that file or your runtime `Data`, `Orders`, or `_Logs` folders.
Downloads require a declared HTTP response length and are limited to 32 GiB per archive.

The fork includes regression coverage for the changes above. From a Windows checkout with the
upstream build prerequisites and submodules installed, run `./Test-Redux.ps1`.
Windows CI also targets `hydra-dev`.

For a problem specific to these changes, use [this fork's issue tracker](https://github.com/HydraSquid/BG3ModManager-Redux/issues).
If it also reproduces on the unmodified baseline, report it upstream with that distinction.
Never include credentials or private data in reports.

## Credits and License

The application and its branding come from [circleainn's Redux](https://github.com/circleainn/BG3ModManager-Redux)
and [LaughingLeader's original BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
Their copyright and attribution are retained under the [MIT License](LICENSE).
Dependency and asset licenses remain in [Third-Party Notices](licenses/Third-Party-Notices.md).
