# Manual Nexus mod update checks

Open **Tools → Check Nexus Mod Updates…**, then choose **Check now**. Opening the
window only displays existing cached results. A Nexus API key is required for
requests; **Disable online mod information** must be unchecked in Preferences.

## Request budget

- No startup checks, timer, or automatic polling.
- One file-list request per distinct Nexus project, including projects shared by
  multiple installed PAKs. Projects without an identifiable installed file are
  shown for manual review without spending a request.
- Successful project checks are cached for **24 hours**, across application restarts.
- Requests start at least one second apart. Failed/interrupted attempts have a
  **15-minute cooldown**; Nexus rate limits and longer retry instructions can pause
  the whole check. Checks stop before consuming the last five reported requests.
- Cancel stops further requests. Completed checks remain cached. A file lock
  prevents another checker using the same cache from launching a parallel scan.

The cache is `Data/NexusUpdateChecks.json`, with atomic replacement and a backup.
It stores public file IDs, names, versions, replacement relationships and check
times. It stores no API keys or signed download links.

## What the result means

- **Update available:** Nexus explicitly connects the recorded installed file to
  an available replacement through its file-update history.
- **No update reported:** Nexus lists the recorded file as current and reports no
  replacement. This does **not** prove there are no newer unlinked uploads.
- **Needs review:** the installed file is not reliably identified, or Nexus's
  replacement history is ambiguous, cyclic, archived or incomplete.
- **Check failed / Not checked:** no current conclusion is available. Old results
  retain their last successful check time rather than becoming “up to date.”

Imported Nexus archives and exact bundled PAK/archive matches supply file-level
provenance. A project-only/manual link, creator manifest or imported order bundle
does not prove which file is installed. A file replaced outside Redux can also
leave old recorded provenance; compare the displayed local version and file ID.

The checker follows Nexus's explicit `file_updates` relationships, not whichever
file has the highest ID, a newer project version, or the primary-download flag.
Optional patches are therefore not silently substituted for a main file. These
relationships exist only when the author marks a file as replacing another:
see the [official Nexus API client's file contracts](https://github.com/Nexus-Mods/node-nexus-api/blob/master/src/types.ts).

**Open Nexus Files** opens the project's Files page, highlighting the replacement
when one is known. Checks do not download, install, activate, reorder or export
mods. Use the existing reviewed Downloads workflow for any update you choose.

The existing **Refresh Online Mod Information** command refreshes provider
metadata; it is separate from this file-replacement checker. mod.io update
checking is not included.
