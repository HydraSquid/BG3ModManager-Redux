# Community and publishing guide

Redux has several public surfaces, but they should describe one product and direct each kind of
report to the right place. This guide is the operating model for GitHub, Nexus Mods, the website,
Discord, announcements, and community support content.

## Channel roles

| Channel | Primary role | Keep there |
|:--|:--|:--|
| [Website](https://bg3mm-redux.com) | Fast product overview and trusted navigation | Positioning, feature highlights, screenshots, download routes, requirements |
| [Nexus Mods](https://www.nexusmods.com/baldursgate3/mods/23799) | Main player-facing discovery and distribution | Current files, changelog summary, install guidance, images, requirements, compatibility notes |
| [GitHub](https://github.com/circleainn/BG3ModManager-Redux) | Source, immutable releases, tracked defects, security policy | Code, docs, release artifacts, issues, contribution and recovery guidance |
| [Discord](https://discord.gg/rJJF89vqFZ) | Community help, discussion, announcements, and feedback | Friendly support, known-issue links, setup sharing, release announcements |
| [Ko-fi](https://ko-fi.com/circleain) | Optional support for continued work | A simple non-blocking support link |

Security vulnerabilities belong in GitHub private vulnerability reporting. Reproducible defects
belong in GitHub issues even if they are first discussed on Discord. Mod-specific gameplay problems
belong with the mod author, and account/service problems belong with the relevant provider.

## Shared public facts

Every channel should agree on these facts:

- product: **Baldur's Gate 3 Mod Manager Redux**;
- tagline: **Bring order to the chaos.**;
- current line: **0.1.0-alpha.16.1 — public alpha hotfix**;
- platform: Windows 10/11 x64 with .NET 8 Desktop Runtime;
- application filename: `Redux.exe`;
- installation choices: verified lightweight Setup or full portable archive;
- official distribution: GitHub Releases and Nexus Mods;
- upstream lineage and unofficial-project disclaimer; and
- public-alpha backup, review, and limitation language.

When a version changes, update application metadata, README badge, changelog, project status, issue
template placeholder, release artifact names, website, Nexus page, Discord announcement, and public
FAQ together.

## Nexus Mods page structure

Use this order so a first-time visitor can decide safely without reading a wall of detail:

1. Product name, tagline, public-alpha label, and one-sentence description.
2. A current 16:9 hero image made from real application-rendered UI.
3. Clear **Requirements** and **Install** sections with Setup and portable choices.
4. A concise feature grid: organization, deliberate load-order review, Download Manager, save and
   game-directory management, themes/accessibility, and offline recognition.
5. A **How Redux differs from BG3MM** summary that preserves upstream credit.
6. Safety boundaries: backups, read-only diagnostics, preview-before-sync, reviewed native layouts,
   and what Redux never does automatically.
7. Known public-alpha limitations and the supported reporting routes.
8. Changelog, credits, source, privacy, Discord, and license links.

The Files tab should make Setup versus portable use obvious. Do not describe Setup as a bundled
offline installer: it is a small web bootstrapper that fetches and verifies the portable release.
Do not upload two differently built files under the same version and filename.

## Website structure

The website should stay lighter than the README:

- hero: full product name, tagline, short description, primary Nexus download, secondary GitHub link;
- proof: authentic current UI, not recreated or synthetic controls;
- core workflow: install/inspect, organize, save, review/sync;
- feature sections for Categories and drawer, load-order comparison/advisor, Download Manager,
  Save Game Manager, Game-Directory Mod Manager, and custom themes;
- public-alpha requirements and limits;
- upstream credit and unofficial-project disclaimer; and
- footer links to docs, support, Discord, privacy, GitHub, Nexus, and Ko-fi.

Avoid copying every README paragraph. The website explains why Redux matters, while the repository
docs explain exact behavior and recovery.

## Discord structure

A compact server can support public alpha without becoming a second untracked issue database:

- **Start here**: `#welcome-and-rules`, `#announcements`, `#getting-started`, `#faq`;
- **Support**: `#help-and-support`, `#known-issues`, `#bug-report-links`;
- **Community**: `#general`, `#load-orders-and-setups`, `#themes-and-showcase`;
- **Creators**: `#mod-authors`, `#database-contributions`;
- **Development**: `#development`, `#testing-feedback`;
- **Staff**: private moderation, triage, and release-coordination channels.

Recommended pinned messages:

- only download from the official Nexus Mods page or GitHub Releases;
- back up important mods, profiles, saves, and `modsettings.lsx` during public alpha;
- never post API keys, signed links, full logs, saves, or copyrighted mod archives;
- use GitHub for reproducible bugs and private reporting for vulnerabilities;
- explain that Redux support cannot replace a mod author's compatibility support; and
- link the current changelog, installation guide, troubleshooting guide, and known limitations.

Use roles sparingly: maintainer, moderator, contributor, tester, mod author, and optional release
notifications are enough for a small server. Do not grant moderation or repository authority based
only on a self-selected role.

## Coordinated release sequence

1. Freeze the reviewed source commit and regenerate every packaged document and artifact.
2. Record hashes and complete local artifact tests.
3. Push and verify `dev` and `main` CI.
4. Create the immutable versioned GitHub release and upload the tested portable ZIP and Setup.
5. Download them anonymously and verify bytes, launch, install, and uninstall.
6. Upload the intended files and current images to Nexus Mods; verify download parity.
7. Publish `Redux-Update-Public-Alpha.json` to the moving channel only after versioned URLs work.
8. Test a fresh Setup install, a Setup-managed update, and the in-app update path through the public
   channel where applicable.
9. Update website state and publish matching Discord, Nexus, and GitHub announcements.
10. Monitor support and security routes; never silently replace versioned artifacts.

Use [Public-alpha releases and update recovery](PUBLIC_ALPHA_RELEASES.md) as the authoritative
artifact and withdrawal procedure.
