# Changes from upstream BG3 Mod Manager

BG3 Mod Manager Redux is a Windows-only fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). Redux depends on
the upstream project's mature BG3 package and load-order foundation; it is neither a clean-room
rewrite nor an attempt to obscure that lineage.

This page documents **durable product and architectural differences**. It is not a changelog or a
claim that every visible behavior originated in Redux.

> [!NOTE]
> “Retained” means Redux continues to build on upstream behavior. “Extended” means Redux adds a
> guarded workflow or presentation layer around that behavior. “Redux-owned” identifies a system
> introduced for this fork.

## At a glance

| Area | Upstream foundation retained | Redux difference |
|:--|:--|:--|
| Mod management | PAK parsing, archives, active/inactive lists, profiles, campaigns | Structured drop review, safer staged replacement, unified selection and presentation |
| Load orders | Editing, saved orders, `modsettings.lsx` import/export | Explicit unsaved working state, review-before-write, restore points, comparisons, Undo/Redo |
| Organization | List ordering and filtering | Multi-category organization and persistent visual separators |
| Mod information | Nexus metadata, package metadata, dependencies, overrides | Unified diagnostics, mod.io/manual provenance, offline recognition, richer details surfaces |
| Guidance | Parsed dependency facts | Optional Load Order Advisor and separator-aware organization previews |
| Portability | Existing order formats | `.bg3redux` Modlists for selected Redux presentation and public source data |
| Saves | Profile and save-path discovery | Profile-aware Save Game Manager with campaign grouping and guarded imports |
| Interface | Existing WPF application and accessibility foundations | Redux design system, themes, custom appearance, Quick Access, motion controls |

## The upstream core Redux preserves

Redux continues to rely on upstream systems for:

- profiles, campaigns, active and inactive lists, saved orders, and normal list editing;
- `modsettings.lsx` and supported load-order import/export;
- PAK metadata parsing and archive workflows through LSLib;
- BG3, profile, save, order, and Mods-folder discovery;
- override and force-loaded packages, dependency metadata, UUID checks, Osiris/Mod Fixer detection,
  and Script Extender integration;
- game launch behavior and inherited update-provider foundations;
- Nexus Mods metadata, caching, images, and links; and
- configurable shortcuts, speech, and screen-reader foundations.

Unless a change is explicitly scoped and tested, Redux preserves these semantics and established
user-data locations. Upstream copyright, attribution, license terms, and third-party notices remain
intact.

## Redux-owned interface and organization

Redux introduces a shared visual system across its main window and dialogs:

- semantic success, warning, error, information, and accent colors;
- consistent typography, spacing, corner radii, controls, menus, tooltips, window chrome, and
  notifications;
- Redux Dark, Redux Light, Parchment, and persistent custom themes;
- optional generated gradients, imported fonts, scalable text, and reusable custom PNG icons;
- a reorganized toolbar and searchable Quick Access menu;
- richer hover information and a persistent selected-mod details drawer; and
- a unified Lucide-based icon language while preserving official provider branding where relevant.

Redux list surfaces retain virtualization and logical scrolling while applying bounded render-only
wheel motion. Reduce Motion removes scrolling, sliding, scaling, and transition animation without
removing clear hover or selection feedback.

### Categories

Redux categories are a persistent presentation layer with automatic and user-created categories,
multiple assignments per mod, names, descriptions, colors, icons, display order, filtering, counts,
and category-aware selection. Missing or removed assets have explicit fallback behavior.

### Separators

Redux separators are named visual markers with descriptions, colors, icons, durable membership,
and persistent collapse state. They never enter `modsettings.lsx` and are never treated as mods.

- An expanded separator moves only its marker.
- A collapsed separator moves with its sealed contents.
- Moving a closed group does not absorb unrelated destination rows.
- Rows placed next to a closed separator remain visible until the group is expanded.

## Deliberate load-order workflow

Redux extends upstream load-order editing with an explicit working state:

- edits remain unsaved until the user presses **Save**;
- closing or replacing a dirty working order requires confirmation;
- saved orders can be created, renamed, deleted, compared, and opened in their folder;
- bounded Undo/Redo covers activation, deactivation, movement, separators, organizer changes, and
  guarded writes to the game order;
- **Sync Load Order to Game** previews activations, deactivations, placement changes, automatically
  included dependencies, and relevant diagnostics before writing;
- per-profile restore points are created before confirmed writes and can also be created on demand;
  and
- an undo refuses to overwrite `modsettings.lsx` if BG3 or another manager changed it afterward.

Redux Modlists add a portable `.bg3redux` format for a saved order and user-selected category,
separator, custom-icon, public-source, and optional-note data. Import choices remain independent.
Bundles are validated and never contain installed PAKs, saves, profiles, API keys, or
`modsettings.lsx`.

## Diagnostics and optional guidance

Redux's built-in Mod Diagnostics unifies package facts that were previously scattered across
different UI paths. It reports detectable dependency, UUID, Script Extender, creator-manifest,
declared-conflict, Mod Fixer, override, and provider-safety conditions through consistent severity
and follow-up actions.

Diagnostics are read-only. A user may explicitly reveal or activate an installed dependency, open
a reviewed source, or copy a UUID, but Redux does not silently download, repair, remove, activate,
or reorder mods.

The opt-in **Load Order Advisor** is a separate experimental rule family. It combines exact package
declarations with reviewed offline dependency and ordering facts. Its organizer can preserve current
separators, create non-empty suggested separators, or remove separators. Every result is previewed;
the preview counts only separators that it creates, removes, or actually repositions. Applying it is
one undoable, unsaved action. Advice can be ignored per relationship without disabling other
diagnostics.

An on-demand **Active File Overlaps** inspector also reports shared internal paths across active and
override PAKs. It describes possible interactions, not confirmed conflicts.

## Source identity and offline knowledge

Redux extends provider metadata with explicit provenance states: manual, native, cached,
creator-supplied, reviewed-database, and Local. Resolution is conservative and honors manual user
choices. Provider metadata cannot replace a package's parsed identity or change its load order.

The bundled database supports:

- exact installed-PAK and downloaded-archive fingerprints;
- reviewed module identities;
- corroborated community identity candidates;
- reviewed missing-dependency source links; and
- exact dependency aliases, substitutes, ordering groups, and author-supplied placement facts.

Online Nexus Mods and mod.io information can be disabled without deleting cached associations.
Privacy-limited `.bg3redux-report` contributions omit credentials, private paths, profiles, notes,
settings, and load-order data and still require maintainer review.

## Save Game Manager

Redux adds a profile-aware save browser over upstream path discovery. It groups story saves by
campaign, uses available WebP thumbnails and `SaveInfo.json` difficulty metadata, remembers campaign
collapse state, and provides compact toolbar actions.

Save folders, loose LSV files, and supported ZIP/7z/RAR/TAR/GZip-family archives can be installed
through a picker or drag and drop. Save and mod drops use distinct structured reviews; mixed drops
are rejected rather than guessed. Imports are staged, unsafe or oversized archive content is
rejected, existing names require confirmation, and deletion uses the Windows Recycle Bin. Redux
does not modify save contents or include saves in portable Modlists.

## Filesystem and privacy hardening

Redux applies staged, validated, or atomic replacement to settings, saved orders, imports,
`modsettings.lsx`, keybindings, provider caches, Script Extender configuration, and active-mod ZIP
backups. Same-destination writes are serialized so overlapping operations cannot expose partial
content. Update and deletion results are reported only after the underlying filesystem action
succeeds.

Provider API keys are stored outside ordinary settings using Windows account-protected storage.
Diagnostic exports and release packaging checks reject credentials and other private runtime data.

## Accessibility additions

Redux retains upstream speech and screen-reader support while adding:

- a top-level Accessibility menu and first-run setup;
- Atkinson Hyperlegible, other bundled fonts, imported fonts, and adjustable text size;
- keyboard-operable Redux dialogs and a rebuilt shortcut editor;
- selectable dialog text and consistent focus behavior;
- lightweight automation for realized rows in large virtualized lists; and
- independent reduced-motion and reduced-background-effects preferences.

## Targeted upstream corrections

Redux includes focused fixes for confirmed inherited problems, including large-archive memory use,
blank-profile startup, saved-order path mismatches, stuck drag/toolbar state after failures,
deleting already-missing PAK entries, Script Extender version comparison, refresh with unsaved
changes, and startup failures without useful feedback.

The upstream tracking discussion is
[issue #11](https://github.com/circleainn/BG3ModManager-Redux/issues/11).

## Current boundaries and non-goals

Redux currently does not provide:

- public Nexus SSO authentication;
- automatic Redux self-updating during the private alpha;
- automatic downloading, compatibility repair, or conflict resolution;
- silent load-order sorting or automatic game-file export;
- application localization;
- Linux, macOS, Wine, or Proton support; or
- a self-contained .NET distribution.

This document should change only when an enduring upstream/Redux boundary changes. Version notes,
individual fixes, plans, and one-off implementation details belong in release notes, issues, or Git
history.
