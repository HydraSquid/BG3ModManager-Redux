# Contributing to BG3 Mod Manager Redux

Focused bug reports, documentation improvements, tested fixes, and reviewed mod-identification data
are welcome. Redux is still in alpha, so discuss large architectural or product changes in an issue
before investing in an implementation.

## Before changing code

- Read the [documentation index](docs/README.md) and
  [changes from upstream](docs/CHANGES_FROM_UPSTREAM.md).
- Search existing issues and recent commits.
- Preserve inherited BG3MM behavior unless the change explicitly replaces it.
- Reuse Redux services, shared semantic resources, controls, and terminology.
- Never include credentials, user state, private paths, downloaded mods, saves, or build outputs.

## Build and validate

The supported local workflow is:

```powershell
.\Build-Redux.ps1 -Configuration Debug
.\Test-Redux.ps1
git diff --check
```

Changes affecting release packaging should also build the Publish configuration. Filesystem,
archive, download, native-mod, and load-order changes need tests for failure, cancellation, and
recovery—not only the success path. Visual changes require inspection across built-in themes,
supported text sizes, common Windows scaling, and Reduce Motion.

## Pull requests

Keep a pull request cohesive. Explain the user-visible result, safety boundary, validation, and any
remaining manual-testing requirement. Do not combine an unrelated refactor with a focused fix.

Update durable documentation when behavior, action names, settings, public formats, trust rules, or
support boundaries change. Keep chronological release details in [CHANGELOG.md](CHANGELOG.md).

## Mod database contributions

Do not submit mod archives or PAKs. Generate a privacy-limited `.bg3redux-report`, independently
verify its provider identity, and follow the [Redux mod database guide](docs/REDUX_MOD_DATABASE.md).
Reports are evidence for review; they are never merged automatically.

## Attribution

Redux is a fork of LaughingLeader's BG3 Mod Manager and includes third-party components and assets.
Preserve copyright, provenance, and required notices. Do not rename copied work to conceal its
origin. Review [Third-Party Notices](licenses/Third-Party-Notices.md) before adding a dependency,
font, icon set, dataset, or substantially adapted code.
