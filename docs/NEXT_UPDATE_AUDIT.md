# Next update readiness

Audited September 13, 2026 against the open GitHub issues and current dev implementation.
This is a development checkpoint, not a release announcement. Public version remains alpha.16.3.5.

Save Manager now exports selected saves or a selected campaign to ZIP, including thumbnails,
with progress and cancellation. Export retains the import limits: 32 saves, 1 GB total and
256 MB per file; larger campaigns require smaller selections. Round-trip and cancellation
regressions pass; real campaign export/restore and cancellation still need a live check.

## Open issues

Save Mod Review has UUID matching and activation regression coverage. Before release, verify
a real save with missing/inactive mods, activation and Undo, and a corrupt save.


| Issue | Assessment | Next action |
|:--|:--|:--|
| [#111](https://github.com/circleainn/BG3ModManager-Redux/issues/111) Inactive organization | Implemented in dev with regression checks. Global Redux-only saved order; not per named active order. Closed groups stay in their pane. | Verify restart, column-sort reset, group movement, Undo/Redo, and active export on a real workspace. |
| [#113](https://github.com/circleainn/BG3ModManager-Redux/issues/113) Window consistency | Substantial work complete; broad scope remains partial. | Finish the manual workflow/theme/keyboard matrix below. |
| [#119](https://github.com/circleainn/BG3ModManager-Redux/issues/119) Extender settings | Omitting EnableAchievements=true is expected when defaults are omitted. A separate export-default preference persistence defect is fixed in dev. | Verify actual game config output/restart; describe the distinction in release notes. |
| [#120](https://github.com/circleainn/BG3ModManager-Redux/issues/120) NXM reassociation | Stale ownership-marker recovery implemented and tested in dev. | Validate takeover/reassociation with another installed manager. |
| [#95](https://github.com/circleainn/BG3ModManager-Redux/issues/95) Elevation warning | Unresolved; current issue has no new reporter diagnostics. | Obtain same-process elevation evidence/logs; do not claim fixed. |
| [#98](https://github.com/circleainn/BG3ModManager-Redux/issues/98) Nexus SSO | Planned; official application registration is a prerequisite in the issue. | Confirm registration before scheduling implementation. |
| [#109](https://github.com/circleainn/BG3ModManager-Redux/issues/109) Overrides per order | Planned; requires safe file moves and recovery. | Separate feature work with opt-in migration and transaction tests. |
| [#110](https://github.com/circleainn/BG3ModManager-Redux/issues/110) Wine | Planned; platform reports need reproducible environments. | Collect/test real Wine prefixes, SE detection, and NXM routing. |
| [#108](https://github.com/circleainn/BG3ModManager-Redux/issues/108) Collections | Low-priority suggestion, not committed scope. | Keep open/deferred. |
| [#118](https://github.com/circleainn/BG3ModManager-Redux/issues/118) Compact interface | Later experiment. | Prototype separately after stabilization. |
| [#63](https://github.com/circleainn/BG3ModManager-Redux/issues/63) Docking | Planned, substantial workspace change. | Separate design/implementation; reuse manager state. |
| [#56](https://github.com/circleainn/BG3ModManager-Redux/issues/56) Localization/accessibility | Planned foundation; current layout work is only partial coverage. | Separate resource/localization work and assistive-technology testing. |

Keep unreleased fixes open until their release disposition is explicitly decided. This audit does
not close issues or post messages to reporters.

## Release qualification still needed

- Fresh setup and returning-user setup: finish, cancel, restart, custom theme/font, icons hidden,
  icons-only, text sizes, gradients, Reduce Motion, and owner backdrop. Verify persisted choices.
- Real Windows scaling (100/125/150/200%), keyboard traversal, and screen-reader context across
  Preferences, Downloads, Saves, native mods, reviews, and What's New. Render tests do not replace this.
- Inactive-list persistence/Undo and unchanged game export; Advisor must not alter inactive mods.
- Real NXM takeover and download/install flow, plus Script Extender default export/config reload.
- Native detection against clean game files; installation/reinstall/remove with protected backups.
  Switching Redux folders still does not migrate ownership records or backups.
- Before publication: clean Debug/Publish builds, regression/dependency/package checks, real updater
  smoke test from the public build, release notes/version assignment, and main synchronization.
  Publish only after approval. Dev remains without release assets or portable CI downloads.

## Recommended scope

The current work already warrants a normal feature update: inactive organization adds a capability,
and onboarding/Appearance plus shared workflow improvements materially change the user experience.
Finish qualification and address concrete failures rather than adding unrelated features to reach a quota.

If a further headline feature is desired, Nexus SSO fits the new onboarding best, once registration
is available. Override management is another meaningful feature but carries greater file-safety scope.
Docking, a second UI redesign, Collections, and Linux support should not be bundled just to enlarge this release.

## Data review checkpoint

Removed one invalid ordering record and added four exact library-name aliases plus four
corroborated UUID category records. Remaining 137 dependency differences, ten new UUID records,
name-only additions, and placement/category changes require further evidence review. No new
ordering constraints or provider identity assignments were accepted in this pass.

Save Mod Review package fixtures now cover populated, empty, absent metadata, missing UUID, and
corrupt saves, including input-file preservation and legacy empty-import behavior. A real gameplay
save and live activation/Undo check remain required before release.
