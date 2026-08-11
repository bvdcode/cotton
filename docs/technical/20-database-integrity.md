# 20. Database Integrity

Cotton signs security-sensitive database rows with a keyed MAC so a database-only edit cannot silently change authorization, identity, session, sharing, or file-graph state. This is tamper evidence, not encryption: protected row values remain visible to a database administrator, but a valid signature cannot be forged without the master key.

## Signature model

Protected tables have nullable shadow columns for an integrity version and MAC. Runtime policy is strict: a protected row is trusted only when both values are present, the version is supported, and the MAC verifies.

Each protected entity type has a typed descriptor that defines:

- its stable entity name and key;
- the descriptor schema version;
- the exact security-sensitive fields included in the signature;
- deterministic canonical serialization of those fields.

This makes coverage a domain decision rather than a reflection-based dump of every property. Operational fields such as retry state may be deliberately excluded when changing them must not invalidate the security identity of the row.

Server settings are not row-signed. Their stored credentials are protected separately by encryption at rest.

## Canonical payload

The canonical format is versioned and independent of JSON or database-provider formatting. It includes the payload marker, MAC algorithm, writer version, entity name, descriptor version, entity key, typed field names, and typed values.

Strings and byte arrays are length-prefixed, integers are big-endian, nullable values carry an explicit presence marker, GUIDs use a stable representation, timestamps are normalized to database precision, and unordered maps are sorted before writing.

Changing the canonical contract or a descriptor invalidates existing signatures and therefore requires an explicit release transition. It must never be treated as an incidental refactor.

## Key derivation

The integrity key is a 32-byte HKDF-SHA-256 subkey derived from the master encryption key with the domain label `CottonDbIntegrityKey:v1`. It is never persisted.

The provider creates a fresh HMAC-SHA-256 instance for each operation, clears temporary key material, and refuses use after disposal. Domain separation prevents the database MAC key from being interchangeable with storage, backup, or other derived keys.

## Write boundary

Before EF Core saves a protected entity:

- new rows receive the current signature and version;
- modified rows must first have a valid original signature;
- deleted rows must also be valid before deletion is accepted;
- only after the original state passes may a modified row receive a replacement signature.

Verifying the original state prevents a legitimate application update from laundering a row that was changed directly in the database.

## Read boundary

Signatures are verified where the application is about to trust protected data: authentication, session refresh, passkey or OIDC use, token resolution, file content access, share access, and other security-sensitive operations.

File reads verify the relevant file graph rather than only the final manifest hash. Collection listings avoid verifying an entire subtree eagerly; a specific item is verified when its protected content or authority is consumed.

## Strict compatibility boundary

Version 0.5 does not create trust by signing unsigned legacy rows at startup. Missing integrity metadata raises a dedicated compatibility error instructing the operator to complete the transition on Cotton 0.4.35 with the same database, storage, and master key.

An unsupported CTN1 object similarly raises the dedicated storage-format transition error. These narrow compatibility exceptions are marked obsolete so the transition guidance can be removed after the supported upgrade window. There is no CTN1 decryptor or unsigned-row backfill in version 0.5.

A present signature that fails verification is corruption or tampering, not a compatibility case.

## Diagnostics and reporting

The administrator security snapshot counts protected rows with missing or unsupported integrity metadata through typed descriptors. It does not recompute every MAC merely to render the dashboard; full verification remains at trust boundaries.

An integrity failure blocks the protected operation and queues a bounded, deduplicated administrator notification. Queue saturation may reduce duplicate notifications but never changes enforcement because the requesting operation has already failed.

Operators should restore invalid rows from a trusted backup or re-apply the intended mutation through Cotton. They should not manually generate or copy MAC values.

## Security boundaries

- Row integrity does not encrypt database values.
- It does not protect against an attacker who has both the database and the master key.
- It does not replace PostgreSQL access control, backups, or storage authentication.
- Missing and invalid signatures fail closed.
- Migration tooling may construct a context without runtime signing; production writes use the server composition that installs the signer.

## Related sections

See [Cryptography Engine](07-cryptography-engine.md), [Master Key and Unlock Bootstrap](08-master-key-bootstrap.md), [Database Backup and Restore](21-database-backup-restore.md), and [Security Hardening](22-security-hardening.md).
