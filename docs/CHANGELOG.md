# Changelog

This file summarizes user-visible Redux releases. The issue tracker and Git history remain the
source for individual implementation details.

## Unreleased — public-alpha preparation

- Public installation, migration, rollback, removal, privacy, support, contribution, security, and
  release-maintenance documentation.
- Clean-machine, public update-channel, and distribution validation remain required before Redux
  leaves private alpha.

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
- Shared window chrome, modal transitions, focus restoration, deletion review, manager cards,
  semantic pills, provider actions, and disabled-state presentation.
- Override separator appearance, persistent membership behavior, bulk collapse/expand controls, and
  category-driven icon/color customization.
- Bundled typography choices: Manrope, Atkinson Hyperlegible, Archivo Black, IBM Plex Mono, and the
  Windows Segoe UI fallback.
- Reduce Motion coverage for lists, campaigns, windows, hover effects, and transitions.
- Live custom-theme preview and Ctrl+L theme cycling performance through coalesced, incremental
  palette updates.

### Fixed

- Download shutdown false positives, restart crashes, stale queue state, incomplete metadata, and
  foreground behavior.
- Save campaign expansion crashes and several modal close/focus issues.
- Deletion selection and disabled semantic-icon inconsistencies.
- Stuck drag/drop overlays and incomplete drop-review thumbnails.
- Hidden-toolbar parity, hover-card tag duplication, and shortcut chevron state.

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
