# Mod developer tools

Redux includes a read-only release preflight for mod authors and maintainers. It helps catch common
packaging and metadata problems before a file is published, without installing or changing the
selected artifact.

## Inspect a package

Open **Tools > Inspect Mod Package...** and choose a PAK or supported ZIP, 7z, RAR, TAR, or
GZip-family archive.

Redux reports:

- parsed module name, folder, UUID, author, and version;
- declared dependencies and whether matching packages are installed;
- Script Extender configuration, Osiris scripting, Mod Fixer files, and always-loaded overrides;
- a validated or rejected embedded [`redux.mod.json`](REDUX_CREATOR_MANIFEST.md);
- development files that may have been included accidentally; and
- installed packages that use the same module UUID.

For a release archive, Redux inspects every contained PAK and also checks the outer archive for
unsafe paths, duplicate PAK filenames, development debris, and an accidentally bundled
`modsettings.lsx`.

## Understand the result

| Result | Meaning |
|:--|:--|
| Blocking issue | The package or archive has a structural or identity problem that should be fixed before release. |
| Review recommended | Redux found something intentional in some mods but worth confirming. |
| No blocking issue | The inspected checks passed. This is not a compatibility guarantee. |

The preflight never installs, extracts into the Mods folder, edits, registers, sorts, activates, or
exports the selected package. It also does not run the mod or prove that it behaves correctly in
game.

## Add stable source identity

Authors may place a root-level `redux.mod.json` inside a PAK. Redux validates the claim against the
PAK's parsed `meta.lsx` before using it to associate the installed file with Nexus Mods or mod.io.
The compact Nexus form needs only a module UUID and Nexus project ID; the detailed form supports
multiple modules, mod.io, fallback metadata, and informational dependencies.

See the [creator manifest guide](REDUX_CREATOR_MANIFEST.md) for examples, placement rules, and the
trust model. The [machine-readable schema](schemas/redux.mod.schema.json) is authoritative.

## Before publishing

1. Inspect the exact PAK or archive you plan to upload.
2. Confirm every UUID, dependency, and provider ID against the release itself.
3. Remove development debris and user-specific files.
4. Rebuild, then inspect the final artifact again.
5. Test the release in a clean game profile; preflight cannot replace in-game testing.
