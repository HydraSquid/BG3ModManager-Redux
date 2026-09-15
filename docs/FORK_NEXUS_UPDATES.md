# Manual Nexus mod update checks

Open **Tools → Check Nexus Mod Updates…**, then choose **Check for updates**.
This reads Nexus file listings and saves check results. It does not download,
install, activate, reorder, save an order or export anything to the game.

Opening the window performs local identity matching and displays cached results;
it sends no Nexus requests. A Nexus API key and enabled online mod information
are needed for network checks. Existing fresh cached listings can be used offline.

## Three different versions

- **Package metadata version** comes from the installed PAK's embedded metadata.
  It can differ from the release name/version on Nexus. For example, a release
  called 1.1.1 can still contain package metadata saying 1.1.0.0.
- **Verified download release** is shown only for a locally verified Nexus file
  with recorded release metadata or a matching Nexus file-list entry. A project's
  overall version is never substituted for an installed release.
- **Your reference** is a release you explicitly choose for future comparisons.
  It is a personal acknowledgement, not proof that this release is installed.

Redux never decides that one release is newer by comparing version strings,
choosing the largest file ID, or selecting the latest optional upload.

## When the installed download is not identified

This is a coverage limitation, not an error in the installed mod. Redux checks
the linked project's file list even if it cannot identify the installed download.
The summary distinguishes downloads it can compare from those needing a reference.

Local matching uses the bundled installed-PAK fingerprint database and retained
Nexus downloads. Retained archives must match their recorded SHA-256 hash (a digest
of their bytes), and a contained PAK must match the installed PAK's SHA-256. Merely
matching a module UUID, filename or version is insufficient. Conflicting matches
remain unidentified. Matching reads local files without extracting or installing.

The resolver supports retained direct PAKs, ZIP, 7z and RAR archives. It applies
entry, byte and candidate limits and supports cancellation. Missing archives,
unsupported containers or older file-ID-only records cannot establish verified
identity; their recorded IDs may remain visible as unverified hints.

## “I'm current through this release”

1. Check for updates to obtain a fresh file list, or use the 24-hour cache.
2. Choose the relevant release in that mod's list. **Nothing is preselected.**
   Match the main/optional variant you actually mean; unrelated patches differ.
3. Choose **I'm current through this release**.

Redux will compare from that Nexus file's replacement history on future checks.
If Nexus later explicitly links a replacement, it appears again—even when the
author reuses the same version text. Unrelated optional uploads do not reset your
acknowledgement or become update recommendations.

Use **Reset my reference** to return to the identified installed download, if one
is available. The reference is scoped to the module UUID, Nexus project and actual
installed PAK hash. It does not apply to different package bytes, another project,
or another UUID. Choosing a reference rechecks the package hash before saving.

References are stored separately in
`Data/NexusUpdateChecks.json.references.json`, using atomic replacement, a backup
and an exclusive write lock. They survive restart and API-cache loss. A corrupt
reference store is reported rather than silently overwritten. It contains public
file identity and a PAK hash, not credentials or local file paths.

## Results and limitations

- **Update available:** an explicit replacement exists for the verified installed
  download or your chosen reference.
- **No update reported / No newer replacement since your reference:** no linked
  replacement is reported. Authors can upload new files without linking them;
  this is not proof that no newer unlinked release exists.
- **Choose an installed-download reference:** the project is known, but file-level
  comparison needs verified local evidence or your explicit choice.
- **Needs review:** the replacement history branches, cycles, or ends at a missing
  or unavailable file.
- **Check failed / Not checked:** no current conclusion is available. Stale cached
  results retain their timestamp and are not presented as current checks.

**Open Nexus Files** opens the project for manual review. The
[official Nexus API file contracts](https://github.com/Nexus-Mods/node-nexus-api/blob/master/src/types.ts)
explain that replacement relationships exist only when the author marks a file
as replacing another. User references do not invent those relationships.

## Request budget

- No startup network checks, timer or automatic polling.
- At most one request per distinct linked Nexus project per successful 24-hour
  cache period, including projects whose installed download is unidentified.
- Requests start at least one second apart. Failed/cancelled attempts have a
  15-minute cooldown. Nexus rate-limit instructions may pause all projects longer.
- Checks stop before consuming the last five reported requests. Cancel stops
  further work; completed checks remain cached.
- A file lease prevents parallel scans against the same cache. API metadata is
  stored in `Data/NexusUpdateChecks.json`, without keys or signed download URLs.

**Refresh Online Mod Information** remains a separate metadata refresh command.
mod.io update checking is not included.
