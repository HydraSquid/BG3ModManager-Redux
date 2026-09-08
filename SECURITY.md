# Security policy

## Supported versions

Only the newest published Redux alpha receives security fixes. Older private test builds and source
snapshots are unsupported once a newer build is available.

## Report a vulnerability

Do not open a public issue for a vulnerability that could expose credentials, overwrite files
outside an intended destination, execute untrusted content, bypass archive validation, or disclose
private user data.

Use GitHub's private vulnerability-reporting feature when it is available for this repository. If
private reporting is unavailable, contact the maintainer through a private channel listed on the
official Redux project pages and include only the information needed to reproduce the problem.

Please include:

- affected Redux version and Windows version;
- the smallest safe reproduction;
- expected and actual security boundary;
- whether credentials, arbitrary paths, executable content, or user files are involved; and
- suggested mitigation, if known.

Do not send live credentials, signed download URLs, copyrighted mod archives, private saves, or
personal data. Use synthetic fixtures wherever possible.

## Security boundaries

Redux treats archives, manifests, mod metadata, provider responses, paths, and downloaded files as
untrusted input. Reviewed package recognition is not permission to execute native code. A package
must still pass its destination-specific inspection and the user must authorize installation.

The project prioritizes path containment, staged writes, atomic replacement, verified downloads,
credential redaction, conservative native-file ownership, and recoverable deletion. See
[Privacy and local data](docs/PRIVACY_AND_DATA.md) for the intended data boundary.
