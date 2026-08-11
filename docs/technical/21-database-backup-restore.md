# 21. Database Backup and Restore

Cotton stores a PostgreSQL custom-format dump inside the same encrypted content-addressed backend as user data. A small master-key-scoped pointer identifies the current manifest, and the manifest identifies the ordered dump chunks.

## Backup creation

The scheduled backup flow is:

1. Run `pg_dump` into a restricted temporary file.
2. Split the dump using the configured chunk size.
3. Hash and ingest each block through the normal compression, encryption, deduplication, and storage pipeline.
4. Compute the complete dump hash and size.
5. Write an immutable JSON manifest containing the format, source metadata, ordered chunks, lengths, total size, and whole-dump hash.
6. Replace the fixed latest-backup pointer with the new manifest reference.
7. Remove the plaintext temporary file in `finally`.

The manifest is content-addressed. The latest pointer is intentionally mutable and has a storage key scoped to the master encryption key, so a deployment can find its own backup without publishing a predictable global object name.

Backup chunks require an owning user for the normal ingest model. A fresh instance with no users therefore has nothing meaningful to back up and skips or fails the operation explicitly.

## Scheduling and administration

The backup job is single-flight and runs on its configured recurring cadence. Its first process execution is staggered with other maintenance jobs. An administrator may trigger the registered job on demand and inspect metadata for the latest resolvable backup.

The Quartz schedule itself is process-local; persistence of backup data comes from the storage backend, not from a durable scheduler record.

## Startup restore

Automatic restore is opt-in through `COTTON_RESTORE_DATABASE_IF_EMPTY=true`. It runs before the server accepts normal traffic and only when the migrated database contains no user or server-settings data.

Restore follows this sequence:

1. Derive the latest-pointer storage key from the configured master key.
2. Resolve and validate the pointer and manifest.
3. Stream manifest chunks in order through the storage pipeline into a temporary dump.
4. Verify the rebuilt dump's total byte count and SHA-256 hash.
5. Run `pg_restore` with ownership and privilege restoration disabled.
6. ensure required PostgreSQL extensions and refresh provider type information;
7. notify administrators after the restored database is available;
8. remove the temporary dump in `finally`.

A hash mismatch, missing required artifact, decryption failure, or non-zero restore exit stops startup. Cotton does not continue with a partially restored database.

## Garbage-collection protection

The following storage objects are protected live references:

- the master-key-scoped latest pointer;
- the manifest named by that pointer;
- every dump chunk named by that manifest.

If the pointer exists but its manifest cannot be resolved, garbage collection aborts rather than guessing which chunks are safe to remove. Only the latest manifest graph is protected as a first-class backup; objects unique to older backups may later become ordinary GC candidates.

## Security properties

Backup chunks, manifest, and pointer pass through the normal encrypted storage pipeline. Recovering them requires the correct backend and master key.

The PostgreSQL password is supplied to the dump tools through process environment rather than command-line arguments. Plaintext dumps exist only in the configured temporary area during backup or restore and are cleaned up on success, failure, or cancellation.

Backup integrity uses both authenticated storage encryption and explicit whole-dump hash and length verification before `pg_restore`.

## Operational requirements

- `pg_dump` and `pg_restore` compatible with the deployed PostgreSQL version must be available on `PATH`.
- The storage backend and master key must be backed up together; losing the key makes stored backups unrecoverable.
- Automatic restore must point at the intended empty database and existing storage backend.
- Operators should test restoration, not merely observe successful backup creation.
- A backup does not replace independent PostgreSQL and storage-provider disaster-recovery policy.

## Related sections

See [Storage Pipeline and Backends](06-storage-pipeline.md), [Master Key and Unlock Bootstrap](08-master-key-bootstrap.md), [Garbage Collection](10-garbage-collection.md), and [Deployment and Operations](27-deployment-operations.md).
