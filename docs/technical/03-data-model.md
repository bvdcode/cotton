# 03. Data Model and Persistence

PostgreSQL stores Cotton's users, mutable filesystem graph, immutable content metadata, settings, authentication state, notifications, and operational history. EF Core is the only application access path; schema changes are delivered through generated migrations.

## Domain groups

### Identity and authentication

- Users own layouts, sessions, credentials, and external identities, and are subject to the instance quota policy.
- Refresh tokens are persisted so rotation and revocation survive process restarts.
- Passkey credentials and OIDC identities are separate from local password material.
- OIDC login state is short-lived protocol state, not a durable identity.

### Logical filesystem

- A layout is a user-owned namespace.
- Nodes form the folder tree and distinguish visible content from trash.
- File entries live under nodes, own display metadata, and reference immutable manifests.
- Normalized name keys enforce collision rules independently of display casing and accents.

### Content storage

- A file manifest identifies immutable file content and ordered chunk membership.
- A chunk is identified by its plaintext content hash.
- Manifest-chunk rows preserve chunk order and logical sizes.
- Ownership and reference data prevent shared chunks from being reclaimed while live.

### Product and operational state

- Server settings contain instance-wide runtime choices and encrypted external-service secrets.
- Share and download tokens grant bounded public access.
- Notifications, application versions, and benchmark samples record operational events rather than content.

## Relationship rules

All relationships use restrictive deletion. Cascading deletes are intentionally avoided because content may be shared and lifecycle operations must make ownership and cleanup decisions explicitly.

Consequences:

- deleting a visible entry does not automatically delete its manifest or chunks;
- deleting a manifest requires proving no live entry or version still references it;
- deleting a user requires coordinated removal of dependent state;
- garbage collection remains responsible for backend reclamation.

## Content and layout invariants

- A visible file entry belongs to one user and one parent node.
- Its manifest may be shared by other entries when content is identical.
- Manifest chunk order is deterministic and cannot contain ambiguous positions.
- Chunk identity is derived from plaintext bytes, while stored bytes may be compressed and encrypted.
- Trash remains inside the user's namespace but is excluded from normal browsing and search.
- Names are validated and normalized before uniqueness checks.

## Integrity metadata

Protected entities carry shadow columns for an integrity schema version and MAC. These columns are managed by the persistence boundary rather than exposed as normal domain properties.

On save, added protected rows receive a signature. Modified rows must first verify against their original persisted signature, preventing a tampered or unsigned row from being legitimized by a normal update.

Missing signatures, unsupported integrity versions, and invalid MACs are hard failures. They are not lazily backfilled.

## Encrypted settings

External-service secrets stored in the database use authenticated encryption under the configured master key. These encrypted fields are confidentiality controls, not database-integrity signatures.

Settings themselves are not row-signed. This keeps the instance configuration recoverable and avoids coupling ordinary configuration edits to the integrity-signing lifecycle.

## Concurrency

EF optimistic concurrency and unique indexes protect database-level races. Higher-level operations also use scoped coordination where a multi-row invariant cannot be represented by one constraint, including layout mutations, quota reservations, chunk ingestion, and garbage collection.

Code must translate expected persistence conflicts into domain or HTTP outcomes rather than leaking provider-specific exceptions.

## Migrations

- Generate migrations with EF tooling; do not hand-edit migration code or the model snapshot.
- Treat an applied migration as immutable history.
- Keep destructive changes explicit and review their rollback and backup requirements.
- Do not use startup code as a hidden substitute for schema migration.
- Major compatibility cutovers must document the required intermediate version and observation period.

## Database boundaries

- Application queries must remain user-scoped where data is tenant-owned.
- Read paths should avoid tracking when entities will not be modified.
- Large result sets require paging or streaming.
- Raw SQL is not part of the application data-access contract.
- The database is authoritative for references and policy; the storage backend is authoritative for object existence and stored byte length.

## Related sections

- [Content-addressed storage](04-content-addressed-storage.md)
- [Logical filesystem](05-logical-filesystem.md)
- [Database integrity](20-database-integrity.md)
- [Database backup and restore](21-database-backup-restore.md)
