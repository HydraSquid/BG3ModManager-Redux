# Installation, updates, and removal

Redux is a portable Windows application and does not need to be placed inside the Baldur's Gate 3
directory. Public-alpha releases support both manual extraction and a separate lightweight Setup.

## Requirements

- Windows 10 or Windows 11, x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Baldur's Gate 3

Linux, macOS, Wine, and Proton are not supported. Redux is framework-dependent rather than a
self-contained application.

## Install Redux

### Lightweight Setup (public alpha)

Download `BG3ModManager-Redux-Setup.exe` from the official Redux release. Setup is a small web
bootstrapper: Redux is not packed inside it. Before downloading the current public-alpha archive,
Setup shows its exact version and size, checks the required x64 .NET 8 Desktop Runtime, and lets you
review the detected game and per-user data paths.

Setup defaults to a writable per-user application folder, accepts another empty location, and
refuses to install inside the Baldur's Gate 3 directory. The downloaded archive must match the
fixed official GitHub channel, declared byte length, SHA-256 hash, safe archive paths, and release
inventory before Setup commits it. Setup adds a Start Menu shortcut, offers an optional Desktop
shortcut, and registers a normal per-user Windows uninstall entry.

If .NET 8 is missing, Setup can download its x64 Desktop Runtime installer directly from Microsoft.
It verifies the final Microsoft download location and Authenticode signer before launching it, and
also offers the official Microsoft download page as a fallback.

### Portable archive

1. Download the complete archive from the official Redux Nexus Mods page or an official GitHub
   release.
2. Create a dedicated writable folder for Redux. Avoid the BG3 installation directory, Windows
   system folders, and running directly from a compressed archive.
3. Extract every file while preserving the archive's folder structure.
4. Run `Redux.exe`.
5. Review the detected BG3, profile, Mods, saves, and Script Extender paths before making changes.
6. Complete Welcome Setup. Online source information, Load Order Advisor guidance, NXM handling,
   and retained package archives remain optional.

Windows may warn about an unsigned or unfamiliar alpha executable. Verify that the archive came
from an official Redux channel before continuing. Never download a repackaged build from an
untrusted mirror.

## Move from a private-alpha build

Private-alpha builds are updated manually when moving to alpha.15:

1. Close BG3 and Redux. Wait for active downloads or file operations to finish.
2. Back up the entire Redux folder, especially its runtime-state directories.
3. Extract the complete new archive over the existing Redux folder.
4. Start Redux and confirm the version shown in the title bar or About window.
5. Verify the selected profile and saved order before syncing the game load order.
6. If the executable moved, repair Redux's NXM association from Download Manager.

The release packager deliberately excludes user state, including `Data`, `orders`, `_Logs`, caches,
downloads, retained archives, and backups. An update archive should therefore replace application
files without supplying somebody else's runtime state. Do not interpret that exclusion as
permission to delete your existing state folders.

## Update a public-alpha build

Public-alpha builds can check Redux's official GitHub update channel in the background. When a
newer release is available, Redux shows the version and official release notes before offering
**Update & Restart**. Choosing **Later** leaves the current installation unchanged.

The update is downloaded into per-user temporary staging, checked against the release's declared
byte length and SHA-256 hash, and inspected before Redux closes. A separate narrow updater then
replaces only files named in Redux's release inventory. Files outside that inventory—including
preferences, downloads, retained archives, saved orders, logs, and user-created files—are not
claimed or removed. If file replacement fails, the updater restores the files it backed up before
the transaction.

The built-in updater does not install Redux on a new computer and is separate from standalone
Setup. Setup performs fresh installations only; it does not update, repair, or move an existing
installation. A protected or read-only installation location can prevent an in-place update; use a
writable application folder or update manually in that case.

## Move Redux

Close Redux before moving its folder. Keep the entire directory together so settings, saved orders,
download state, custom appearance assets, and backups remain available. After moving:

- launch the executable from the new folder;
- review every configured path;
- repair the NXM association if Redux owns it; and
- update shortcuts that point to the previous executable.

## Roll back

Keep a backup of the previous working Redux folder before updating. To roll back, close Redux and
restore that complete backup. Settings written by a newer alpha may not be understood by a much
older build, so restoring only old binaries into newer runtime state is not a reliable rollback.

Never use an application rollback to roll back `modsettings.lsx`, installed PAKs, saves, or managed
game-directory files. Use Redux's relevant restore, deletion, Save Game Manager, or Game-Directory
Mod Manager workflows for those items.

## Remove Redux

Removing the application does not uninstall mods, saves, Script Extender, or game-directory mods.

If Redux was installed with lightweight Setup, use the normal **BG3 Mod Manager Redux** entry in
Windows Installed Apps. Its uninstaller removes only files owned by the installed release inventory
and preserves unlisted content in the application folder.

For a manually extracted portable copy:

1. Finish or cancel active downloads and close BG3.
2. If Redux handles `nxm://` links, use **Disable NXM Links** so it can restore the previous handler
   where possible.
3. Remove Redux-managed game-directory mods through Game-Directory Mod Manager if you want those
   files removed or restored.
4. Close Redux.
5. Keep any saved orders, archives, logs, or backups you still need, then remove the Redux folder.

If Redux cannot start and still owns the NXM association, reinstall or restore the same Redux folder
long enough to disable the association, or repair the Windows default-app/protocol setting manually.

## Back up the right things

Before testing an alpha update, independently back up:

- the complete Redux folder;
- the BG3 Mods folder;
- important saves;
- `modsettings.lsx` and any profiles you cannot recreate; and
- original downloaded archives that are not retained by Redux.

Redux's restore points and Package Archive Library are useful recovery tools, but neither replaces
an independent backup.
