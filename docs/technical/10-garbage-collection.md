# 10. Garbage Collection and Storage Consistency

Garbage collection removes data that is no longer reachable without treating temporary inconsistency as permission to delete. The database defines liveness, deletion is delayed, and every candidate is checked again immediately before destructive work.

## Liveness model

A chunk is live while it is referenced by any supported durable relationship, including:

- file-manifest membership;
- preview or avatar storage references;
- the active database-backup graph;
- the master-key sentinel;
- other explicitly protected storage objects.

`ChunkOwnership` is an ingest and proof-of-possession record, not a durable liveness reference. A chunk with ownership rows but no live content reference may be collected.

The protected-storage-key calculation is shared by garbage collection and storage-consistency checks so the two processes cannot apply different definitions to backups or the sentinel.

## Collection lifecycle

Garbage collection runs in bounded batches and separates discovery from deletion:

1. Remove manifests that have no logical file references, using a transaction and a final reference check.
2. Clear pending deletion schedules for chunks that became live again or are protected.
3. Schedule currently unreferenced chunks after a retention delay.
4. Reserve due candidates, pause briefly so active ingestion can observe the reservation, and delete them in bounded groups.
5. Re-check liveness and schedule state immediately before deleting each group.

A chunk that becomes referenced between discovery and deletion is removed from the candidate set and its schedule is cleared. Database constraints and the final liveness query remain the safety boundary; in-memory reservations coordinate concurrent work but do not replace those checks.

Retention and batch size vary with the configured storage-space mode. The exact values are operational policy rather than part of the storage format and may change without a migration.

## Coordination with ingestion

Ingestion waits when the content-addressed object it needs is currently reserved for deletion. The wait is bounded and cancellation-aware. If the conflict does not clear, the upload fails as retryable work rather than racing a destructive operation.

This coordination applies only to the conflicting storage key. Unrelated uploads and downloads continue normally.

## Storage consistency

The consistency pass reconciles the database graph with objects visible in the configured backend:

- a missing file-data chunk is reported and its manifest is preserved for diagnosis;
- a missing derived preview or avatar may have its stale reference cleared;
- an untracked backend object may be registered as an orphan so normal delayed GC can handle it;
- protected backup and sentinel objects are never converted into ordinary orphan candidates.

The consistency pass does not delete user file metadata merely because a backend read failed. Transient backend failures must remain distinguishable from confirmed missing objects.

## Scheduling and shutdown

GC and consistency jobs are single-flight. Their startup staggering exists to avoid a burst of heavy maintenance work immediately after process start. All startup delays and batch loops observe cancellation so shutdown does not wait for the full delay interval.

Manual execution bypasses only scheduling cadence; it does not bypass liveness or protection checks.

## Observability

Administrators can inspect pending and overdue garbage-collection work through the server administration API. Time buckets use the request timezone when valid and otherwise the configured server timezone. Reported pending data is computed from the same liveness predicates used by collection.

Important signals are:

- scheduled and overdue object counts and bytes;
- backend delete failures;
- chunks referenced by file data but missing from storage;
- newly discovered backend orphans;
- work skipped because a conflicting upload is active.

## Failure behavior

- Candidate discovery and final deletion are intentionally separate.
- A failed storage deletion is logged and later consistency work can reconcile the remaining object.
- A failed manifest cleanup transaction leaves the whole candidate batch intact.
- Notification failure does not authorize destructive cleanup of missing file data.
- Cancellation stops between bounded units of work and releases in-memory reservations.

## Related sections

See [Content-Addressed Storage](04-content-addressed-storage.md), [Upload and File Lifecycle](09-upload-file-lifecycle.md), [Background Jobs](15-background-jobs.md), and [Database Backup and Restore](21-database-backup-restore.md).
