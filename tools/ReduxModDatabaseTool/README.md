# Redux Mod Database Tool

This maintainer utility validates and prepares conservative updates to
`src/GUI/Resources/ReduxModDatabase.json`. It is deliberately separate from Redux runtime behavior:
it does not contact providers, store credentials, guess a project from a filename, or write without
an explicit `--write` flag.

For the database trust model and user contribution flow, read the
[Redux mod database guide](../../docs/REDUX_MOD_DATABASE.md).

## Quick reference

Run commands from the repository root.

| Command | Purpose | Writes by default? |
|:--|:--|:--:|
| `validate` | Check schema, counts, references, hashes, group cycles, and collisions | No |
| `fingerprint` | Calculate Redux's exact PAK or archive fingerprint | No |
| `review-report` | Privacy-audit and classify a contribution report | Only the requested review output |
| `accept-report` | Preview selected, independently reviewed report records | No |
| `add` | Preview one project and exact-artifact addition | No |

## Validate the database

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- validate
dotnet run --project tools/ReduxModDatabaseTool -- validate --database "C:\Redux\ReduxModDatabase.json"
```

Validation covers the complete database, including source identities, exact-match collisions,
ordering-group references and cycles, dependency aliases and substitutes, supported match policy,
and recorded counts. Run it before and after every accepted update.

## Fingerprint one artifact

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- fingerprint --file "C:\Mods\Example.pak"
```

PAKs use xxHash64 encoded as Base64 from the little-endian 64-bit value. Archives use lowercase MD5.
Both include exact byte length. These values identify an exact artifact; they are not signatures of
authorship or safety.

## Review a contribution report

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- review-report `
  --file "C:\Reports\Contribution.bg3redux-report" `
  --output "C:\Reports\Contribution.review.json"
```

The command validates the report schema and privacy declaration, rejects embedded path data,
non-public provider URLs, invalid UUID fallbacks, and inconsistent fingerprints, then compares each
record with the current database. Results are grouped into new project candidates, known projects,
already-known packages, conflicts, non-Nexus records, and unavailable fingerprints. The database is
not changed.

## Accept reviewed report records

After independently confirming the Nexus projects, preview one project or a selected batch:

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- accept-report `
  --file "C:\Reports\Contribution.bg3redux-report" `
  --mod-id 123

dotnet run --project tools/ReduxModDatabaseTool -- accept-report `
  --file "C:\Reports\Contribution.bg3redux-report" `
  --mod-ids 123,456,789
```

Acceptance requires exact fingerprints and verified project IDs. It rejects collisions and does not
promote UUIDs into reviewed module identities. Nexus file IDs are retained when present; exact PAK
records use `-1` when a file ID is unavailable.

Review the preview, then repeat the same command with `--write`. The selected batch is validated and
written as one atomic replacement.

## Add one reviewed artifact

```powershell
dotnet run --project tools/ReduxModDatabaseTool -- add `
  --file "C:\Mods\Example.pak" `
  --mod-id 123 `
  --file-id 456 `
  --name "Example Mod" `
  --author "Example Author" `
  --version "1.0"
```

Useful options:

| Option | Meaning |
|:--|:--|
| `--authors <a,b>` | Multiple project authors |
| `--aliases <a,b>` | Additional reviewed project names |
| `--category <name>` | Nexus/Redux category metadata |
| `--picture-url <url>` | Public Nexus image URL |
| `--logical-file-name <name>` | Archive display filename |
| `--module-uuid <uuid>` | Optional reviewed module identity |
| `--module-name <name>` | Reviewed module name |
| `--module-folder <folder>` | Reviewed module folder |
| `--module-files <a.pak,b.pak>` | Expected PAK filenames |
| `--database <path>` | Override automatic database discovery |
| `--write` | Atomically apply a validated preview |

The `add` command is also preview-only until `--write` is present. A module UUID should be promoted
only when maintainers have established that it reliably identifies one project.

## Desktop reviewer

The repository contains a compact private maintainer interface over the same validation and
acceptance code:

```powershell
dotnet run --project tools/ReduxModDatabaseTool.Desktop
```

It opens a contribution report, displays duplicate and conflict classifications, lets a maintainer
select verified projects, and exposes the write action only after a successful preview. It is not
included in public Redux tester packages.

From a checkout, the reviewer finds the bundled database automatically. A portable copy can place
`ReduxModDatabase.json` beside the executable or in a `Resources` subfolder, or receive paths
explicitly:

```powershell
ReduxModDatabaseReviewer.exe `
  --report "C:\Reports\Contribution.bg3redux-report" `
  --database "C:\Redux\ReduxModDatabase.json"
```

Use `--help` for the command-line contract. A successful preview is still not a substitute for
checking the real provider project and exact release artifact.
