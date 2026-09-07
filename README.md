<div align="center">

<img src="https://bg3mm-redux.com/assets/redux-square.png" alt="BG3 Mod Manager Redux" width="92">

# Baldur's Gate 3 Mod Manager Redux

**A more visual, deliberate way to organize Baldur's Gate 3 mods.**

[![Current build](https://img.shields.io/badge/build-0.1.0--alpha.14-9A7BFF?style=flat-square)](https://github.com/circleainn/BG3ModManager-Redux/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-4F86F7?style=flat-square)](#requirements-and-alpha-status)
[![License](https://img.shields.io/badge/license-MIT-42A66F?style=flat-square)](LICENSE)
[![Support on Ko-fi](https://img.shields.io/badge/Support-Ko--fi-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/circleain)
![Discord — Coming Soon](https://img.shields.io/badge/Discord-Coming_Soon-5865F2?style=flat-square&logo=discord&logoColor=white)

[Download on Nexus Mods](https://www.nexusmods.com/baldursgate3/mods/23799) ·
[Visit the website](https://bg3mm-redux.com) ·
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
| Context-aware <kbd>Ctrl</kbd> + <kbd>Q</kbd> Quick Access | Undo/Redo for reversible changes | Grouped shortcut editor and motion controls |

### The main workflow

1. **Install and inspect.** Drop a supported package anywhere on the main Redux window. Its unified
   install target inspects and routes the package; ordinary PAK mods always enter Inactive Mods
   after Redux previews new installs, updates, same-version replacements, and possible downgrades.
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
- Compact Active Mods controls can collapse or expand every separator at once. The same action can
  be assigned a shortcut, while individual and context-menu controls remain available.
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

Open **Tools > Save Game Manager...** or use the **Saves** toolbar group. Redux groups story
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

### Game-Directory Mod Manager

Open **Tools > Game-Directory Mod Manager...**, its **Mods & Campaign** toolbar shortcut, or **Quick Access** to
review supported native and root-level mods that install beside BG3 rather than into the ordinary
Mods folder. The action is also available in the shortcut editor if you want to assign your own key
combination. These packages stay outside the PAK load-order panes. Redux shows every managed
destination before applying a change and clearly warns that native DLLs execute inside the game.

The first alpha.14 catalog recognizes reviewed layouts for Native Mod Loader, WASD and camera
plugins, Achievement Enabler, Baldur's Priority, Improved Camera, Best of Hands, BG3WASD Camera
Follow, True Third-Person Camera, bg3fgvk, and Script Extender. Script Extender now uses the same
staged game-directory transaction and ownership record as the rest of the reviewed catalog. Mixed
native-and-PAK packages send their companion PAK through the normal inactive-mod import path.

Archives may be selected in the manager or dropped onto Redux. Installation is staged and bounded;
paths, layouts, reviewed DLL fingerprints, AMD64 DLL headers, prerequisites, the source archive, and
destination files are rechecked before commit. Exact fingerprints distinguish related projects that
share filenames and identify known versions; unknown or modified DLLs remain explicitly unverified.
Reviewed add-only plugins installed elsewhere can be adopted without rewriting them, after which
Redux removes only unchanged adopted DLLs. User-editable `.toml` and `.ini` configuration and
companion PAKs remain user-owned.

Replacer mods use stricter ownership. Redux never adopts an external replacer because it did not
preserve the files that were already replaced. To bring one under management, remove the external
replacer, verify BG3 through Steam or GOG, then install it through Redux. Before replacing anything,
Redux verifies the current targets as known clean game files and stores those originals in protected
backup storage. Unknown or already-modded targets are refused rather than backed up. Redux does not
ship copies of BG3 files, and removal restores an original only while all managed files still match
the ownership record.

> [!CAUTION]
> Layout validation is not a publisher signature or malware scan. Install native code only from a
> source you trust, close BG3 first, and use the manager's status and recovery information instead
> of manually deleting Redux-owned files.

### Download Manager

**Download Manager** is Redux's shared intake inbox for local packages and optional Nexus Mod
Manager downloads. Add a package from the window or drop supported PAK, LSV, ZIP, 7z, RAR, TAR, or
GZip files onto Redux's full-window install target. Drop location never selects Active versus
Inactive Mods. Local inputs are copied into the managed Downloads folder, assigned a stable SHA-256
identity, inspected, and shown in the same persistent inbox as network acquisitions.
Known local packages reuse installed or bundled source artwork during intake, so their Download
Manager cards do not have to wait for installation before showing an available thumbnail.

For Nexus, enable online mod information, add a Nexus Mods API key, and enable NXM links during
onboarding or in Preferences. Choosing **Mod Manager Download** then sends the `nxm://` link to the
existing Redux process and opens **Download Manager**. An optional preference controls whether
protocol activations bring that window to the front.

Downloads are queued in a managed folder with bounded concurrency, visible progress,
pause/resume/retry behavior, restart recovery, and a verified SHA-256 archive identity. Free-user
downloads that lose their temporary authorization ask for a new link without saving the temporary
key or signed URL. Network state remains separate from package inspection and installation.
Completed packages are automatically classified and remain separate from installation until their
destination-aware **Install** action is chosen. Redux then verifies the archive again and routes
ordinary PAKs to Inactive Mods, reviewed native and Script Extender packages through Game-Directory
Mod Manager, and saves through Save Game Manager. Mixed, ambiguous, malformed, and unreviewed native
layouts stay blocked in the inbox without changing files. Intake never activates, reorders, or syncs
a mod automatically.

The PAK install review distinguishes clean new mods from updates, replacements, downgrades, and
unreadable packages. **Review clean mod installs** can be turned off from a clean review or restored
in Preferences; only entirely clean, brand-new PAK batches bypass that dialog. Anything requiring a
decision continues to stop for review.

Active transfers are paused and their queue state is saved before Redux exits. Finished downloads
and completed installation history do not keep the application open. Completed installations move
to the **Installed** tab with their destination. **Clear Installed History** removes those records
without deleting the installed content or downloaded archives; removing an uninstalled completed
download explicitly offers to move its archive to the Recycle Bin.

Installed-history entries can be reinstalled from their retained Download Manager archive. Adding
the same local archive again returns that record to the inbox instead of creating a duplicate or
leaving it stranded as completed history. Deleting a mod never implicitly deletes a retained
download; a future optional archive library remains separate from both uninstall and history state.

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
- Contextual category and semantic colors extend through Quick Access hover and selection states;
  disabling colored interactions restores the active theme's standard treatment.
- Reduce Motion for scrolling, sliding, scaling, and animated transitions while preserving clear
  hover and selection states.
- Independently removable background blur and dimming.
- Configurable shortcuts, keyboard-operable dialogs, selectable dialog text, screen-reader helpers,
  and inherited speech commands.

Quick Access searches commands, profiles, saved orders, categories, installed mods, theme actions,
folders, and Redux support links from one keyboard-first surface. Its category results reuse saved
category icons and colors, while destructive, warning, and save/export actions retain their
semantic meaning. The shortcut editor groups related actions and provides compact group-wide
expand/collapse controls.

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

**Tools > Inspect Mod Package** performs a read-only preflight on PAKs, common release archives,
reviewed or unknown native/DLL layouts, hybrid packages, save archives, and loose `.lsv` files. It
reviews identity, expected destinations, dependencies, embedded creator metadata, Script Extender
and Osiris signals, override behavior, and common development debris without installing or
modifying the selected file. A clean result is not a guarantee of in-game compatibility.

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
