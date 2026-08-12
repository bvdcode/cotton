# 15. Background Jobs and Scheduling

Cotton uses Quartz for recurring and on-demand work that should not extend an HTTP request. Jobs are responsible for orchestration and bounded iteration; domain rules remain in reusable services and mediator operations.

## Registration model

Scheduled jobs declare their cadence through the project's job-trigger attribute. Registration discovers those declarations and configures Quartz with non-overlapping execution for each job type.

An on-demand trigger requests the same registered job; it does not call a private implementation path with different safeguards. Triggering is best-effort when equivalent work is already running.

## Job categories

Current recurring work includes:

- garbage collection and storage consistency;
- complete manifest-hash verification;
- preview generation and metadata extraction;
- database backup;
- expired download-token, refresh-session, and sync-change retention;
- temporary-folder cleanup;
- performance snapshot collection.

Historical backfill jobs for stored chunk size, MIME normalization, and null metadata have been removed. Current write paths are responsible for producing complete metadata at creation time.

## Startup staggering

Maintenance jobs that would otherwise create a startup load spike use one shared startup-delay coordinator. Each configured delay is consumed once per process; later scheduled or manual executions are not delayed again.

The delay exists only for jobs whose startup execution is potentially heavy, such as backup, retention, consistency, collection, cleanup, and performance aggregation. It is not a generic delay added to every job.

All delays receive Quartz cancellation. Stopping the process therefore cancels pending startup waits instead of waiting several minutes for them to expire.

## Upload-aware derived work

Preview, metadata, and complete-hash jobs skip or defer a file while its upload is still active. They process independent items in bounded batches and isolate item-level failures so one unsupported or corrupt file does not stop the whole pass.

An `ffmpeg` or `ffprobe` failure is classified from the actual process result. Expected unsupported-media failures are recorded for that item; cancellation and infrastructure failures are not swallowed as ordinary media misses.

## Correctness rules

- A job must re-check mutable eligibility close to its write or delete boundary.
- Database writes use asynchronous APIs and pass the cancellation token.
- External-process and storage I/O is cancellation-aware.
- Repeating work is idempotent or protected by uniqueness/concurrency checks.
- Progress is persisted in bounded units so a process restart can resume safely.
- A failed notification does not mark underlying verification work complete when notification delivery is part of that work's contract.

## Failure and retry behavior

Quartz records job failures and applies the configured future schedule. Individual jobs decide whether a failed item should be retried in the next pass, marked as unsupported, or surfaced as a durable diagnostic.

Long-running jobs log progress and the identity of the failing unit without logging decrypted content or secrets. Catch blocks log with context and either rethrow or deliberately continue at the item boundary; silent broad exception handling is not acceptable.

## Performance

Batch sizes and deliberate throttling protect the database, storage backend, CPU, and external tools. Throttles are local coordination rather than user-facing rate limits. Increasing parallelism should be based on measured backend and CPU capacity, not merely on available request concurrency.

## Related sections

See [Garbage Collection](10-garbage-collection.md), [Previews and Media Processing](18-previews-media.md), [Database Backup and Restore](21-database-backup-restore.md), and [Performance and Testing](26-performance-benchmarking-testing.md).
