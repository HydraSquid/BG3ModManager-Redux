# Fork integration: alpha.15

This integration starts at circleainn's `363a1da` (alpha.15) and selectively
retains behavior from HydraSquid's `d6be4e3`. It was prepared on
`integration/upstream-alpha15`. The fork's earlier history is retained through
a merge, while the integrated tree uses the newer upstream implementations
plus the differences below.

## Retained fork differences

| Area | Integration |
| --- | --- |
| Downloads workspace | Embedded Inbox, History, and Archives pane using upstream's current download services; standalone window remains the fallback. |
| Selection | Explicit selected-only install/removal, Select All, and virtualized selection synchronization. |
| Dependency batches | Selected prerequisites are ordered before dependents; missing/old requirements and cycles block affected packages. Failed prerequisites suppress their dependents. |
| Dependency assistance | UUID-based installed/bundled/download matches, reviewed source links, and Copy UUID without name-based guessing. |
| PAK installation | Stage and validate all archive packages before committing; roll back all destination changes on failure. Sibling packages satisfy each other's declared requirements. |
| Layout | Adjacent-only pane resizing with responsive sizing and persisted Downloads visibility/width. Override Mods has a height-resize handle, minimum list heights, and a chosen height retained across collapse/expand during the session. Downloads uses the shared pane-header arrow. |
| Themes | Theme-aware alternating table rows, retaining upstream's current fonts, palettes, gradients, and transitions. |
| Sources | Manual BG3 mod.io and Nexus page links with persisted provenance; native mod.io identity blocks automatic Nexus inference. |
| Activation/shutdown | Restore activation after offscreen startup; require successful settings and queue persistence before closing. |
| Native compatibility | Read BG3 ProductVersion rather than FileVersion; recognize validated legacy native ownership records. |

## Upstream implementations retained

- Expanded game-directory catalog, guarded uninstall, adoption, repair, and Script Extender integration.
- Current download inspection, retained package archive store, install history, and generic local-package intake.
- Current mod-table selection reconciliation, load-order persistence, advisor, save manager, installer, and updater.
- Current public documentation, release version, bundled mod database, and branding.

The embedded Downloads pane retains upstream's card presentation. The old fork's
dedicated **Download Again** and **Details / Inspect Failure** actions are not
currently exposed there; their restoration remains a follow-up. Dependency review
is available through **Dependencies**.

## Runtime compatibility

- Queue schema remains backward-readable. Intact completed legacy files missing
  an archive hash receive a SHA-256 hash during reconciliation. Existing hashes
  are never replaced to conceal a mismatch.
- The fork continues using `Data/NativeInstalls`. Legacy version-1 native records
  accept only the three historical project identities with validated paths,
  hashes, backups, and game-directory identity. Successful mutations write the
  current version-2 format; incomplete journals continue to block mutations.
- Both native-state directory names are protected by installer/updater package
  inventories. This integration does not merge independently created
  `GameDirectoryInstalls` and `NativeInstalls` state trees.
- Installed-download history follows upstream's history semantics. Native
  installed status and removal are managed by the game-directory manager.

## Verification

Run `./Test-Redux.ps1` with Visual Studio's desktop C++/CLI and .NET workloads.
The regression harness uses synthetic packages, game directories, and UI data.
Set `REDUX_TABLE_SCREENSHOTS` to an existing private directory for wide/compact
WPF layout, narrow Downloads, and theme-striping captures.

Building and testing the integration does not deploy it to an existing portable
installation. Deployment requires separate approval and preservation of that
installation's runtime data.
