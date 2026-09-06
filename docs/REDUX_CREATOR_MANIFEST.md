# Redux creator manifest

`redux.mod.json` is optional, declarative metadata that lets a mod author attach stable provider
identity to a PAK. Redux validates it against the package's parsed `meta.lsx`; it is never executed
and cannot install files or change a user's load order.

Most Nexus authors should use the compact form. The detailed form is available for multi-module
packages, mod.io identity, offline fallback metadata, or informational dependency claims.

## Where the file belongs

Place `redux.mod.json` at the virtual root of every distributed PAK. A multi-PAK release should put
a manifest in each PAK, and each manifest should describe only the modules in that PAK.

An outer release archive may also contain a root-level copy for import-time discovery. That copy is
only a convenience: embedded PAK metadata remains authoritative and is validated independently.
Conflicting archive claims are rejected.

## Compact Nexus manifest

```json
{
  "$schema": "https://raw.githubusercontent.com/circleainn/BG3ModManager-Redux/main/docs/schemas/redux.mod.schema.json",
  "schemaVersion": 1,
  "manifestType": "bg3-redux-mod",
  "moduleUuid": "11111111-2222-3333-4444-555555555555",
  "nexus": {
    "projectId": 12345
  }
}
```

Replace:

- `moduleUuid` with the primary module UUID from the PAK's `meta.lsx`;
- `projectId` with the numeric ID at the end of its Nexus Mods page URL.

An optional positive `fileId` can identify a specific Nexus file, but it is not required to connect
the installed package with its project. Place the completed JSON at the virtual root before building
the PAK; Redux does not require a separate post-build tool.

## Detailed manifest

Use the detailed form when the compact Nexus identity is not sufficient:

```json
{
  "$schema": "https://raw.githubusercontent.com/circleainn/BG3ModManager-Redux/main/docs/schemas/redux.mod.schema.json",
  "schemaVersion": 1,
  "manifestType": "bg3-redux-mod",
  "mod": {
    "name": "Example Mod",
    "version": "1.2.0",
    "authors": ["Example Author"],
    "description": "Optional offline fallback description.",
    "homepage": "https://example.invalid/mod",
    "sources": [
      {
        "service": "nexus",
        "projectId": 12345,
        "fileId": 67890
      }
    ],
    "modules": [
      {
        "uuid": "11111111-2222-3333-4444-555555555555",
        "name": "Example Mod",
        "folder": "ExampleMod",
        "version": "1.2.0",
        "pak": "ExampleMod.pak"
      }
    ],
    "dependencies": [
      {
        "uuid": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        "name": "Example Library",
        "minimumVersion": "2.0.0",
        "optional": false
      }
    ]
  }
}
```

`sources[].service` accepts `nexus` or `modio`. Provider and file IDs must be positive integers.
Dependencies are informational claims; the package's own parsed metadata and Redux's normal
diagnostic rules remain authoritative for user-facing checks.

The compact and detailed forms are intentionally exclusive. Do not place `moduleUuid` or `nexus`
beside `mod` in one manifest.

## Validation and precedence

Redux treats the manifest as an author-supplied claim, not proof by itself.

- Claimed UUIDs, folders, names, versions, and PAK filenames must agree with parsed package data.
- Unknown properties and unsupported schema versions fail closed.
- Duplicate authors, invalid URLs, unrelated modules, and conflicting PAK claims are rejected.
- A source claim cannot replace an explicit manual link or manual unlink.
- A manifest cannot contain commands, hooks, credentials, absolute paths, or deletion instructions.
- It cannot move mods, activate packages, rewrite `modsettings.lsx`, or bypass a review dialog.

A valid claim joins Redux's normal source-resolution pipeline. Cached or live provider data may add
the current name, author, description, image, or version history. If the installed PAK later loses
the manifest or changes its claimed project, the cached creator association is revalidated and
discarded when it no longer matches.

Invalid claims are ignored and shown as a non-destructive Mod Diagnostics finding. The package
itself is not edited.

## Author checklist

1. Use schema version `1` and manifest type `bg3-redux-mod`.
2. Copy identity values from the final PAK, not an earlier project state.
3. Include only modules actually contained by that PAK.
4. Use public HTTP or HTTPS URLs and never include credentials or private paths.
5. Validate against the [JSON schema](schemas/redux.mod.schema.json).
6. Run **Tools > Inspect Mod Package...** against the final release artifact.

The [schema](schemas/redux.mod.schema.json) is the authoritative field and length definition. This
guide explains intended use; it does not override the schema.
