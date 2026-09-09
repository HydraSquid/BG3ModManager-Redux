# Security policy

## Supported versions

Only the newest published Redux alpha receives security fixes. Older private test builds and source
snapshots are unsupported once a newer build is available.

## Report a vulnerability

Do not open a public issue for a vulnerability that could expose credentials, overwrite files
outside an intended destination, execute untrusted content, bypass archive validation, or disclose
private user data.

Use GitHub's [private vulnerability-reporting form](https://github.com/circleainn/BG3ModManager-Redux/security/advisories/new).
If GitHub does not make that form available to you, contact the maintainer through a private
channel listed on the official Redux project pages and include only the information needed to
reproduce the problem.

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
[Privacy and local data](PRIVACY_AND_DATA.md) for the intended data boundary.

Application updates use a fixed official GitHub channel document. Redux rejects unexpected
channels, versions, properties, hosts, release paths, archive sizes, hashes, archive paths, and
release contents before launching its updater. The updater runs outside the installation, waits
for Redux to exit, and mutates only paths declared by the new or previously installed release
inventory. Unlisted local files are outside its ownership boundary.

Standalone Setup uses that same fixed official public-alpha channel but remains a separate,
fresh-install-only bootstrapper. It bounds and strictly parses the channel document, verifies the
portable archive's declared length and SHA-256 digest, rejects unsafe or unowned paths, and commits
through isolated staging. If the x64 .NET 8 Desktop Runtime is missing, Setup accepts only the
official Microsoft runtime download and verifies its Authenticode signature before execution.
