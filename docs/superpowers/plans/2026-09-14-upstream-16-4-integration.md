# Upstream 16.4.2 integration plan

**Goal:** Current upstream functionality plus retained fork improvements, verified as one usable application.

**Architecture:** Merge upstream main into the existing fork after checkpointing the verified Nexus confidence feature. Preserve upstream service/model evolution and port fork UI actions onto it. Resolve semantic overlaps explicitly rather than treating automatic merges as proof of compatibility.

**Stack:** Windows WPF, .NET 8, Visual Studio MSBuild, native C++/CLI dependencies.

**Approval:** User approved checkpoint/merge commits, Debug and Publish builds, controlled UI verification, and push to the fork. Portable deployment needs a separate normal-close checkpoint and authorization.

## Constraints

- Preserve all mods, game exports, saved orders, source links, queue/archive records, credentials, and native ownership. Verification uses fixtures only.
- Keep fork instance/channel identity, NativeInstalls ownership root, and disabled upstream binary self-updates.
- Main checkout is authoritative; preserve unrelated External/lslib artifacts. Never include local evidence or handoffs in commits.
- One whole-integration read-only reviewer after parent gates, maximum two fix/re-review rounds.

## Execution

1. Inspect Git state and checkpoint the uncommitted Nexus identity/reference implementation after the existing full gate. Record the checkpoint in the private handoff.
2. Merge current origin/main without committing. Resolve DivinityApp identity/version, GUI packaging, MainWindowViewModel behaviors, and test registrations by combining their intended changes.
3. Audit retained differences from docs/FORK_INTEGRATION.md against upstream:
   - Upstream collections, save details/review/export, onboarding, inactive persistence, per-order separators, menu and updater fixes remain available.
   - Embedded Downloads and standalone fallback expose upstream collection access and appropriate tab management; selected batches, dependency review/order/failure cascade, and transactional installs remain.
   - Source persistence/unlinks, no-key provider linking, row striping/custom preview, responsive pane resizing, Nexus update references and local proof remain.
   - Preserve native guards/ownership compatibility, queue shutdown safety and non-BG3 NXM forwarding.
4. Run Test-Redux.ps1, address compile/runtime regressions, and capture wide/compact WPF fixtures for changed Downloads and Nexus checker flows. Extend focused regressions where the integration changes behavior. Inspect images directly.
5. Parent runs gates; reviewer reads whole integration versus both parents, focusing on lost behavior, migrations/ownership, and UI access to upstream additions. Fix findings, rerun relevant gates, and document exact verification limits.
6. Build-Redux.ps1 -Configuration Publish; run produced regression executable where required by script behavior. Update FORK_INTEGRATION.md to current feature/state boundaries and private HANDOFF.md. Inspect staged diff, commit merge and push main only to verified HydraSquid fork remote.

## Completion evidence

- Upstream tip is an ancestor; full regression gate passes; Publish build succeeds.
- Required upstream and fork workflows have an explicit code/test/UI check, including collection access from the fork's Downloads UI.
- Protected portable runtime untouched. No deployment claimed.
