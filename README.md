<div align="center">

# Baldur's Gate 3 Mod Manager Redux

**A more visual, deliberate way to organize Baldur's Gate 3 mods.**

[![Current build](https://img.shields.io/badge/build-0.1.0--alpha.13-9A7BFF?style=flat-square)](https://github.com/circleainn/BG3ModManager-Redux/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-4F86F7?style=flat-square)](#requirements-and-alpha-status)
[![License](https://img.shields.io/badge/license-MIT-42A66F?style=flat-square)](LICENSE)
![Discord — coming soon](https://img.shields.io/badge/Discord-Coming_Soon-5865F2?style=flat-square&logo=discord&logoColor=white)

<br>

<a href="https://ko-fi.com/circleain"><img alt="Support me on Ko-fi" src="https://img.shields.io/badge/Support_me_on_Ko--fi-FF5E5B?style=for-the-badge&amp;logo=ko-fi&amp;logoColor=white"></a>

<br>

[Download on Nexus Mods](https://www.nexusmods.com/baldursgate3/mods/23799) ·
[Report a problem](https://github.com/circleainn/BG3ModManager-Redux/issues) ·
[Browse the docs](docs/README.md) ·
[See what differs from BG3MM](docs/CHANGES_FROM_UPSTREAM.md)

</div>

> [!IMPORTANT]
> Redux is in early development. Keep independent backups of important profiles, saves, downloaded
> archives, and the BG3 Mods folder. Always review proposed load-order changes before applying them.

Redux is a Windows mod manager built on
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). It preserves
BG3MM's proven package, profile, and load-order foundation while adding a cohesive interface,
stronger organization, safer review workflows, and optional offline-assisted guidance.

## Redux at a glance

| Organize | Review | Personalize |
|:--|:--|:--|
| Multiple categories per mod | Built-in package diagnostics | Dark, Light, and Parchment themes |
| Named, collapsible separators | Optional Load Order Advisor | Custom themes and generated gradients |
| Saved orders and comparisons | Export previews and restore points | Adjustable text, fonts, icons, and motion |
| Searchable <kbd>Ctrl</kbd> + <kbd>Q</kbd> actions | Undo/Redo for reversible changes | Unified hover cards and details drawer |

### The main workflow

1. **Install and inspect.** Drop PAKs or supported archives into Active or Inactive Mods. Redux
   previews new installs, updates, same-version replacements, and possible downgrades first.
2. **Organize without losing intent.** Assign categories, create separators, move mods, and use
   <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> for reversible edits.
3. **Save deliberately.** Working changes do not overwrite the selected saved order until **Save**
   is pressed. Closing with unsaved changes requires confirmation.
4. **Review the game change.** **Sync Load Order to Game** shows what will activate, deactivate, or
   move before Redux writes `modsettings.lsx`.

## What Redux adds

### Categories, separators, and mod details

- Automatic and custom categories with names, descriptions, colors, icons, ordering, and filtering.
- Up to three visible category assignments per mod.
- Separators with persistent membership and collapse state. Closed separators move with their
  contained mods and do not absorb nearby rows unexpectedly.
- A resizable details drawer and hover cards for descriptions, requirements, files, changelogs,
  source pages, diagnostics, and private notes.
- Configurable list columns and unified selection between Active and Inactive Mods.

Categories, separators, and notes are Redux presentation data. They never enter the game's
`modsettings.lsx`.

### Diagnostics and Load Order Advisor

Mod Diagnostics is built into Redux. It brings facts already detected by BG3MM's package parser—
including dependencies, UUID problems, overrides, Mod Fixer behavior, and Script Extender
requirements—into consistent row indicators, hover details, the mod drawer, and review windows.
Diagnostics are read-only: they do not download, repair, remove, activate, or reorder mods.

The **Load Order Advisor** is the optional, experimental layer. When enabled, it adds cautious
placement checks based on exact package declarations and Redux's offline ordering knowledge.
**Organize Active Load Order** can preview one of three policies:

- preserve current separators and sort only within them;
- replace them with non-empty suggested separators; or
- remove active separators and organize the full numbered list.

Nothing is applied silently. The preview shows mod moves, actual separator changes, and
relationships that need review; unchanged preserved separators are left out of the change count.
Applying it creates one undoable, unsaved edit, and individual recommendations can be ignored and
restored later.

### Safer load-order changes

- Explicit working state with an unsaved indicator and close protection.
- Named order creation, renaming, deletion, comparison, and history.
- Bounded Undo/Redo for activation, deactivation, movement, separators, organizer changes, and
  guarded game-file changes.
- Staged and validated writes for saved orders, settings, imports, backups, and `modsettings.lsx`.
- Per-profile restore points before confirmed game changes.
- External-change protection: Redux will not undo over a game file changed afterward by BG3 or
  another manager.

### Save Game Manager

Open **Tools > Save Game Manager...** or use the **Save Games** toolbar group. Redux groups story
saves by campaign and shows available thumbnails, dates, sizes, and difficulty metadata. Honour
campaigns receive a gold crown; Tactician campaigns receive a skull badge. Campaign collapse state
is remembered, and its expand/collapse motion follows the Reduce Motion preference.

Redux accepts a save folder, loose `.lsv`, or supported ZIP, 7z, RAR, TAR, or GZip-family archive
through the picker or drag and drop. Save drops receive a distinct review so they cannot be confused
with mod installation. Archive paths and sizes are checked, imports are staged, existing names
require confirmation, and deletion uses the Windows Recycle Bin.

> [!NOTE]
> Redux does not edit or validate save contents. Close BG3 before changing saves, keep independent
> backups, and remember that Steam Cloud may restore files removed locally.

### Redux Modlists

A `.bg3redux` Modlist can carry a saved order plus selected Redux presentation data:

- category definitions, descriptions, assignments, and display order;
- separators, descriptions, positions, membership, and collapse state;
- reusable custom PNG icons;
- public Nexus Mods or mod.io source references; and
- private notes only when explicitly included.

Import and export previews show what will change. A Redux Modlist never contains installed PAKs,
profiles, saves, API keys, or `modsettings.lsx`, and importing one does not install missing mods.
Source-link import is off by default so a recipient's existing provider association is preserved.

**Back Up Active Mods to ZIP** is a separate personal-backup feature. It asks where to save and
reminds users that redistributing mod files requires permission from every relevant author.

## Offline mod recognition

Redux includes a curated offline database that connects exact package fingerprints and reviewed
module identities to Nexus Mods projects. Matching is intentionally conservative: uncertain mods
remain **Local** rather than being assigned a potentially incorrect source. The same database also
contains exact dependency and ordering facts used only when Load Order Advisor is enabled.

Use **Tools > Generate Redux Database Contribution...** to create a privacy-limited
`.bg3redux-report`. It contains sanitized mod identity, known provider IDs, and exact PAK
fingerprints for maintainer review. It does **not** include packages, profiles, load-order positions,
settings, API keys, notes, or private filesystem paths, and generating it changes nothing locally.

If you would like to help improve recognition, attach only the generated report—not archives or
PAKs—to an [issue](https://github.com/circleainn/BG3ModManager-Redux/issues). Reports are reviewed;
they are never imported into the bundled database automatically. The full trust and contribution
model is documented in the [Redux mod database guide](docs/REDUX_MOD_DATABASE.md).

## Themes and accessibility

- Redux Dark, Redux Light, Parchment, and importable custom themes.
- Solid semantic action colors or theme-generated gradients.
- Compact, Default, and Large text with bundled or imported `.ttf` / `.otf` fonts.
- Optional category-colored interactions, colored text, icons, and icon-only labels.
- Reduce Motion for scrolling, sliding, scaling, and animated transitions while preserving clear
  hover and selection states.
- Independently removable background blur and dimming.
- Configurable shortcuts, keyboard-operable dialogs, selectable dialog text, screen-reader helpers,
  and inherited speech commands.

The first launch provides one setup window for theme, optional source linking, optional Load Order
Advisor guidance, API keys, and accessibility choices. Optional online and advisor features begin
disabled. Provider keys are masked, encrypted for the current Windows account, and excluded from
ordinary settings and diagnostic exports.

## Built on BG3 Mod Manager

Redux is a fork, not a from-scratch replacement. It retains substantial work from LaughingLeader
and other BG3MM contributors, including:

- profiles, campaigns, active/inactive lists, saved orders, and the core load-order model;
- PAK and archive handling through LSLib;
- BG3 path detection, launch workflows, overrides, dependency parsing, Osiris and Mod Fixer
  detection, and Script Extender integration;
- Nexus Mods integration, caching, metadata, images, links, and update foundations; and
- configurable shortcuts, developer utilities, CrossSpeak, Windows speech fallback, and
  screen-reader support.

Redux reworks and extends many of these systems while retaining their credit. See
[Changes from upstream BG3 Mod Manager](docs/CHANGES_FROM_UPSTREAM.md) for a precise comparison.

## For mod authors

**Tools > Inspect Mod Package** performs a read-only release preflight on a PAK or common release
archive. It reviews module identity, declared dependencies, embedded creator metadata, Script
Extender and Osiris signals, override behavior, and common development debris without installing
or modifying the package. A clean result is not a guarantee of in-game compatibility.

Authors may also include an optional root-level
[`redux.mod.json`](docs/REDUX_CREATOR_MANIFEST.md) inside a PAK. Redux validates its module claim
against parsed `meta.lsx` data before using it for Nexus Mods or mod.io identification. Invalid
claims are ignored and reported without changing packages or load orders.

## Requirements and alpha status

- Windows 10 or Windows 11, x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Baldur's Gate 3

Linux, macOS, Wine, and Proton are not supported. Redux is framework-dependent and is not
distributed as a self-contained build.

Known private-alpha limits include personal Nexus API-key authentication instead of public SSO,
incomplete provider/category/dependency coverage, imported-font variability, incomplete mod.io
author links, and limited clean-machine testing. Uncommon scaling and extremely dense layouts may
still expose visual issues. Imported fonts and PNG icons remain the user's responsibility to license.

## Documentation

| Guide | Audience | Purpose |
|:--|:--|:--|
| [Documentation index](docs/README.md) | Everyone | Find the right user, author, or maintainer guide |
| [Changes from upstream](docs/CHANGES_FROM_UPSTREAM.md) | Users and contributors | Understand what Redux retains and changes |
| [Optional features](docs/REDUX_OPTIONAL_MODULES.md) | Contributors | Understand feature boundaries and safety rules |
| [Redux mod database](docs/REDUX_MOD_DATABASE.md) | Contributors and maintainers | Recognition, advisor knowledge, and reports |
| [Mod developer tools](docs/MOD_DEVELOPER_TOOLS.md) | Mod authors | Inspect releases before distribution |
| [Creator manifest](docs/REDUX_CREATOR_MANIFEST.md) | Mod authors | Add a validated source identity to a PAK |

## Reporting problems

Use the [issue tracker](https://github.com/circleainn/BG3ModManager-Redux/issues) for reproducible
bugs. Include the Redux version, the smallest reliable reproduction steps, relevant screenshots or
logs, and affected mod names or UUIDs. Never post API keys or unreviewed private path information.

## Credits and license

Redux exists because of [LaughingLeader's original BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager).
You can also [support LaughingLeader on Ko-fi](https://ko-fi.com/LaughingLeader).

Bundled dependencies and assets include LSLib, CrossSpeak, AdonisUI, ReactiveUI,
GongSolutions.WPF.DragDrop, Lucide, and the bundled open fonts. Attribution and full terms are in
[Third-Party Notices](licenses/Third-Party-Notices.md).

Baldur's Gate 3 is developed and published by Larian Studios. Redux is an unofficial community
project and is not affiliated with or endorsed by Larian Studios, Nexus Mods, or mod.io.

The original project and Redux modifications are distributed under the [MIT License](LICENSE),
subject to all retained copyright and third-party notices.
