# Project Agent Contract

## Scope and authority

- Follow system and developer constraints, then the user's explicit instructions. Skills are workflow guidance; they do not override those instructions or expand authorization.
- Read a local `HANDOFF.md` when present and confirm the active worktree and Git state. Work in the checkout the user selected; do not switch to another checkout based on a tool's default directory.
- The consolidated local checkout is `C:\dev\BG3ModManager-Redux` on `main`, tracking `fork/main`. Use it for ongoing work unless the user requests isolation; do not create a worktree for routine changes by default.
- Preserve unrelated and concurrent changes. A completed handoff feature is context, not authorization to resume deployment or start another feature.
- Keep this contract project-specific. Do not copy entire model guides or skill libraries into the repository.

## Execution

- Treat requests for action as authorization to perform the scoped work. Carry it through implementation and appropriate verification rather than stopping at a plan or an offer to continue.
- Infer routine, reversible details from the code and context. Ask a focused question only when the answer materially affects correctness, scope, or an expensive-to-reverse decision. Complete independent authorized work before asking.
- Explicit approval boundaries still apply: commit, push, publish, deploy, change account settings, and non-trivial builds require authorization. Prepare a reviewable result before requesting approval; do not infer approval from a model guide's examples.
- Make the smallest correct change. Preserve working behavior and external contracts; adopt new patterns or capabilities only when needed for the requested outcome.
- Size the process to the task using the host's task-class rules. Do not add brainstorming rounds, plans, subagents, or reviews merely because a skill template suggests them.
- Use parallel tools for independent reads. Delegate bounded research or disjoint implementation only when the task class permits it and doing so improves quality or saves time. Do not duplicate delegated work or use user-facing sessions as helper agents.
- If a skill causes a permission request, pause, unfinished work, or divergence from the user's intent, identify the exact `SKILL.md` path or link, quote the relevant instruction, and explain whether the constraint is explicit or your interpretation. Do not invent an approval requirement.
- Communicate direct, concise progress and outcomes. Use plain language, useful file references, and flat lists when helpful; avoid canned framing and repeated summaries. State verification gaps accurately.

## Repository safeguards

- This is a Windows WPF application with native C++ dependencies. Use the repository's PowerShell scripts and Visual Studio MSBuild; ordinary `dotnet run` is not the supported build/test route.
- Preserve portable runtime `Data`, `Orders`, `_Logs`, download queues and archives, installed mods, and game load order. Never publish runtime data or backups.
- Before an authorized portable deployment, ask the user to save working-order edits and close the application normally. Do not force-kill it or overwrite runtime data.
- Keep API keys, NXM authorization, and signed download URLs out of logs, queue manifests, commits, and reports.
- Dependency assistance must remain conservative: reviewed links or Copy UUID, without automatic selection, download, activation, or installation. Do not infer Nexus project identity from names alone.
- Keep local handoff notes out of pushes unless requested. Update or remove stale handoff state in the same commit as the work it describes.

## Verification

- Always run `git diff --check` and inspect the intended diff before finishing.
- For application code, behavior, dependency, or build changes, run `./Test-Redux.ps1` from the active worktree. Obtain any required build approval before execution. Report blockers rather than claiming a pass.
- For documentation-only changes, check the instructions for conflicts, verify cited sources and paths, and inspect the diff. A documentation edit alone does not require compiling the application.
- Use `./Build-Redux.ps1 -Configuration Publish` for an authorized publish build. A successful build does not authorize deployment.
- Verify changed WPF visuals in the running application or the existing render harness, using wide and compact captures. Browser screenshots do not establish native WPF behavior.
- Add regression coverage for changed behavior where meaningful. Do not add tests that merely repeat low-impact text or implementation details. Once the relevant gate passes, repeat or broaden it only for new changes, failures, or unresolved concerns.

## GPT-6 Astra and OpenAI Docs

Guidance checked against official sources on 2026-09-05:

- [Using GPT-6 Astra: prompting best practices](https://developers.openai.com/api/docs/guides/latest-model/gpt-6-astra.md#prompting-best-practices) is the canonical model guidance. The execution rules above apply its recommendations on follow-through, instruction conflicts, communication, delegation, and proportionate verification.
- Codex's skill is [`openai-docs`](https://github.com/openai/codex/blob/main/codex-rs/skills/src/assets/samples/openai-docs/SKILL.md), with an [Astra migration reference](https://github.com/openai/codex/blob/main/codex-rs/skills/src/assets/samples/openai-docs/references/upgrading-to-gpt-6-astra.md). It is not a separate skill named `gpt-6-astra`.
- For OpenAI-specific model, API, or prompting work, use `openai-docs` when the current harness exposes it. Otherwise fetch the official live documentation directly and disclose any unavailable reference. Do not assume Codex's bundled skills exist in OpenCode or that an installed release matches upstream `main`.
- Prefer live official documentation over bundled snapshots for model IDs, parameters, capabilities, and availability. Preserve the explicitly requested model; do not substitute another model or infer account access.
- This contract update concerns coding-agent instructions. It does not authorize adding an OpenAI runtime integration, changing harness configuration, or replacing model strings. For a future requested API migration, inventory usage and preserve behavior, schemas, tool semantics, reasoning, latency/cost roles, and caching before making the smallest compatible change.
