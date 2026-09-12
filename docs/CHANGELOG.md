# Changelog

This file summarizes user-visible Redux releases. The issue tracker and Git history remain the
source for individual implementation details.

## Unreleased

### Changed

- Add subtle alternating table-row backgrounds, including mod lists, using the active theme's
  text color. Dark, Light, Parchment, and custom themes share the same treatment; hover and
  selection retain their existing highlights.

## 0.1.0-alpha.16.3.5 — silent public-alpha maintenance release

### Changed

- Same-version reinstalls keep their placement and skip the clean-package review. Batch rows
  describe each package's action, and PAK counts sit with the other download metadata.
- Show a What's New window with GitHub release notes after an update.
- Prevent another Redux copy from starting for the same user session, including other versions;
  NXM links are routed to the running instance when it supports shared activation.
  Older releases cannot enforce this guard when launched after an updated copy.
- Show modal package progress throughout Install All, including preparation, and prevent
  conflicting workspace actions until the batch finishes. Download Manager's toolbar icon uses
  semantic success, warning, and error colors with explanatory tooltips.
- Nexus links can start downloads with a small notification without opening Download Manager
  or taking focus. Quiet handling is the default for new settings; saved foreground preferences
  are preserved. A quiet NXM launch starts Redux minimized.
- Download toasts appear above the taskbar for starts and completions, then fade away after
  four seconds. Click a toast to open Download Manager; reduced motion disables the fades.
- Remove routine download and clean new-mod installation confirmations. Updates, replacements,
  and packages requiring review still prompt before installation. Explain missing Nexus setup
  separately from Windows NXM link association.
- Simplify the update window with scrolling details and a consistent action footer, and avoid
  repeating its results in notification banners.
- Shorten onboarding explanations and let shared confirmation dialogs resize and wrap their
  actions when space is limited.

### Fixed

- Allow Download Manager to bring a maximized Redux window forward after a quiet startup.
- Read administrator elevation from the primary process token rather than a possible thread
  impersonation token, and log the token elevation type to help investigate unexpected warnings.
- Keep secondary-window content transparent through dismissal and prevent repeated or canceled
  close requests from starting competing transitions.
- Restore remembered window bounds on-screen, including monitors to the left of or above the
  primary display, and recover safely when saved bounds are unusable or a monitor is disconnected.
- Keep Save Game Manager discovery working when a save has an invalid package signature.
- Import loose saves from an archive into separate folders with only their matching thumbnails.
- Clean up eligible abandoned update folders for maintenance versions as well as single-part alphas.

## 0.1.0-alpha.16.3.4 — silent public-alpha maintenance release

### Fixed

- Kept an installed PAK mod's active or inactive state and load-order position when updating it
  through Download Manager. Newly installed mods still enter Inactive Mods.
- Made **Don't show again** on the administrator warning persist after restarting Redux.

### Changed

- Nexus uploads now include a short list of changes taken from the matching GitHub release notes.

## 0.1.0-alpha.16.3.3 — silent public-alpha maintenance release

- Let users deliberately reassociate NXM links with the current Redux when an older Redux install
  or another marked handler owns the Windows registration, while preserving the previous handler.
- Simplified repetitive Download Manager guidance.
- Corrected the current-theme summary to show an active custom theme's own name.
- Added the Redux star to the built-in icon library and used its theme-aware monochrome form on the
  startup screen.
- Gave the NXM reassociation action the same Nexus source styling used elsewhere in Redux.

## 0.1.0-alpha.16.3.2 — silent public-alpha maintenance release

- Prevented separators from being dragged into Inactive Mods.
- Cleared the insertion line immediately when Inactive Mods rejects a separator drag or drop.

## 0.1.0-alpha.16.3.1 — silent public-alpha maintenance release

### Fixed

- Removed the generic mod.io health warning because a linked source page does not prove that the
  installed package is subscribed through the in-game manager or managed by Steam Cloud.
- Added maintenance-release versions such as `.16.3.1` across the updater and Nexus publication
  contract, including deterministic ordering between their parent and the next hotfix.

## 0.1.0-alpha.16.3 — public-alpha hotfix

### Fixed

- Prevented an incidental mod.io `PublishHandle` from overriding a reviewed Nexus match when Redux
  is introduced to an existing mod installation.
- Added symmetric source controls: explicit Nexus/mod.io labels, persistent mod.io unlinking, and a
  **Change Source to Nexus Mods** action for mods currently displaying mod.io metadata.
- Expanded Redux's offline source catalog while refusing cross-provider and
  same-provider name collisions instead of guessing.
- Added conservative bundled mod.io recognition that requires a matching UUID and exact package
  name, folder, or filename and never overrides native or manually selected provenance.

## 0.1.0-alpha.16.2 — public-alpha hotfix

### Fixed

- Preserved the exact selected Nexus file name in the mod list when several files belong to the
  same Nexus page, while retaining the shared project title in source details.
- Added manual mod.io source linking, replacement, and clearing with URL/ID validation and explicit
  confirmation when replacing an existing Nexus link.

### Distribution

- Simplified public releases to one portable ZIP shared byte-for-byte by GitHub Releases and Nexus
  Mods. The separate Setup artifact is no longer part of future public releases.
- Added a protected Nexus Mods publishing workflow that downloads the approved GitHub release ZIP,
  refuses duplicate versions, uploads through Nexus Mods' official action, and records the resulting
  file-version ID.

## 0.1.0-alpha.16.1 — public-alpha hotfix

### Fixed

- Added an explicit public-alpha hotfix version contract (`alpha.N.H`) so corrected builds can be
  delivered through the updater without pretending to be a new feature alpha or replacing existing
  release bytes.
- Prevented an exceptional load-order rebuild during Sync—such as one involving legacy separator
  PAK state—from leaving Save, keyboard shortcuts, menus, and drag-and-drop permanently disabled.

### Distribution

- Setup now updates or repairs its existing registered Redux installation using verified,
  inventory-scoped replacement and rollback while preserving settings and other user content.

## 0.1.0-alpha.16 — public-alpha hotfix

### Fixed

- Welcome Setup now stays within the available desktop work area, can be resized, and keeps its
  navigation actions reachable on common laptop displays and non-default scaling.
- Screen-reader speech now uses the bundled Tolk bridge directly and falls back to Windows SAPI
  when the optional CrossSpeak wrapper is unavailable.
- The elevated-process warning now uses the process token's actual elevation state, appears once
  per startup, and provides working **Close** and **Don't show again** actions.
- Top-bar menus, toolbar dropdowns, nested submenus, and combo boxes now prefer rightward placement,
  with left/up placement retained only when required by a screen edge.
- Cancelling the first-run BG3 folder picker no longer lets Welcome Setup overlap Preferences;
  onboarding waits until Preferences has fully closed.

### Distribution

- Enabled Redux's verified public-alpha update checks now that the moving channel is live.
- Added short-lived CI artifacts for both the portable application and standalone Setup so release
  candidates come from the same tested commit.

## 0.1.0-alpha.15 — public alpha

### Distribution and identity

- Separate lightweight web Setup for verified fresh installation, .NET 8 prerequisite handling,
  per-user shortcuts, and inventory-scoped removal that preserves unlisted content.
- Strict public-alpha update-channel checks with quiet background scheduling, explicit release
  presentation, verified staging, restart-based replacement, transaction rollback, and post-update
  status reporting.
- Release-owned file inventories that keep application updates separate from settings, saved
  orders, downloads, retained archives, logs, backups, and user-created content.
- The desktop runtime is now `Redux.exe` / `Redux.dll`; Setup and Windows continue to present the
  installed product as **BG3 Mod Manager Redux**, and Setup recognizes legacy private-alpha
  installations that still contain `BG3ModManager.exe`.
- New transparent Redux star application icon and consistent product identity across the app,
  installer, repository, documentation, and public showcase materials.
- Direct Redux Discord access from the application and public project navigation.

### Refined

- App-wide button and modal consistency, including current semantic styling, action icons, close
  affordances, and disabled states in Preferences, category/separator editors, and manager windows.
- Load Order History styling now matches the current Redux review and organizer surfaces.
- Custom Theme Editor and color-picker shells now participate in live background-color previews.
- Expanded, deduplicated built-in category icon library and Segoe UI as Parchment's default font.
- Override Mods once again has a usable vertical resize grip after its visual refresh.
- Public installation, migration, rollback, removal, privacy, support, contribution, security,
  release, brand, community, architecture, and FAQ documentation.

### Fixed

- Save Game Manager deletion no longer keeps the selected save locked by its own thumbnail preview.
- Game-directory mod detection again evaluates the configured live game directory instead of an
  unrelated showcase path.
- Script Extender requirements use one dedicated row indicator and one complete diagnostic detail,
  removing duplicate or incomplete warning tooltips.

## 0.1.0-alpha.14 — private alpha

### Added

- Unified Download Manager intake for local packages and optional Nexus Mod Manager links.
- Guarded destination routing for ordinary PAK mods, saves, and reviewed game-directory packages.
- Queue persistence, resumable Nexus transfers, concurrent download limits, retry/recovery states,
  installed history, and source-aware thumbnails.
- Guarded **Install All** planning with dependency-aware skips and per-item results.
- Optional, quota-limited Package Archive Library with content-addressed deduplication and
  placement-preserving PAK reinstall.
- Reviewed game-directory mod catalog, archive inspection, installed-file recognition, guarded
  adoption, ownership records, repair, removal, and replacer rollback protections.
- Script Extender installation, update, reinstall, and adoption through Game-Directory Mod Manager.
- Save Game Manager campaign metadata, thumbnails, difficulty presentation, and guarded save import.
- Full-window drag-and-drop intake with destination-independent routing.

### Refined

- Main toolbar grouping, overflow parity, Quick Access coverage, action semantics, icons, spacing,
  and responsive layout.
- Shared window chrome and theme-aware move/resize feedback, modal transitions, focus restoration,
  deletion review, manager cards, semantic pills and action icons, provider actions, and
  disabled-state presentation.
- Override separator appearance, persistent membership behavior, bulk collapse/expand controls, and
  category-driven icon/color customization.
- Bundled typography choices: Manrope, Atkinson Hyperlegible, Archivo Black, IBM Plex Mono, and the
  Windows Segoe UI fallback.
- Reduce Motion coverage for lists, campaigns, windows, hover effects, and transitions.
- Live custom-theme preview and Ctrl+L theme cycling performance through coalesced, incremental
  palette updates.
- Quick Access hover and selection rails, semantic shortcut badges, and cleaner responsive
  mod-list headers now follow the same interaction language as Categories and manager lists.

### Fixed

- Download shutdown false positives, restart crashes, stale queue state, incomplete metadata, and
  foreground behavior.
- Save campaign expansion crashes and several modal close/focus issues.
- Deletion selection and disabled semantic-icon inconsistencies.
- Stuck drag/drop overlays and incomplete drop-review thumbnails.
- Hidden-toolbar parity, hover-card tag duplication, and shortcut chevron state.
- Mod-list column headers now finish cleanly across the scrollbar gutter while vertical tracks
  begin below the header strip.

### Safety

- Downloading and package intake remain separate from installation, activation, ordering, and game
  sync.
- Replacer installs back up only verified clean game files; Redux never adopts an unknown or modded
  DLL as a clean restoration source.
- Completed files and retained archives are hash-validated before use, while credentials and signed
  URLs remain outside persistent queue and archive records.

## Earlier private alphas

Earlier builds established Redux categories and separators, saved-order isolation, review-before-
sync workflows, restore points, Undo/Redo, Mod Diagnostics, the optional Load Order Advisor, Redux
Modlists, offline recognition, contribution reports, creator manifests, package preflight, custom
themes, Quick Access, and the first Redux visual system. Consult Git history for per-build detail.
