# Redux documentation

This directory separates everyday product guidance from author and maintainer references. Start
with the README for the public overview, then use the smallest guide that fits the question.

## Product and contribution guides

| Guide | Best for | Covers |
|:--|:--|:--|
| [Project README](../README.md) | Users and testers | Features, safety boundaries, requirements, and reporting |
| [Changes from upstream](CHANGES_FROM_UPSTREAM.md) | Users and contributors | What BG3MM provides and what Redux changes or owns |
| [Optional features](REDUX_OPTIONAL_MODULES.md) | Contributors | Online-source and Load Order Advisor boundaries |
| [Redux mod database](REDUX_MOD_DATABASE.md) | Contributors and maintainers | Recognition policy, advisor knowledge, and contribution review |

## Mod-author references

| Guide | Covers |
|:--|:--|
| [Mod developer tools](MOD_DEVELOPER_TOOLS.md) | Read-only package and release-archive preflight |
| [Creator manifest](REDUX_CREATOR_MANIFEST.md) | Optional validated provider identity inside a PAK |
| [Creator manifest schema](schemas/redux.mod.schema.json) | Machine-readable `redux.mod.json` definition |

## Maintainer references

- [Database maintenance tool](../tools/ReduxModDatabaseTool/README.md)
- [Third-party notices](../licenses/Third-Party-Notices.md)
- [Windows build and regression workflow](../.github/workflows/windows-ci.yml)

> [!IMPORTANT]
> Contribution reports, logs, and screenshots can contain contextual information. Never publish API
> keys, credentials, or private filesystem details. `.bg3redux-report` files are designed to exclude
> those values, but should still be reviewed before sharing.
