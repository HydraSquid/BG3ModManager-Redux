# Public-alpha releases and update recovery

This page documents Redux's public release contract so maintainers, contributors, and users can
verify what the in-app updater is allowed to download. It does not replace testing
the exact files published on GitHub and Nexus Mods.

## Release artifacts

`0.1.0-alpha.15` was the first Redux public-alpha version; `0.1.0-alpha.16.2` is the current hotfix.
Each public alpha has an immutable version and a matching Git tag such as
`v0.1.0-alpha.16.2`. A correction to an already-published alpha uses a hotfix suffix such as
`0.1.0-alpha.16.1`, then `.16.2`; it does not replace the earlier release's files. The next planned
alpha remains `0.1.0-alpha.17`. Starting with alpha.16.2, the public artifact
set contains:

- `BG3ModManager-Redux_v0.1.0-alpha.N[.H].zip`, the versioned portable application; and
- release notes for that exact version.

Alpha.16.1 was the last release to include the experimental Setup artifact. Historical release notes
retain that fact, but future releases and current installation guidance are portable-only.

The portable ZIP contains `Redux-Release-Files.json`. That inventory names every application file
owned by the release. It deliberately excludes settings, saved orders, downloads, retained
archives, logs, backups, custom themes, and other user-created state.

The portable application starts at `Redux.exe`. The adjacent managed assembly is `Redux.dll`.
`BG3ModManager.exe` was the private-alpha runtime name and must not be present at the root of a
public portable package.

The moving `public-alpha` channel release contains
`Redux-Update-Public-Alpha.json`. Redux uses its fixed URL rather than relying on GitHub's
generic latest-release selection. The document points to one versioned portable ZIP and records its
exact byte length and SHA-256 digest.

The updater compares a four-part numeric internal version. Alpha.15 and alpha.16 retain their
published legacy values (`0.1.0.15` and `0.1.0.16`). Starting with the hotfix-aware contract,
`0.1.0-alpha.N.H` maps to `0.1.N.H`, and a new base alpha maps to `0.1.N.0`. This keeps
`alpha.16 < alpha.16.1 < alpha.16.2 < alpha.17` while preserving updates from the two legacy
public builds.

Compatibility matters during the transition: the already-published alpha.15 and alpha.16 clients
only parse the original `alpha.N` form, so they cannot discover a dotted hotfix automatically. The
first dotted hotfix must therefore be installed manually by extracting its portable archive, or be
preceded by one final single-number bridge release. Once a hotfix-aware build is installed, later
dotted hotfixes work normally. Never publish a dotted channel manifest on the assumption that an
unmodified alpha.16 client can read it.

## Prepare a candidate

1. Make the application, assembly, tag, ZIP filename, release notes, and manifest versions agree.
   Start from the previous file in [`releases/`](releases/) and update only the final,
   artifact-specific details.
2. Run `Build-Redux.ps1 -Configuration Debug` and the complete Redux regression executable.
3. Run `Build-Redux.ps1 -Configuration Publish` with Python 3 available. This creates the
   versioned and Latest ZIPs, release inventory, and public-alpha channel manifest.
4. Confirm the portable inventory exactly covers the ZIP and the ZIP has only the four expected
   `Updater/` files.
5. Audit NuGet dependencies and inspect the archive for secrets, logs, settings, caches,
   dumps, source paths, and other build-machine data.
6. Complete clean-extraction, update-from-the-previous-alpha, rollback, removal, and core workflow
   smoke tests on the exact candidate archive.

For alpha.15, migration testing also covered a folder from the legacy `BG3ModManager.exe` era. A
clean public package must contain only the new `Redux.*` root runtime. The final portable archive
must be regenerated after any README or packaged-document change because those bytes affect its
SHA-256 digest.

## Publish without creating a broken channel

1. Publish the immutable versioned GitHub release with the tested portable ZIP.
2. Download the ZIP from GitHub and repeat its size, hash, contents, and launch checks.
3. Approve the protected `nexus-production` deployment. The release workflow downloads the GitHub
   asset and submits those exact bytes through Nexus Mods' official upload action.
4. Record the returned Nexus file-version ID and verify the Nexus entry matches the GitHub version,
   filename, and archive.
5. Confirm the manifest's versioned artifact and release-notes URLs work anonymously.
6. Upload the already-tested manifest to the moving `public-alpha` channel only after every
   versioned artifact is reachable. The channel manifest is the final publication step.
7. Reproduce an in-app update, a manual replacement update, and a fresh portable launch through the
   public URLs before announcing the release.

For the first public alpha there was no previous public channel build, so its upgrade test was
replaced by migration from the newest private alpha and a clean portable launch. Publish the channel
manifest last. Until that step, in-app update checks must fail safely without changing an
installation.

Never replace an immutable versioned ZIP with different bytes while retaining its version. Publish
a new, higher alpha version and a matching manifest instead.

## Withdraw or supersede a bad update

If a release may crash, corrupt state, mutate unsafe paths, expose private data, or fail installation
or updating, remove the channel manifest asset immediately. A missing channel document makes checks
fail safely and does not alter installed copies. Keep the affected versioned artifact available only
as long as needed for diagnosis and clearly mark its release as withdrawn.

Publish a corrected higher version, verify it independently, then replace the channel document with
the corrected manifest. Redux refuses equal-version replacements and downgrades, so do not point the
channel at an older build as a rollback mechanism.

If files were already applied, publish recovery guidance that names the affected versions and state
boundaries. Prefer a fixed forward update. When manual rollback is necessary, restore a complete
known-good application backup; do not delete or overwrite mods, saves, settings, saved orders,
downloads, retained archives, or backups as part of application rollback.

## Evidence for the public-alpha gate

Record the tested Windows and BG3 versions, source and target Redux versions, the exact portable ZIP
hash, and the result of each major smoke-test group. Link defects to
focused issues. Release only when no known crash, data-loss risk, unsafe mutation, broken primary
workflow, install/update/uninstall failure, or credential/privacy leak remains open.

Expected limitations should be written plainly in the release notes, installation guide, or
troubleshooting guide. Speculative redesigns can remain deferred without blocking a safe alpha.

## Per-release publication record

Keep one maintainer record with the following values from the exact artifacts that are uploaded:

- source commit and matching `v0.1.0-alpha.N[.H]` tag target;
- portable ZIP filename, byte length, and SHA-256;
- `Redux-Update-Public-Alpha.json` byte length and SHA-256;
- successful `dev` and `main` Windows CI run links;
- clean extraction, manual update/removal, private-alpha migration, NXM association, update-channel,
  and core load-order smoke-test results;
- GitHub and Nexus Mods download URLs; and
- announcement time plus any accepted public-alpha limitations.

The generated channel manifest records the portable archive's byte length and SHA-256, but it is not
a substitute for this human-readable release record.
