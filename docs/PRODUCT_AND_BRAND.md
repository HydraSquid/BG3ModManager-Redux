# Product and brand guide

This guide keeps Redux recognizable across the application, repository, website, Nexus Mods page,
Discord, screenshots, and generated support content. It describes the public identity; the WPF
theme resources remain authoritative for live interface colors and control behavior.

## Canonical identity

| Use | Canonical form |
|:--|:--|
| Full product name | **Baldur's Gate 3 Mod Manager Redux** |
| Compact product name | **BG3 Mod Manager Redux** |
| Short name | **Redux** |
| Tagline | **Bring order to the chaos.** |
| Version | **0.1.0-alpha.16** |
| Lifecycle | **Public alpha** |
| Category | Windows mod manager for Baldur's Gate 3 |
| Runtime filename | `Redux.exe` |
| Portable archive | `BG3ModManager-Redux_v0.1.0-alpha.16.zip` |
| Setup filename | `BG3ModManager-Redux-Setup.exe` |

Use the full name on first mention and **Redux** afterward. Do not shorten the product to “BG3MM”
where it could be confused with LaughingLeader's upstream application. Do not call a public alpha a final,
stable, official, or Larian-supported release.

## Core positioning

One-sentence description:

> Redux is a modern, community-driven fork of BG3 Mod Manager that helps players organize, review,
> and safely apply complex Baldur's Gate 3 mod setups.

Short feature statement:

> Organize mods with categories and separators, review diagnostics and load-order changes, manage
> downloads and saves, and personalize the workspace without hiding the decisions that affect BG3.

The product should feel deliberate, capable, transparent, and calm. Lead with the user's result,
then explain the safeguard. Avoid exaggerated claims such as “perfect load orders,” “one-click
compatibility,” “guaranteed safe,” or “supports every mod.”

## Logo and icon

The canonical public mark is the four-point Redux star:

- [`assets/brand/redux-star.svg`](../assets/brand/redux-star.svg) is the scalable transparent brand
  source used by the README and public layouts.
- [`src/GUI/Redux.ico`](../src/GUI/Redux.ico) is the multi-resolution Windows application icon used
  by `Redux.exe` and Setup.

Keep the star's transparent surroundings. Do not place it in an accidental black square, stretch it,
crop its points, add a second container tile, or redraw it synthetically. Provide clear space of
at least one quarter of the mark's visible width when it appears beside headings or other logos.

The SVG gradient is the canonical fixed logo treatment:

- violet `#4F19FF`;
- purple `#8B2DF1`; and
- magenta `#F02FC4`.

Interface accents are theme-dependent and should not be flattened into one marketing hex:

- Redux Dark accent: `#9676FF`;
- Redux Light accent: `#694AD6`;
- Parchment accent: `#8B3034`.

Discord's `#5865F2` is reserved for Discord branding or the Discord link's hover treatment. Success,
warning, error, information, provider, and category colors retain their semantic meaning; they are
not interchangeable decorative accents.

## Visual language

- Dark, spacious compositions are the primary public showcase language, with restrained violet and
  magenta light, subtle depth, and readable contrast.
- Use rounded corners, fine borders, and controlled glow. A glow should separate a real window from
  its background, not create a larger false outline around the captured interface.
- Prefer one clear feature and one authentic Redux surface per image. Secondary elements should
  support that story rather than fill empty space.
- Keep generous safe margins around headings, pills, window chrome, and the frame edge. Never let a
  pill overlap the interface or another caption.
- Gradients should be smooth and low-frequency. Avoid visible banding, stripes, noisy textures, and
  competing color pools.
- Typography should be direct and highly legible. Manrope matches the primary Redux interface;
  avoid novelty fonts for product or instructional copy.

## Screenshot and showcase rules

Public interface imagery must be grounded in the current built application.

1. Capture the current public-alpha `Redux.exe` at a completed layout pass and a known display scale.
2. Use real, publicly identifiable mod names already present in the maintainer's Redux library when
   permission and context allow. Do not invent fake authors, paths, versions, or package IDs.
3. Redact personal usernames, private filesystem paths, API keys, signed URLs, save names, and any
   other sensitive data before compositing.
4. Capture menus, popups, hover cards, and drawers in their real application context. Do not place a
   random context menu over an unrelated feature.
5. Use an opaque, consistent showcase background behind transparent or custom-chrome windows.
6. Preserve the native window's proportions. Crop only empty exterior pixels, and leave enough room
   for shadows, resize rails, scrollbars, title-bar controls, and the complete final row.
7. Inspect every image at full resolution for clipping, overlapping text, oversized outlines,
   transparent holes, stale version labels, synthetic data, typos, and mismatched themes.
8. Use code-based compositing for UI showcases. Decorative backgrounds must never redraw, replace,
   or “repair” the actual Redux interface.

When showing the main workspace, include the Categories pane and selected-mod drawer where they help
explain the feature. For custom-theme material, show the theme editor and color picker responding to
the same live theme rather than using a disconnected mockup.

## Voice and terminology

Use sentence case for headings and controls in prose. Prefer the exact interface names:

- **Active Mods**, **Inactive Mods**, and **Override Mods**;
- **Download Manager**, **Save Game Manager**, and **Game-Directory Mod Manager**;
- **Mod Diagnostics** and **Load Order Advisor**;
- **Organize Active Load Order**, **Sync Load Order to Game**, and **Quick Access**;
- **Redux Modlist** for `.bg3redux`; and
- **Package Archive Library** for retained verified packages.

Write “load order” as two words and “game-directory mod” with a hyphen when it modifies a noun.
Use “Script Extender,” not “the extender,” on first mention. Explain that saving an order and syncing
it to BG3 are separate actions.

Voice principles:

- confident without pretending uncertainty does not exist;
- clear about what changes files and what is read-only;
- respectful of mod authors and upstream BG3MM contributors;
- welcoming to new mod users without talking down to experienced users; and
- concise in interface copy, more explicit in safety and recovery guidance.

## Required attribution and disclaimers

Redux is a fork of
[LaughingLeader's BG3 Mod Manager](https://github.com/LaughingLeader/BG3ModManager). Public pages
must preserve that lineage and link to the upstream project. Baldur's Gate 3 is developed and
published by Larian Studios. Redux is unofficial and is not affiliated with or endorsed by Larian
Studios, Nexus Mods, or mod.io.

Do not imply ownership of third-party mods, provider branding, BG3 assets, upstream code, or bundled
dependencies. Use the repository's [MIT license](../LICENSE) and
[third-party notices](../licenses/Third-Party-Notices.md) as the legal source.

## Official links

| Destination | URL |
|:--|:--|
| Website | <https://bg3mm-redux.com> |
| GitHub | <https://github.com/circleainn/BG3ModManager-Redux> |
| Releases | <https://github.com/circleainn/BG3ModManager-Redux/releases> |
| Issues | <https://github.com/circleainn/BG3ModManager-Redux/issues> |
| Nexus Mods | <https://www.nexusmods.com/baldursgate3/mods/23799> |
| Discord | <https://discord.gg/rJJF89vqFZ> |
| Ko-fi | <https://ko-fi.com/circleain> |
| Upstream BG3MM | <https://github.com/LaughingLeader/BG3ModManager> |
