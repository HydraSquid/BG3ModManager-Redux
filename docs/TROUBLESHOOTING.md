# Troubleshooting Redux

Start with the smallest safe check. Do not delete settings, game files, or the entire Redux folder
to troubleshoot an unknown problem.

## Redux does not start

1. Confirm the full archive was extracted and `.NET 8 Desktop Runtime` is installed.
2. Confirm you are launching `Redux.exe`. `BG3ModManager.exe` is the obsolete private-alpha runtime
   name and should not be used for alpha.15 or later.
3. Move Redux to a normal writable folder if it is inside a ZIP, the BG3 directory, or a protected
   system location.
4. Look for `startup_crash.log` or the newest log under Redux's `_Logs` directory.
5. Back up runtime state before changing or removing any file.
6. Report the exact Redux version and the first relevant exception. Redact personal paths and never
   include credentials.

Do not repeatedly launch Redux while an updater, antivirus scanner, cloud-sync client, or earlier
Redux process is still replacing or locking its files.

## An application update fails

Redux verifies and extracts an update before closing. If that preparation fails, the existing
installation remains unchanged and **Try Again** is available. If Redux cannot finish closing—for
example, because its download queue cannot be saved—the queued update is cancelled rather than
being applied later without warning.

The updater changes only release-inventory files and attempts to restore those files when a
replacement fails. Check write access to the Redux folder, close tools that may lock its binaries,
and retry. If the files update successfully but Redux cannot restart automatically, launch
`Redux.exe` yourself; the completed update is still valid. Preferences, downloads,
archives, saved orders, and mods are not application-update targets. If the helper cannot run,
download the complete archive from the official release page and follow the manual update steps in
[Installation, updates, and removal](INSTALLATION.md).

## Paths or profiles are wrong

Open Preferences and verify the BG3 executable, Mods, profiles, saves, and Script Extender paths.
Selecting a different profile changes the active game context; confirm the profile and campaign
before installing, deleting, or syncing.

## NXM links open the wrong application

Open Download Manager and inspect the NXM association state. Use **Repair NXM Links** when Redux
owns a registration that still points to an earlier executable location. Use **Disable NXM Links**
to restore the previous per-user handler where Redux has a valid recovery snapshot.

An NXM link may need to be requested again when its temporary Nexus authorization has expired.
Redux never persists signed download URLs or temporary authorization values.

## A download is incomplete or needs a new link

Open Download Manager and read the item's current action. Free-account downloads may require a
fresh matching Mod Manager Download link. A completed file is revalidated before installation; a
missing, changed, truncated, or unsupported archive will not be treated as complete.

Clearing installed history does not uninstall content. Clearing the Package Archive Library does
not uninstall content or clear download history. Removing an unfinished item and deleting its
partial file are explicit, separate choices.

## A package will not install

Read the install review rather than bypassing it. Redux blocks unknown game-directory DLL layouts,
unsafe archive paths, ambiguous mixed packages, invalid saves, and packages that changed after
review. Ordinary PAK installs enter Inactive Mods and never silently activate, reorder, or sync.

For a native replacer installed outside Redux, remove the replacer, verify BG3 through Steam or GOG,
then install the reviewed package through Redux. Redux must never preserve a modded DLL as the clean
game backup.

## Load order changes do not appear in game

Saving a Redux load order and syncing it to BG3 are separate actions. Confirm the correct profile,
save the intended order, then use **Sync Load Order to Game** and review the proposed changes.
Redux will refuse an unsafe undo when another program or the game changed `modsettings.lsx` after
Redux's write.

## A visual or interaction problem appears

Record the active theme, font, text size, Reduce Motion setting, background-effects setting,
Windows scaling, and approximate window size. Test whether the problem persists with Redux Dark,
Manrope, Default text size, and a normal window size; this narrows the report without erasing the
original appearance settings.

## Prepare a useful report

Include:

- Redux and Windows versions;
- BG3 patch/hotfix when relevant;
- the smallest repeatable steps;
- expected and actual behavior;
- affected mod names or UUIDs; and
- a screenshot or the narrow relevant log excerpt.

Do not attach PAKs, saves, API keys, signed URLs, complete user directories, or somebody else's mod
archive unless its author and the support channel explicitly permit it. See [Support](SUPPORT.md)
and [Privacy and local data](PRIVACY_AND_DATA.md).
