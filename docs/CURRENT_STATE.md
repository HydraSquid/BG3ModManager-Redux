# Current project state

This page records the decisions and boundaries that define Redux today. It is the first reference
to check before changing established behavior. The [changelog](CHANGELOG.md) records what shipped,
the [issue tracker](https://github.com/circleainn/BG3ModManager-Redux/issues) tracks individual
reports and proposals, and the source and tests remain authoritative for implementation details.

Last reviewed: September 12, 2026, at release commit `9006c652`.

## Current release

| Item | Current value |
|:--|:--|
| Product | Baldur's Gate 3 Mod Manager Redux |
| Short name | Redux |
| Latest version | `0.1.0-alpha.16.3.4` |
| Lifecycle | Public alpha |
| Supported platform | Windows 10/11 x64 |
| Required runtime | .NET 8 Desktop Runtime |
| Application | `Redux.exe` |
| Distribution | One portable ZIP |
| Public sources | GitHub Releases and Nexus Mods |
| Update channel | `public-alpha` |
| Active milestone | `v0.1.0 – Public Alpha` |

The `dev` and `main` branches and the `v0.1.0-alpha.16.3.4` tag pointed to `9006c652` when this page
was reviewed. Always verify the live branches and releases before preparing another publication.

## What Redux is

Redux is a community fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). It keeps the
proven package, profile, and load-order foundation while building a more cohesive Windows
application around organization, review, recovery, customization, and accessibility.

The current public alpha includes:

- categories, separators, filtering, configurable columns, and a resizable mod-details drawer;
- explicit working changes, saved-order comparison and history, review-before-sync, restore points,
  and bounded Undo/Redo;
- built-in Mod Diagnostics and a separately enabled, preview-first Load Order Advisor;
- a shared Download Manager for local packages and optional NXM downloads;
- Save Game Manager and a guarded Game-Directory Mod Manager;
- dark, light, parchment, and custom themes with accessibility and motion controls;
- conservative offline mod recognition and privacy-limited contribution reports; and
- safe in-app updates for existing portable installations.

See [Changes from upstream BG3 Mod Manager](CHANGES_FROM_UPSTREAM.md) for the complete feature
comparison.

## Repository layout

Keep new files with the part of the project that owns them:

| Location | What belongs there |
|:--|:--|
| Repository root | Git configuration, the main `README.md`, the project `LICENSE`, solution/build entry points, and shared MSBuild configuration |
| `docs/` | Current user, mod-author, contributor, security, support, and release-process guides |
| `docs/releases/` | Historical notes for one published version |
| `docs/assets/` | Public documentation and Nexus page media that must have stable repository URLs |
| `docs/schemas/` | Public schemas documented for creators or contributors |
| `licenses/` | Third-party license texts and the third-party notices inventory |
| `src/` | Application, core, updater, and toolbox source |
| `tests/` | Automated regression projects and their fixtures |
| `tools/` | Standalone maintainer and database utilities, with tool-specific documentation beside them |
| `.github/` | Issue forms, pull-request templates, and GitHub Actions workflows |
| `External/` | Attributable upstream or vendored dependencies and submodules |

The root `LICENSE` is intentionally separate from `licenses/`: it covers Redux itself and is the
standard location recognized by GitHub. The files under `licenses/` cover bundled third-party work.
Likewise, `src/GUI/Redux.ico` is the Windows application resource, while
`docs/assets/redux-star.svg` is the reusable public documentation mark.

Generated ZIPs, manifests, build directories, logs, user settings, downloaded packages, local
reports, and promotional working files are outputs rather than source. They stay ignored and must
not be committed. A tool-specific README can remain beside its tool; general project guidance
belongs in `docs/` and should be linked from the documentation index.

## Decisions that are already settled

### Installation and updates

- Public Redux builds are portable-only. The old Setup experiment is retired and is not a release
  artifact.
- A fresh installation is a complete ZIP extraction into a writable folder such as
  `C:\Modding\Redux`.
- The built-in updater updates an existing portable folder. It is not a fresh installer and must
  never claim user-created state.
- GitHub Releases and Nexus Mods receive the same approved ZIP. The moving update-channel manifest
  is published only after the versioned package is available and verified.
- The public executable is `Redux.exe`. `BG3ModManager.exe` is a legacy private-alpha name and must
  not appear at the root of a current release.
- Application folders, Redux state, BG3 Mods, profiles, saves, retained archives, managed
  game-directory files, and provider credentials are separate ownership domains.

Current installation and removal instructions are in [Installation and updates](INSTALLATION.md).
Older release notes are historical records and may describe artifacts that are no longer offered.

### Versioning and announcements

- Published ZIPs are immutable. A corrected build receives a higher version rather than replacing
  different bytes under an existing version.
- Two-part alpha updates such as `.16.4`, `.16.5`, and `.16.6` are normal announced releases.
- Three-part updates such as `.16.3.4` or `.16.4.1` are quiet maintenance releases. Their GitHub
  notes include `<!-- redux:no-announce -->` so the Redux Helper Bot records the version without
  posting or pinging.
- A quiet maintenance release is still a normal Latest GitHub release, Nexus upload, and update
  channel target.
- The next announced alpha is not assumed to be ready merely because a higher version number has
  appeared in a local filename or build folder.

The full publishing and recovery contract is in
[Release process and update recovery](PUBLIC_ALPHA_RELEASES.md).

### Mod intake, placement, and source identity

- A clean new PAK install enters Inactive Mods. Updating or reinstalling an installed PAK preserves
  its active or inactive state and load-order position.
- Downloading, inspecting, retaining, reinstalling, or importing a Modlist never silently
  activates, reorders, saves, or syncs a mod.
- Source recognition is conservative. When evidence is not strong enough, a mod remains **Local**;
  a similar filename or title is not enough to assign Nexus Mods or mod.io.
- Nexus Mods and mod.io are both valid sources. Users can link, change, or unlink supported source
  pages explicitly.
- Creator manifests and contribution reports provide evidence for review. They do not override
  parsed package identity or authorize automatic database changes.
- Categories and separators are Redux presentation data and never enter `modsettings.lsx`.
- Separators belong to Active Mods. Invalid drops must be rejected cleanly without leaving a drag
  marker behind.

### Saving, syncing, diagnostics, and advice

- **Save** updates the selected Redux saved order. **Sync Load Order to Game** is a separate,
  reviewed write to `modsettings.lsx`.
- Mod Diagnostics is built-in and read-only. It explains known package facts but does not repair,
  download, remove, activate, reorder, or sync anything.
- Load Order Advisor is optional and experimental. Its organizer is deterministic, preview-first,
  separator-aware, undoable, and unsaved until the user chooses to save.
- Unknown relationships remain unknown. Guidance must not imply certainty that the available data
  does not support.
- Redux Modlists can carry order and selected presentation data, but never PAKs, saves, profiles,
  credentials, or `modsettings.lsx`. Importing source links remains opt-in so existing associations
  are preserved by default.

### Native and game-directory mods

- Game-Directory Mod Manager accepts only reviewed layouts and shows the destinations it will
  change.
- Unknown or modified DLL layouts stay unverified and unmanaged.
- Redux may adopt an exact reviewed add-only plugin without rewriting it. It never adopts an
  external replacer because Redux did not preserve the original game files.
- Layout recognition is not a malware scan or a publisher signature. Users remain responsible for
  trusting the source of native code.

### Interface and visual system

- Shared semantic resources, modal chrome, menu behavior, buttons, and icons are preferred over
  one-off window styling.
- Theme colors must update live. Icons inherit the appropriate semantic or accent color unless a
  provider-specific interaction deliberately supplies one, such as Discord or Nexus hover color.
- Primary, neutral, destructive, success, warning, information, provider, and category actions
  retain distinct meaning across themes.
- Buttons use consistent spacing, icon placement, focus states, disabled states, and labels.
- Windows and popups must remain usable at common laptop resolutions, Windows scaling levels, text
  sizes, and reduced-motion settings without clipped actions or unreachable content.
- The transparent Redux star in `docs/assets/redux-star.svg` is the canonical mark. New copies of
  the same asset should not be scattered through the repository.
- Public screenshots and documentation show the real application with real data. Synthetic paths,
  fake identifiers, and UI that does not match the current build are not used as product evidence.

## Current reports and planned work

The issue tracker is the live source. At the time of this review, the public reports still needing
attention were:

- [#112 — Remember Window Position can produce a blank app on the next start](https://github.com/circleainn/BG3ModManager-Redux/issues/112)
- [#95 — Elevation warning can appear unexpectedly](https://github.com/circleainn/BG3ModManager-Redux/issues/95), currently awaiting more reproduction information

Accepted or proposed additions include:

- [#113 — Streamline windows, warnings, and explanatory text](https://github.com/circleainn/BG3ModManager-Redux/issues/113)
- [#111 — Separators and orders for Inactive Mods](https://github.com/circleainn/BG3ModManager-Redux/issues/111)
- [#110 — Improve compatibility with Wine and Linux desktops](https://github.com/circleainn/BG3ModManager-Redux/issues/110)
- [#109 — Manage Override mods when switching saved load orders](https://github.com/circleainn/BG3ModManager-Redux/issues/109)
- [#108 — Explore Nexus Collections support](https://github.com/circleainn/BG3ModManager-Redux/issues/108)
- [#98 — Add official Nexus Mods SSO account connection](https://github.com/circleainn/BG3ModManager-Redux/issues/98)
- [#63 — Explore a docked or paged Managers workspace](https://github.com/circleainn/BG3ModManager-Redux/issues/63)
- [#56 — Expand localization and accessibility support](https://github.com/circleainn/BG3ModManager-Redux/issues/56)

An open issue is not permission to broaden its scope. Confirm its current discussion, labels, and
acceptance state before implementing it.

## Current limits and non-goals

- Nexus authentication currently uses a personal API key; official SSO remains planned work.
- Windows 10/11 x64 is the supported platform. Wine may work for some users, but Linux, macOS,
  Wine, and Proton are not currently supported release targets.
- Provider, category, dependency, and advisor knowledge is intentionally incomplete rather than
  filled with guesses.
- Redux does not promise automatic compatibility repair or a universally correct load order.
- Nexus Collections, automatic management of inactive Override mods between orders, and a new
  Managers workspace are not current shipped features.
- The retired Setup project and its dedicated tests are no longer retained in the repository.
  Installer work should not be reintroduced without a new decision about distribution and signing.

## Before changing established behavior

1. Read this page, the relevant user guide, and the current issue discussion.
2. Confirm the behavior is not an intentional safety or ownership boundary.
3. Check the current source, tests, branch state, and release rather than relying on an old build or
   document.
4. Report newly found problems with evidence before folding unrelated fixes into an active task.
5. Keep implementation changes focused. Do not publish releases, move update channels, close
   issues, or change public services unless that work is explicitly part of the task.
6. Preserve unrelated local changes and submodule state.

## Which source wins

When two references disagree, use this order:

1. current source and tests for actual application behavior;
2. current release files and update manifest for public distribution;
3. this page for accepted product decisions and boundaries;
4. focused guides for user and contributor procedures;
5. GitHub issues for active reports and proposals;
6. the changelog and release notes for historical behavior.

Update this page when a release changes the current product contract, a settled decision changes,
or an issue listed here is resolved or accepted. Keep release-by-release detail in the changelog
instead of turning this page into another release history.
