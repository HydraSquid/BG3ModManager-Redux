# Nexus update confidence implementation plan

Goal: useful, understandable manual checks even when installed download IDs are missing,
with hash-backed recovery and explicit, resettable user reference releases.

Approved design: preserve package metadata as package metadata; never compare version
strings numerically. Fetch each linked project's file list at most once per 24 hours.
Known files follow explicit replacement chains. Unknown downloads remain neutral and
offer available files without selecting one automatically. A user may acknowledge a
specific release; compare future replacement history from that reference, clearly
separate from installed identity. Bind acknowledgements to UUID, project and actual
installed SHA-256; changed bytes invalidate the acknowledgement. Never install,
export, change source metadata, or contact Nexus merely by opening the dialog.

## Implementation units

- Core `NexusInstalledIdentityResolver.cs` and focused tests: local async hashing,
  same-project bundled fingerprints, verified retained archive/direct-PAK matching,
  bounded decompression, cancellation and ambiguity rejection. Read-only delegate
  owns these disjoint files; parent owns integration.
- Core models/service: distinguish package/download/reference versions and unknown
  identity from errors. Expose cached file choices. Fetch project-only identities
  under existing cache/rate limits. Keep user acknowledgements in a separate atomic,
  bounded store with exclusive locking and no credentials/local paths in its data.
  Reject acknowledgement requests against stale/missing files or changed packages.
- GUI: coverage summary, explicit read-only action, neutral unknown labels, local
  identity progress, file chooser, acknowledge/reset buttons. Freeze mutable GUI
  snapshots before background work; cancel on close and preserve settings semantics.
- Regression coverage: divergent metadata/release versions, project-only listings,
  explicit chains/branches/cycles, equal version text across newer file IDs,
  acknowledge/restart/newer successor/reset, changed bytes/source invalidation,
  stale cache rejection, disk/lock failure and byte-identical archive evidence.
- Verification: use background-only test filtering through `Test-Redux.ps1`; default
  full gate unchanged. Do not run desktop-opening WPF tests or captures without user
  approval. One whole-feature read-only reviewer after parent gate, max two fixes.
- Document exact guarantees and limitations; update ignored HANDOFF state. No commit,
  Publish, deployment, live authenticated requests or desktop interaction authorized
  by this implementation task.
