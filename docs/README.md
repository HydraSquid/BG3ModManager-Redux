# Redux documentation

Use this page to find the smallest guide that answers your question. The main
[project README](../README.md) is the best starting point for installing, testing, or getting a
quick picture of Redux.

## Choose a path

| I want to… | Start here |
|:--|:--|
| Understand Redux as a user | [Project README](../README.md) |
| Install, update, move, roll back, or remove Redux | [Installation and updates](INSTALLATION.md) |
| Verify or recover a public-alpha release | [Public-alpha releases and update recovery](PUBLIC_ALPHA_RELEASES.md) |
| Fix a startup, path, download, or sync problem | [Troubleshooting](TROUBLESHOOTING.md) |
| Understand what Redux stores or sends | [Privacy and local data](PRIVACY_AND_DATA.md) |
| Ask for help or report a problem | [Support](SUPPORT.md) |
| See what Redux retains and changes from BG3MM | [Changes from upstream](CHANGES_FROM_UPSTREAM.md) |
| Understand diagnostics, online information, or the Load Order Advisor | [Feature boundaries](REDUX_OPTIONAL_MODULES.md) |
| Help improve offline mod recognition | [Redux mod database](REDUX_MOD_DATABASE.md) |
| Check a PAK or release archive before publishing it | [Mod developer tools](MOD_DEVELOPER_TOOLS.md) |
| Understand native and root-level install safeguards | [Changes from upstream](CHANGES_FROM_UPSTREAM.md#game-directory-mod-management) |
| Understand NXM handling and download safeguards | [Changes from upstream](CHANGES_FROM_UPSTREAM.md#nexus-mod-manager-downloads) |
| Add stable provider identity to a PAK | [Creator manifest](REDUX_CREATOR_MANIFEST.md) |

## Author and maintainer references

- [Creator manifest JSON schema](schemas/redux.mod.schema.json)
- [Database maintenance CLI and desktop reviewer](../tools/ReduxModDatabaseTool/README.md)
- [Windows build and regression workflow](../.github/workflows/windows-ci.yml)
- [Third-party notices](../licenses/Third-Party-Notices.md)
- [Contribution guide](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Changelog](CHANGELOG.md)

## Terms used throughout the docs

- **Mod Diagnostics** means the built-in, read-only checks that explain package and dependency
  conditions.
- **Load Order Advisor** means the optional guidance layer and its user-reviewed organizer.
- **Separator** means a Redux visual grouping marker. Separators never enter `modsettings.lsx`.
- **Redux Modlist** means the portable `.bg3redux` format. It does not contain PAKs or saves.
- **Contribution report** means a privacy-limited `.bg3redux-report` prepared for database review.
- **Game-directory mod** means a reviewed native or root-level package installed outside the normal
  PAK Mods folder and managed through Redux's separate guarded workflow.
- **Download Manager** means Redux's shared local-package and optional NXM acquisition inbox.
- **Package Archive Library** means the separate, opt-in, quota-limited store of verified install
  packages available from Download Manager's Archives tab.
  Acquisition remains separate from package inspection, installation, activation, ordering, and
  game sync.

> [!IMPORTANT]
> Review logs, screenshots, and reports before sharing them. Contribution reports are designed to
> exclude credentials and private paths, but API keys or other private information should never be
> posted publicly.
