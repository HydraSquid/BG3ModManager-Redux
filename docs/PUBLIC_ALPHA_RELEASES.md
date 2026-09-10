# Public-alpha releases and update recovery

This page documents Redux's public release contract so maintainers, contributors, and users can
verify what the installer and in-app updater are allowed to download. It does not replace testing
the exact files published on GitHub and Nexus Mods.

## Release artifacts

`0.1.0-alpha.15` is the first Redux public-alpha version. Each public alpha has an immutable version
and a matching Git tag such as `v0.1.0-alpha.15`. The versioned GitHub release contains:

- `BG3ModManager-Redux_v0.1.0-alpha.15.zip`, the portable application;
- `BG3ModManager-Redux-Setup.exe`, the separate fresh-install web bootstrapper; and
- release notes for that exact version.

The portable ZIP contains `Redux-Release-Files.json`. That inventory names every application file
owned by the release. It deliberately excludes settings, saved orders, downloads, retained
archives, logs, backups, custom themes, and other user-created state.

The portable application starts at `Redux.exe`. The adjacent managed assembly is `Redux.dll`.
`BG3ModManager.exe` was the private-alpha runtime name and must not be present at the root of an
alpha.15 package. The Setup artifact retains its descriptive filename because it is a distinct
bootstrapper, not the application runtime.

The moving `public-alpha` channel release contains
`Redux-Update-Public-Alpha.json`. Redux and Setup use its fixed URL; they do not rely on GitHub's
generic latest-release selection. The document points to one versioned portable ZIP and records its
exact byte length and SHA-256 digest.

## Prepare a candidate

1. Make the application, assembly, Setup, tag, ZIP filename, release notes, and manifest versions
   agree.
   For alpha.15, start from [`releases/0.1.0-alpha.15.md`](releases/0.1.0-alpha.15.md) and update only
   the final artifact-specific details.
2. Run `Build-Redux.ps1 -Configuration Debug` and the complete Redux regression executable.
3. Run `Build-Installer.ps1 -Configuration Release -RunTests`.
4. Run `Build-Redux.ps1 -Configuration Publish` with Python 3 available. This creates the
   versioned and Latest ZIPs, release inventory, and public-alpha channel manifest.
5. Confirm the portable inventory exactly covers the ZIP, the ZIP has only the four expected
   `Updater/` files, and Setup is not inside the portable archive.
6. Audit NuGet dependencies and inspect both deliverables for secrets, logs, settings, caches,
   dumps, source paths, and other build-machine data.
7. Complete clean-install, update-from-the-previous-alpha, rollback, uninstall, and core workflow
   smoke tests on the exact candidate files.

For alpha.15, migration testing must also cover a folder from the legacy `BG3ModManager.exe` era.
Setup should recognize that folder as an existing installation, while a clean alpha.15 package must
contain only the new `Redux.*` root runtime. The final portable archive must be regenerated after
any README or packaged-document change because those bytes affect its SHA-256 digest.

## Publish without creating a broken channel

1. Publish the immutable versioned release and upload the tested portable ZIP and Setup executable.
2. Download both files from GitHub and repeat their size, hash, launch, install, and uninstall checks.
3. If Nexus Mods also hosts the files, confirm its downloads are byte-identical to the tested GitHub
   artifacts where the filenames represent the same build.
4. Confirm the manifest's versioned artifact and release-notes URLs work anonymously.
5. Upload the already-tested manifest to the moving `public-alpha` channel only after every
   versioned artifact is reachable. The channel manifest is the final publication step.
6. Reproduce an update from the previous public alpha and a fresh Setup install through the public
   URLs before announcing the release.

For the first public alpha there is no previous public channel build. Replace that one upgrade test
with a migration from the newest private alpha, a clean portable launch, and a clean Setup install.
Publish the channel manifest last. Until that step, in-app checks and Setup must fail safely without
changing an installation.

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

Record the tested Windows and BG3 versions, source and target Redux versions, install type, hashes of
the exact Setup and portable ZIP, and the result of each major smoke-test group. Link defects to
focused issues. Release only when no known crash, data-loss risk, unsafe mutation, broken primary
workflow, install/update/uninstall failure, or credential/privacy leak remains open.

Expected limitations should be written plainly in the release notes, installation guide, or
troubleshooting guide. Speculative redesigns can remain deferred without blocking a safe alpha.

## Final alpha.15 publication record

Keep one maintainer record with the following values from the exact artifacts that are uploaded:

- source commit and `v0.1.0-alpha.15` tag target;
- portable ZIP filename, byte length, and SHA-256;
- Setup filename, byte length, and SHA-256;
- `Redux-Update-Public-Alpha.json` byte length and SHA-256;
- successful `dev` and `main` Windows CI run links;
- clean portable, Setup install/uninstall, private-alpha migration, NXM association, update-channel,
  and core load-order smoke-test results;
- GitHub and Nexus Mods download URLs; and
- announcement time plus any accepted public-alpha limitations.

The generated channel manifest records the portable archive's byte length and SHA-256, but it is not
a substitute for this human-readable release record.
