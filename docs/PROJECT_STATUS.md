# Project status and roadmap

This page is the canonical snapshot of Redux's release position. The
[changelog](CHANGELOG.md) records shipped behavior, while GitHub issues hold individual defects and
proposals.

## Current position

| Item | Current value |
|:--|:--|
| Product | Baldur's Gate 3 Mod Manager Redux |
| Short name | Redux |
| Tagline | **Bring order to the chaos.** |
| Release line | `0.1.0-alpha.16.3.3` |
| Lifecycle | Public alpha |
| Platform | Windows 10/11 x64 |
| Runtime | .NET 8 Desktop Runtime |
| Application entry point | `Redux.exe` |
| Public artifact | `BG3ModManager-Redux_v0.1.0-alpha.N[.H[.M]].zip` |
| Active milestone | `v0.1.0 – Public Alpha` |

The private-alpha stabilization milestone is complete. Alpha.15 established the first public-alpha
baseline, alpha.16 delivered its focused startup, accessibility, and window-behavior fixes, and
alpha.16.1 corrected Sync recovery and was the final release to experiment with a separate Setup.
Alpha.16.2 fixes public reports about Nexus file names and mod.io source correction, and begins the
single portable ZIP workflow shared by GitHub Releases and Nexus Mods. Those services
are authoritative for the current artifact availability. The release gate
below applies
whenever a build is prepared for publication. The public-alpha
milestone remains open for launch feedback, stabilization, and later alpha releases; it is not a
one-build milestone.

## What the current public alpha establishes

- A cohesive Redux interface with responsive toolbar groups, shared modal chrome, semantic actions,
  categories, separators, a selected-mod drawer, custom themes, and accessibility controls.
- Explicit saved-order working state, comparison and history, review-before-sync, restore points,
  Undo/Redo, and conservative external-change protection.
- Built-in Mod Diagnostics plus a separately enabled, preview-first Load Order Advisor.
- A unified Download Manager for local packages and optional NXM downloads, guarded Install All,
  installed history, and optional retained package archives.
- Separate Save Game Manager and reviewed Game-Directory Mod Manager workflows.
- Conservative offline mod recognition, privacy-limited contribution reports, and optional creator
  manifests.
- A verified public-alpha update contract for safe in-app updates to existing portable folders.
- The new `Redux.exe` runtime identity, transparent Redux star, current tagline, and official Discord
  link.

See [Changes from upstream](CHANGES_FROM_UPSTREAM.md) for the durable feature boundary and the
[changelog](CHANGELOG.md) for release-specific detail. Current release copy is in the
[alpha.16.3.3 release notes](releases/0.1.0-alpha.16.3.3.md).

## Public launch gate

The source is ready for the publication sequence only when all of the following are true:

- `dev` and `main` point to the reviewed release commit and their Windows CI runs pass.
- The version agrees across the application, archive, channel manifest, tag, and release copy.
- A clean Publish build creates a portable ZIP with `Redux.exe`, `Redux.dll`, its release inventory,
  and exactly the expected updater payload—without user state or legacy root runtimes.
- The exact portable ZIP passes clean extraction, private-alpha migration, manual and in-app update,
  launch, core load-order, NXM association, removal, and recovery smoke tests.
- GitHub and Nexus Mods copy matches the README, support boundaries, requirements, and known limits.
- GitHub receives immutable versioned artifacts before the moving public-alpha channel manifest is
  published.
- Downloads from GitHub and Nexus Mods are checked against the locally recorded size and SHA-256.

The complete operator sequence is in
[Public-alpha releases and update recovery](PUBLIC_ALPHA_RELEASES.md).

## Public-alpha roadmap

1. **Maintain the public channel.** Publish each tested portable ZIP on GitHub, send those exact
   bytes to Nexus Mods through the protected release workflow, publish the update-channel manifest
   last, and synchronize announcements.
2. **Observe real installations.** Triage startup, migration, path, package, save, native-mod,
   accessibility, and scaling reports. Security, privacy, data-loss, and primary-workflow defects
   take priority over cosmetic expansion.
3. **Ship focused alpha hotfixes.** Never replace an existing versioned artifact with different
   bytes. Correct defects in a higher alpha with a matching tag, archive, notes, and channel entry.
4. **Expand coverage deliberately.** Improve recognition and guidance only from reviewed evidence;
   continue clean-machine, display-scaling, and real-world mod-set testing.
5. **Exit public alpha only on evidence.** Stable installation/update/removal, safe core workflows,
   understandable recovery, and an acceptably low volume of release-blocking defects are required.

## Planned, non-blocking work

Two longer-term explorations are intentionally outside the current public-alpha gate:

- [#56 — Expand Redux localization and accessibility support](https://github.com/circleainn/BG3ModManager-Redux/issues/56)
- [#63 — Explore a docked/paged Managers workspace](https://github.com/circleainn/BG3ModManager-Redux/issues/63)

They are proposals, not commitments for a particular alpha. New plans belong in focused issues and
should preserve the optional-feature and safety rules documented in
[Diagnostics and optional features](REDUX_OPTIONAL_MODULES.md).

## Known public-alpha limits

- Nexus Mods uses a user's personal API key rather than public Redux SSO.
- Provider, automatic-category, offline-recognition, and advisor knowledge are intentionally
  incomplete and conservative.
- Some mod.io author links and metadata paths remain limited.
- Imported fonts and custom PNG icons depend on the user's files and licensing.
- Uncommon display scaling, very small windows, or unusually dense mod sets may expose visual or
  performance defects not seen during private testing.
- Linux, macOS, Wine, and Proton are unsupported.

These limits do not authorize unsafe guesses. Unknown source identities remain **Local**, unknown
native packages remain unmanaged, and advice remains optional and preview-first.
