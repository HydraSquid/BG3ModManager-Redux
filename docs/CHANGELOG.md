# Changelog

This file summarizes user-visible Redux releases. The issue tracker and Git history remain the
source for individual implementation details.

## Unreleased

- Added an explicit public-alpha hotfix version contract (`alpha.N.H`) so corrected builds can be
  delivered through the updater without pretending to be a new feature alpha or replacing existing
  release bytes.
- Prevented an exceptional load-order rebuild during Sync—such as one involving legacy separator
  PAK state—from leaving Save, keyboard shortcuts, menus, and drag-and-drop permanently disabled.

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
