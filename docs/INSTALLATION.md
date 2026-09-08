# Installation, updates, and removal

Redux is a portable Windows application. It does not currently use an installer and it does not
need to be placed inside the Baldur's Gate 3 directory.

## Requirements

- Windows 10 or Windows 11, x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Baldur's Gate 3

Linux, macOS, Wine, and Proton are not supported. Redux is framework-dependent rather than a
self-contained application.

## Install Redux

1. Download the complete archive from the official Redux Nexus Mods page or an official GitHub
   release.
2. Create a dedicated writable folder for Redux. Avoid the BG3 installation directory, Windows
   system folders, and running directly from a compressed archive.
3. Extract every file while preserving the archive's folder structure.
4. Run `BG3ModManager.exe`.
5. Review the detected BG3, profile, Mods, saves, and Script Extender paths before making changes.
6. Complete Welcome Setup. Online source information, Load Order Advisor guidance, NXM handling,
   and retained package archives remain optional.

Windows may warn about an unsigned or unfamiliar alpha executable. Verify that the archive came
from an official Redux channel before continuing. Never download a repackaged build from an
untrusted mirror.

## Update a private-alpha build

Application self-updating is disabled in alpha.14. Update manually:

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
