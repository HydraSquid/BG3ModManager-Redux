# Post-16.4 issue audit

Audited September 13, 2026 against all open issues, recent closures, and the published alpha.16.4 implementation.
This checkpoint is assigned to alpha.16.4. The release notes record the final scope; live checks listed below remain qualification follow-ups unless explicitly marked complete.

Save Manager now exports selected saves or a selected campaign to ZIP, including thumbnails,
with progress and cancellation. Export retains the import limits: 32 saves, 1 GB total and
256 MB per file; larger campaigns require smaller selections. Round-trip and cancellation
regressions pass; real campaign export/restore and cancellation still need a live check.

## Issue disposition

Save Mod Review has UUID matching and activation regression coverage. Live follow-up: verify
a real save with missing/inactive mods, activation and Undo, and a corrupt save.


| Issue | Assessment | Next action |
|:--|:--|:--|
| [#111](https://github.com/circleainn/BG3ModManager-Redux/issues/111) Inactive organization | Closed: shipped in 16.4 with persistence, separators, and Undo/Redo coverage. | Global Redux-only ordering; Advisor remains active-only. |
| [#113](https://github.com/circleainn/BG3ModManager-Redux/issues/113) Window consistency | Substantial work complete; broad scope remains partial. | Finish the manual workflow/theme/keyboard matrix below. |
| [#119](https://github.com/circleainn/BG3ModManager-Redux/issues/119) Extender settings | Closed: default-export preference persistence is fixed in 16.4. | Omitted EnableAchievements=true means the normal enabled default, not disabled achievements; explicit default export is optional. |
| [#120](https://github.com/circleainn/BG3ModManager-Redux/issues/120) NXM reassociation | Closed: explicit takeover recovers from stale Redux ownership markers in 16.4. | Automatic repair cannot replace another handler without explicit reassociation. |
| [#95](https://github.com/circleainn/BG3ModManager-Redux/issues/95) Elevation warning | Unresolved; current issue has no new reporter diagnostics. | Obtain same-process elevation evidence/logs; do not claim fixed. |
| [#98](https://github.com/circleainn/BG3ModManager-Redux/issues/98) Nexus SSO | Planned; official application registration is a prerequisite in the issue. | Confirm registration before scheduling implementation. |
| [#109](https://github.com/circleainn/BG3ModManager-Redux/issues/109) Overrides per order | Planned; requires safe file moves and recovery. | Separate feature work with opt-in migration and transaction tests. |
| [#110](https://github.com/circleainn/BG3ModManager-Redux/issues/110) Wine | Planned; platform reports need reproducible environments. | Collect/test real Wine prefixes, SE detection, and NXM routing. |
| [#108](https://github.com/circleainn/BG3ModManager-Redux/issues/108) Collections | Open, partial: web-link importer and download workflow shipped in 16.4. | Direct collection NXM activation from the original request remains. Historical revisions and installer rules are unsupported. |
| [#118](https://github.com/circleainn/BG3ModManager-Redux/issues/118) Compact interface | Later experiment. | Prototype separately after stabilization. |
| [#63](https://github.com/circleainn/BG3ModManager-Redux/issues/63) Docking | Planned, substantial workspace change. | Separate design/implementation; reuse manager state. |
| [#56](https://github.com/circleainn/BG3ModManager-Redux/issues/56) Localization/accessibility | Planned foundation; current layout work is only partial coverage. | Separate resource/localization work and assistive-technology testing. |

Issues #111, #119, and #120 were closed with release-audit explanations appended to their original reports. Issues #108 and #113 remain partial. No issue was reopened: no new regression evidence justified reopening, and #97 signing remains explicitly deferred (not completed). Other planned issues and #95 awaiting diagnostics retain their existing state.

## Live verification follow-ups

- Fresh setup and returning-user setup: finish, cancel, restart, custom theme/font, icons hidden,
  icons-only, text sizes, gradients, Reduce Motion, and owner backdrop. Verify persisted choices.
- Real Windows scaling (100/125/150/200%), keyboard traversal, and screen-reader context across
  Preferences, Downloads, Saves, native mods, reviews, and What's New. Render tests do not replace this.
- Inactive-list persistence/Undo and unchanged game export; Advisor must not alter inactive mods.
- Real NXM takeover and download/install flow, plus Script Extender default export/config reload.
- Native detection against clean game files; installation/reinstall/remove with protected backups.
  Switching Redux folders still does not migrate ownership records or backups.
- Real updater smoke test from the previous public build. Builds, regression/dependency/package
  checks, publication, main synchronization, and update-channel verification are complete.
  Dev remains without release assets or portable CI downloads.

## Recommended scope

16.4 shipped the manager overhauls, collections, inactive organization, and onboarding work.
Prioritize concrete reports and the remaining collection-link/UI verification scope before another broad feature pass.

If a further headline feature is desired, Nexus SSO fits the new onboarding best, once registration
is available. Override management is another meaningful feature but carries greater file-safety scope.
Collections were subsequently implemented and are included in 16.4. Docking, a second UI redesign, and Linux support remain separate work.

## Data review checkpoint

Removed one invalid ordering record and added four exact library-name aliases plus four
corroborated UUID category records. Remaining 137 dependency differences, ten new UUID records,
name-only additions, and placement/category changes require further evidence review. No new
ordering constraints or provider identity assignments were accepted in this pass.

Save Mod Review package fixtures now cover populated, empty, absent metadata, missing UUID, and
corrupt saves, including input-file preservation and legacy empty-import behavior. A real gameplay
save and live activation/Undo check remain live verification follow-ups.

## Alpha.16.4 release verification

- Debug and Publish builds completed; 471/471 regression checks passed in each configuration.
- NuGet transitive dependency audit reported no known vulnerable packages.
- The portable package contains 75 inventoried files, exactly four updater files, matching manifest size/hash, and no detected user state or local build paths. Clean extraction succeeded.
- Main and dev publication is authorized for 16.4. No app-control testing was performed. Live free-account collection handoff, real UI/update smoke checks, scaling and assistive-technology checks remain follow-ups; automated checks do not claim to replace them.

## Published release

- Release commit: `618013f3bfdbf2c3cf9b154ad5191674e3d7c915` on dev and main; immutable tag `v0.1.0-alpha.16.4`.
- Both GitHub Windows CI runs succeeded, including regression and Publish-package checks.
- GitHub ZIP: 17,252,367 bytes; SHA-256 `58ffcf33ed8dca5b426c324da3b135ca7ec1d3ff5beb0b22431f156c3a8b1c30`. Anonymous download matched.
- Nexus publication succeeded after the configured reviewer approved it: public file `130490`, version ID `14920716516794`. API verification confirmed version, size, and all three headline feature descriptions.
- The public-alpha update channel was updated last and verified against the published ZIP.
- README and this issue audit were refined after publication. Published 16.4 assets and their tag remain unchanged.
