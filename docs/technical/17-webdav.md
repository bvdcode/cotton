# 17. WebDAV

The WebDAV surface exposes the same user-owned logical filesystem through WebDAV semantics. It does not bypass Cotton ownership, quota, versioning, encryption, or content-addressed storage rules.

## Endpoint and authentication

WebDAV is mounted at `/api/v1/webdav` with the remaining path identifying a resource below the user's default root.

Clients authenticate with HTTP Basic using the Cotton username and a dedicated WebDAV application token. The account password is not the WebDAV credential. The server accepts one well-formed Basic credential payload only; embedded line breaks or multiple credential records are rejected.

Authentication failures are counted atomically per trusted client address. Concurrent invalid requests cannot lose increments and evade the failure policy. Successful authentication clears or ages failure state according to the limiter contract.

## Path resolution

Every request path is decoded and normalized into logical segments. Resolution:

- starts at the authenticated user's active root;
- rejects traversal and malformed separators;
- applies the same name normalization and sibling uniqueness rules as the browser API;
- never crosses into another user's layout;
- distinguishes a missing final segment from a missing or non-folder parent.

Protocol-specific path parsing ends before domain mutations begin. Handlers operate on resolved node and file identities rather than reinterpreting raw URLs.

## Supported operations

Cotton supports the common filesystem operations expected by desktop WebDAV clients:

- `OPTIONS` advertises capabilities;
- `PROPFIND` returns resource metadata, children, and quota properties according to depth;
- `GET` and `HEAD` serve file content and metadata;
- `PUT` creates or replaces file content;
- `MKCOL` creates a folder;
- `MOVE` renames or moves an item;
- `COPY` duplicates files or subtrees;
- `DELETE` applies Cotton deletion semantics;
- `LOCK` and `UNLOCK` provide compatibility locks;
- `PROPPATCH` handles supported property behavior.

Unsupported methods or properties return explicit WebDAV responses rather than being silently accepted.

## Upload and copy behavior

`PUT` streams the request through the normal chunking, hashing, encryption, manifest, versioning, and quota pipeline. A known content length may be used for an early quota rejection, but the final manifest-aware check still occurs inside the per-user mutation gate.

Replacing an existing file captures version history according to the same policy as browser uploads. `COPY` bills every new logical file reference and performs its final quota check and commit under the same user gate. Target conflicts follow the `Overwrite` header and never become implicit data loss.

Quota exhaustion maps to WebDAV insufficient storage (`507`).

## Metadata and ranges

`PROPFIND` exposes stable WebDAV properties such as resource type, content length, modification time, entity tag, and quota availability. Entity tags represent the current logical content state and support conditional updates where applicable.

`GET` supports normal streaming and range behavior without materializing the entire file. `HEAD` returns equivalent headers without a body.

## Locks

WebDAV locks are short-lived, process-local compatibility state. A matching lock token is required for conflicting mutations while a lock is active. Expired locks are removed opportunistically.

Locks are not a durable distributed transaction mechanism. A process restart clears them, and multi-instance deployments require affinity or a shared lock implementation if strong cross-instance WebDAV locking is needed.

## Failure and security boundaries

- Authentication uses the normalized trusted client address.
- Resource-not-found responses do not reveal cross-user existence.
- Request cancellation stops body reads, storage writes, and downstream database work.
- A failed upload cannot publish a partial logical file.
- WebDAV and browser mutations share layout, quota, integrity, and storage invariants.
- Normal client concurrency is coordinated; abuse responses are reserved for authentication or enumeration pressure.

## Related sections

See [Logical Filesystem](05-logical-filesystem.md), [Upload and File Lifecycle](09-upload-file-lifecycle.md), [Sharing, Versions, Trash, Archives, and Quotas](11-sharing-versioning-trash-archives-quotas.md), and [Authentication and Sessions](13-authentication-sessions.md).
