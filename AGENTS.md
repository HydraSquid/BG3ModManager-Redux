# Project Agent Contract

## Scope and execution

- Follow system/developer constraints and the user's explicit instructions. Skills do not expand authorization.
- Read local `HANDOFF.md` and inspect Git state before work. Use the user's selected checkout; do not switch to historical worktrees.
- This is a Windows WPF application with native C++/CLI dependencies. Use PowerShell and Visual Studio MSBuild through the repository scripts, not ordinary `dotnet run`.
- Treat scoped action requests as authorization to implement and verify. Infer reversible details; ask only when a decision materially affects correctness or scope.
- Commit, push, publish, deploy, account changes, and non-trivial builds require explicit authorization.
- Preserve unrelated work, including untracked `External/lslib` build artifacts. Do not stage all files indiscriminately.
- Size process using the host's task-class rules. Delegate only when the task class permits it and files or responsibilities are disjoint.

## Repository and runtime safeguards

- `origin` is circleainn/BG3ModManager-Redux; `fork` is HydraSquid/BG3ModManager-Redux. Verify the intended remote before any authorized push.
- Preserve portable `Data`, `Orders`, `_Logs`, queues, archives, native ownership/backups, installed mods, and game load order.
- Native state for this fork is `Data/NativeInstalls`; never silently merge independently created ownership trees.
- Before an authorized portable deployment, ask the user to save working edits and close the application normally. Do not force-kill it.
- Keep API keys, NXM authorization, and signed URLs out of logs, queue manifests, commits, and reports.
- Dependency assistance uses reviewed UUID-based links or Copy UUID, without guessed project identity or automatic selection/download/activation.
- `HANDOFF.md` is ignored local state. Rewrite stale state when its work changes; never publish private handoffs or runtime backups.
- Preserve upstream branding and public documentation. Track retained fork differences in `docs/FORK_INTEGRATION.md`.

## Verification

- Always run `git diff --check` and inspect intended changes before finishing.
- For application code or behavior changes, run `./Test-Redux.ps1` from this checkout after obtaining required build approval.
- An authorized publish build uses `./Build-Redux.ps1 -Configuration Publish`; it does not authorize deployment.
- Verify WPF visuals with the render harness or actual application, using wide and compact captures. `REDUX_TABLE_SCREENSHOTS` points to an existing private directory.
- Add meaningful behavioral regressions. Report verification gaps rather than claiming success without evidence.
- Keep saved load orders distinct from game-export state. Never export or overwrite a saved order as a recovery guess.
