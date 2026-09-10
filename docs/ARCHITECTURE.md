# Redux architecture

This is a contributor-level map of Redux. It is intentionally higher level than the source and does
not replace code, tests, schemas, or security validation.

## Solution map

| Component | Path | Responsibility |
|:--|:--|:--|
| Desktop application | `src/GUI/GUI.csproj` | WPF windows, controls, themes, interaction, app composition; builds `Redux.exe` / `Redux.dll` |
| Core | `src/Core/DivinityModManagerCore.csproj` | Models, persistence, package/load-order logic, diagnostics, downloads, native installs, updates |
| Updater | `src/Updater/ReduxUpdater.csproj` | Narrow out-of-process application-file replacement and rollback |
| Setup | `src/Installer/ReduxInstaller.csproj` | Install/update web bootstrapper, prerequisite handling, shortcuts, transactional replacement, inventory-scoped uninstall |
| Toolbox | `src/Toolbox/Toolbox.csproj` | Retained utility and Script Extender support code |
| Runtime regression suite | `tests/Redux.Core.Tests` | Executable behavioral checks for core and WPF contracts |
| Installer regression suite | `tests/Redux.Installer.Tests` | Executable checks for Setup, manifests, install, rollback, and uninstall |
| Database tools | `tools/ReduxModDatabaseTool*` | Preview-first validation and reviewed offline-database maintenance |

`External/` contains the upstream and vendored dependencies required by the inherited BG3MM/LSLib
foundation. Redux-specific compatibility policy belongs in root build props/targets where practical
so submodule history remains attributable and updateable.

## Major product domains

### Package and mod state

The inherited package parser and profile model supply installed packages, active/inactive state,
overrides, dependencies, UUIDs, Script Extender metadata, and BG3 paths. Redux extends this with
explicit source provenance, categories, separators, notes, richer inspection, and unified selection.

Parsed package identity remains authoritative. Provider metadata, filenames, creator manifests, and
the offline database may enrich a package only through conservative, tested precedence rules.

### Load orders

Working edits are separate from the selected saved order and from the game's `modsettings.lsx`.
Saving and syncing are distinct operations. Comparison, history, restore points, and bounded
Undo/Redo make intended changes visible and recoverable. External-change checks prevent an undo from
overwriting a game file changed after Redux's write.

### Diagnostics and guidance

Mod Diagnostics is built-in and read-only. The Load Order Advisor is optional and experimental.
Organizer plans are deterministic, preview-first, separator-aware, one-step undoable, and unsaved
until the user explicitly saves. Neither system silently repairs, downloads, activates, reorders, or
syncs a package.

### Downloads and package routing

Download Manager separates link receipt, metadata resolution, transfer, archive verification,
inspection, destination selection, installation, activation, order editing, and game sync. Its
persistent queue, partial downloads, installed history, and optional Package Archive Library have
separate ownership and removal behavior.

Finalized packages route through destination-specific services:

- ordinary PAKs enter the inactive-mod review/import path;
- saves use Save Game Manager validation and staged import;
- reviewed native and root-level packages use Game-Directory Mod Manager; and
- ambiguous, unsafe, corrupt, or unreviewed native content remains blocked.

### Game-directory mods

The game-directory service accepts only reviewed layouts. It bounds archives, validates canonical
paths and AMD64 DLL headers, stages content, fingerprints source and destination state, and rechecks
immediately before commit. Ownership records distinguish files Redux added, originals Redux backed
up, user-editable configuration, external installations, changed files, and unknown files.

Replacer mods have stricter rules than add-only plugins. Redux never adopts an external replacer as
managed because it did not preserve the user's original game file.

### Application distribution

Publish packaging produces an inventory of release-owned files. Setup and the in-app updater accept
only the fixed public-alpha channel, strict version/host/path shapes, declared size and SHA-256, safe
archive paths, and a complete inventory. The updater runs outside the installation and mutates only
new or previously owned application files. Setup uses isolated staging for a fresh install; for an
existing registered install it backs up the prior inventory, atomically replaces new or previously
owned files, removes obsolete owned files, preserves unlisted content, and rolls back failed
transactions.

## UI and theme system

`src/GUI/Themes` holds the shared Redux visual language. Dark, Light, and Parchment define semantic
resource roles; custom themes derive the same roles and may optionally generate gradients. Shared
styles and templates should be reused instead of hard-coding one theme's colors into a window.

Important presentation rules:

- use dynamic semantic resources for live theme changes;
- keep success, warning, error, information, save/export, destructive, provider, and category
  meaning distinct;
- use the shared modal/window chrome and action-button styles;
- verify Hide Icons, Hide Icon Labels, colored interactions/text, text-size choices, imported fonts,
  Reduce Motion, and background-effect settings;
- preserve list virtualization and avoid collection resets for ordinary single-row changes; and
- complete layout before screenshots or pixel-sensitive assertions.

The application uses WPF pack URIs against the `Redux` assembly. Runtime-name changes must update
assembly metadata, resource URIs, icon resources, packaging, installer/updater contracts, NXM
registration tests, and documentation together.

## State and ownership boundaries

Application files, user-created Redux state, BG3 Mods, profiles, saves, `modsettings.lsx`, retained
archives, managed game-directory files, and provider credentials are different ownership domains.
One workflow must not infer permission to mutate another.

Safe write patterns include:

- path containment and canonicalization;
- temporary staging before validation;
- atomic replacement for durable JSON and load-order state;
- transaction backup and rollback for multi-file changes;
- publish-by-rename for completed downloads;
- hash and length checks before retained or downloaded packages are reused;
- Windows Recycle Bin for recoverable user-requested deletion where appropriate; and
- revalidation at commit time to catch source or destination changes after review.

Read [Privacy and local data](PRIVACY_AND_DATA.md) and [Security policy](SECURITY.md) before changing
any filesystem, archive, credential, provider, update, or native-code path.

## Build and verification

Common commands from the repository root:

```powershell
.\Build-Redux.ps1 -Configuration Debug -Rebuild
.\Test-Redux.ps1
.\Build-Installer.ps1 -Configuration Release -RunTests
.\Build-Redux.ps1 -Configuration Publish -Rebuild -PythonExecutable "C:\Path\To\python.exe"
git diff --check
```

The main build requires Visual Studio managed-desktop, Desktop C++, and C++/CLI components because
LSLib includes native and managed/native projects. Publish packaging also requires Python 3.

Automated checks are necessary but not sufficient. Inspect visual work across themes, scaling,
window sizes, typography, and motion settings. Test destructive or stateful work through success,
failure, cancellation, restart, and recovery. Release validation must use the exact uploaded bytes.

## Where to make common changes

| Change | Start with |
|:--|:--|
| Shared colors, controls, buttons, menus, or chrome | `src/GUI/Themes` and shared controls |
| Window-specific behavior | Matching XAML/code-behind and view model under `src/GUI` |
| Load-order persistence or comparison | `src/Core/AppServices` and `src/Core/Services` |
| Package diagnostics | `IModHealth*`, health rules, package preflight, and regression tests |
| Categories or separators | category models/classifier plus visual divider/filter/drag policies |
| Downloads or NXM | NXM manager/scheduler/store/transfer/association services |
| Saves | `Bg3SaveGameService` and Save Manager UI |
| Native/root-level mods | `ReduxGameDirectoryInstallService` and its manager UI |
| Offline identity/advice | database service, bundled JSON, tool, and database docs |
| Updates or Setup | channel/package/launch services, Updater, Installer, packaging workflow |
| Public messaging | README, changelog, status, brand, community, and FAQ |

## Non-negotiable invariants

- No package intake action silently activates, reorders, or syncs a mod.
- Separators and categories never enter `modsettings.lsx`.
- Unknown evidence stays unknown; it is not upgraded into a confident provider or native identity.
- Optional network and advisor features remain independently controllable.
- Credentials and temporary signed download capability never enter ordinary settings, logs, reports,
  Modlists, or archive indexes.
- Release updates never claim user state.
- Documentation and public imagery must describe the current real build, not a synthetic UI.
