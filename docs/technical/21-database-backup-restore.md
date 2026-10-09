# 21. Database Backup and Restore

Cotton stores PostgreSQL custom-format dumps inside the same encrypted content-addressed backend as user data. A master-key-scoped pointer contains the retained backup history, newest first. Each entry identifies an immutable manifest and its ordered dump chunks.

## Backup creation

The scheduled backup flow is:

1. Run `pg_dump` into a restricted temporary file.
2. Split the dump using the configured chunk size.
3. Hash and ingest each block through the normal compression, encryption, deduplication, and storage pipeline.
4. Compute the complete dump hash and size.
5. Write an immutable JSON manifest containing the format, source metadata, ordered chunks, lengths, total size, and whole-dump hash.
6. Atomically replace the fixed pointer with the retained history, including the new manifest reference.
7. Remove the plaintext temporary file in `finally`.

The manifest is content-addressed. The pointer is intentionally mutable and has a storage key scoped to the master encryption key. Filesystem storage publishes a completed temporary file using an atomic replacement; S3 storage replaces the object with a single PUT. The old pointer is never deleted before publishing its replacement.

Every backup from the last seven days is retained, along with at least the three newest backups regardless of age. Frequent manual requests cannot evict backups still within the seven-day window. Retention is evaluated when a successful backup is published; expired entries then become eligible for normal garbage collection. Existing pointers containing a single backup are read as a one-entry history and upgraded on the next successful backup.

Backup chunks require an owning user for the normal ingest model. A fresh instance with no users therefore has nothing meaningful to back up and skips or fails the operation explicitly.

The dump includes the `file_embeddings` table and its indexes, but excludes its rows. Search embeddings are regenerated from the stored files after restoration.

## Scheduling and administration

The backup job runs every seven days. Its first process execution is staggered with other maintenance jobs. Scheduled and HTTP-triggered backups use the same operation and an asynchronous process-wide gate. Garbage collection takes the same gate so it cannot delete dump chunks while a backup is being assembled or published.

The administrator's database backup page displays the current pointer history and can create a backup on demand. Both manual endpoints return HTTP 200 with backup metadata only after the dump, manifest, and pointer have been saved:

- `PATCH /api/v1/server/database-backup/trigger`: administrator session.
- `POST /api/v1/server/database-backup`: external backup token in `X-Cotton-Backup-Token`.

`GET /api/v1/server/database-backup` returns the retained history to administrators. `GET /api/v1/server/database-backup/latest` returns the latest manifest metadata.

### External backup token

An administrator generates a token on the database backup page or through `POST /api/v1/server/database-backup/token`. The response is marked `Cache-Control: no-store`. Save the token in the external backup tool; Cotton does not persist it in the database.

Tokens are authenticated and encrypted with AES-GCM under a purpose-specific key derived from the master encryption key. They permit only the external backup creation endpoint. Tokens have no expiry or independent revocation: all issued tokens remain valid while the master key is unchanged. Issuing another token does not invalidate earlier tokens.

For example, with the token supplied by the backup tool as `COTTON_BACKUP_TOKEN`:

```sh
curl --fail-with-body --request POST \
  --header "X-Cotton-Backup-Token: ${COTTON_BACKUP_TOKEN}" \
  https://cotton.example/api/v1/server/database-backup
```

Wait for a successful response before copying storage. Configure the caller and reverse proxy to allow enough time for the dump and upload. A disconnected HTTP request can cancel the operation before publication; a lost response does not prove that publication failed.

The Quartz schedule itself is process-local; persistence of backup data comes from the storage backend, not from a durable scheduler record.

## Startup restore

Automatic restore is opt-in through `COTTON_RESTORE_DATABASE_IF_EMPTY=true`. It runs before the server accepts normal traffic and only when the migrated database contains no user or server-settings data.

Restore follows this sequence:

1. Derive the latest-pointer storage key from the configured master key.
2. Resolve the pointer and validate the newest candidate manifest.
3. Stream manifest chunks in order through the storage pipeline into a temporary dump.
4. Verify the rebuilt dump's total byte count and SHA-256 hash.
5. If a manifest or dump chunk is missing, damaged, or fails verification, log the skipped backup and try the preceding entry. No database restore is attempted until a complete dump has passed verification.
6. Ensure required PostgreSQL extensions, then run `pg_restore` in a single transaction with ownership and privilege restoration disabled.
7. Apply pending migrations, refresh provider type information, and notify administrators. The notification identifies the restored backup and any skipped newer backups.
8. Remove the temporary dump in `finally`.

If all retained candidates are invalid, startup stops. An unreadable pointer, cancellation, local I/O failure, or PostgreSQL restore failure also stops the operation. Database execution failures do not trigger another restore attempt against a modified database.

On its first eligible run after startup, the text indexing job checks whether `file_embeddings` is empty. If so, it clears the manifests' text indexing versions and errors in batches of 500 before resuming indexing. The check runs once per process so documents without extractable text are not retried every minute. Regeneration requires global indexing to be enabled, the vector extension and index to be ready, and the computation service to be available.

## Garbage-collection protection

The following storage objects are protected live references:

- the master-key-scoped latest pointer;
- every manifest retained in that pointer;
- every dump chunk named by those manifests.

If any retained manifest cannot be resolved, garbage collection aborts because it cannot establish the complete set of protected chunks. Objects unique to entries removed by retention become ordinary GC candidates.

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

Retention helps recover from a storage copy that contains a newer pointer but lacks that dump's objects. It does not make a live copy of all user-file storage a consistent snapshot: a valid database dump may still reference user-file chunks absent from the copy. Use a coordinated storage snapshot when that guarantee is required.

## Related sections

See [Storage Pipeline and Backends](06-storage-pipeline.md), [Master Key and Unlock Bootstrap](08-master-key-bootstrap.md), [Garbage Collection](10-garbage-collection.md), and [Deployment and Operations](27-deployment-operations.md).
