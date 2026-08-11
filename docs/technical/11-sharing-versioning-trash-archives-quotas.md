# 11. Sharing, Versions, Trash, Archives, and Quotas

These features build on the same logical filesystem and manifest graph. They do not create alternative ownership or storage models.

## Public sharing

Cotton supports public links for individual files and folder subtrees. Possession of the token grants only the operation represented by that token.

Public tokens have adaptive entropy:

- small active-token populations use short lowercase alphanumeric links for convenience;
- larger populations increase token length and alphabet size;
- the lookup-failure limiter protects compact token lengths, while expanded tokens rely on their substantially larger search space;
- generation checks both file and folder token namespaces before committing a token.

Short-token protection is applied before database resolution and records failures only when a lookup fails. A successful user workflow is not charged as abuse. All routes that resolve the compact token, including direct download and HLS paths, use the same protection policy.

Expiration is checked at lookup time. Single-use tokens are consumed only after a successful response completes, so an interrupted transfer does not destroy the link. Metadata probes that do not represent a real download do not consume it.

Folder sharing remains scoped to the owner's active filesystem subtree. Moving, deleting, or changing the subtree cannot expand an existing token's authority.

## Version history

Historical versions reuse normal file and manifest entities. A lineage identifier connects the current file and retained historical rows.

The version lifecycle follows these rules:

- changing file content captures the previous state before the current manifest changes;
- restoring a version captures the state being replaced, making restore reversible;
- version-retention policy is applied after a successful mutation;
- the original lineage entry cannot be deleted as an arbitrary historical row;
- retained versions count toward logical quota.

Version downloads use expiring download tokens and the same authorization, integrity, and streaming path as other file downloads.

## Trash and restore

Normal deletion is a soft delete. The item is moved into the trash domain and records enough metadata to attempt restoration to its original parent.

Restoration validates that:

- the caller still owns the item and destination;
- the destination is an active writable folder;
- restoring the item will not create a cycle;
- the restored name does not silently collide with an existing sibling.

If the original parent no longer exists, the caller must choose or accept an explicit valid destination. Permanent deletion removes the logical references and retained lineage; physical chunks remain subject to delayed garbage collection.

## Archive downloads

Multi-item archive download is a two-stage operation:

1. An authenticated request resolves and validates the selected subtree, computes deterministic archive entries and length, and creates a short-lived ticket.
2. The ticket URL streams the stored ZIP representation without rebuilding authorization decisions mid-response.

Archive tickets are high-entropy, expire automatically, and are held in process memory. They do not survive a server restart. The writer streams file chunks and emits directory entries without buffering the complete archive.

Entry paths are normalized and uniquified so repeated names cannot produce ambiguous ZIP entries. The response declares its exact content length and disables transformation by intermediaries.

## Logical quota

Quota measures logical file references owned by a user, including retained versions. It does not bill raw uploaded chunks before they become reachable and does not reduce usage merely because two users share physical storage through deduplication.

The configured default quota applies uniformly to users; an unset or non-positive value means unlimited. The usage snapshot reports used, quota, and available bytes.

Mutations that add or replace file references follow a common critical section:

1. acquire the per-user mutation gate;
2. read or refresh logical usage;
3. calculate the additional bytes, accounting for a same-content replacement;
4. reject if the mutation would exceed the quota;
5. commit the storage-reference mutation;
6. update the process cache;
7. release the gate.

This closes races between parallel requests and between different layouts handled by one server process. A deployment with multiple application processes still requires a distributed or database-level reservation mechanism for a globally hard limit; the current process gate does not claim to provide that.

WebDAV exposes the same quota and returns its protocol-specific insufficient-storage response. Browser uploads, content replacement, version restore, and WebDAV PUT/COPY use the same quota rules.

## Failure and security boundaries

- A token lookup never bypasses owner, node-state, or integrity validation.
- Archive planning fails before issuing a ticket if selected content cannot be resolved safely.
- Restoring or copying cannot bypass quota by targeting a different folder.
- Quota cache entries are an optimization; a cold aggregate from the database is authoritative.
- Expired token rows may remain until retention cleanup, but they grant no access after expiration.
- Removing a logical reference never directly deletes shared physical content.

## Related sections

See [Logical Filesystem](05-logical-filesystem.md), [Upload and File Lifecycle](09-upload-file-lifecycle.md), [Garbage Collection](10-garbage-collection.md), [HTTP API and Mediator Layer](12-http-api-mediator.md), and [WebDAV](17-webdav.md).
