# 25. Configuration and Startup

Cotton combines environment-based bootstrap configuration with database-backed runtime settings. Environment variables provide only the information needed before the application can safely read its database and encrypted storage.

## Configuration sources

Bootstrap configuration includes:

- PostgreSQL connection values;
- the optional root master key;
- optional empty-database restore enablement;
- process-hardening and runtime flags;
- explicit external-tool paths where supported.

Runtime settings include storage backend selection, encrypted backend credentials, public base URL, trusted proxy network, upload and encryption tuning, email, OIDC, quotas, preview behavior, privacy, and other administrator-managed policy.

Settings are exposed through an immutable process snapshot. Updates commit through the settings provider and then replace or invalidate that snapshot. Consumers do not retain tracked settings entities or construct extra database contexts merely to read configuration.

## Startup sequence

The server starts in this order:

1. configure UTC process behavior and request Linux process hardening;
2. resolve the root master key from `COTTON_MASTER_KEY` or the interactive unlock server;
3. derive runtime encryption settings and clear the root key from the process environment;
4. build the normal application dependency graph;
5. run startup preflight checks;
6. apply EF Core migrations;
7. optionally restore an empty database from the latest storage backup;
8. validate the master key through the configured database and storage path;
9. run the narrow legacy settings repair, then initialize and warm server settings;
10. map normal controllers and the SignalR hub;
11. begin accepting traffic and start hosted work.

A blocking failure before the normal host is ready exposes only the limited startup-status behavior. The server does not accept normal authenticated traffic while key validation, migration, or restore is incomplete.

## Master-key validation

Environment and interactive keys use the same validation rules. Normal startup resolves them through the application graph. The pre-application unlock flow constructs the standard `CottonDbContext` model and shared storage-backend factory for the submitted candidate; it does not maintain a second entity model or a dedicated probe context.

Validation proceeds from strongest available evidence:

- decrypt and validate an existing sentinel;
- otherwise validate existing encrypted configuration or storage objects that already belong to the instance;
- if both database and storage are genuinely empty, create the sentinel with the submitted key.

Existing unreadable evidence is never overwritten with a new sentinel. A wrong key, unavailable backend, and uninitialized empty instance remain distinguishable outcomes.

## Settings initialization

A new instance receives one settings row with product defaults. Existing instances load their row through the normal provider. Sensitive S3, SMTP, OIDC, and cloud credentials are encrypted before persistence.

The narrow legacy zero-key recovery for encrypted server settings is isolated and marked obsolete. During startup settings materialization it tries the obsolete key only after normal authenticated decryption fails; if recovery succeeds, populated encrypted settings are rewritten with the active key before the fallback is disabled. It is not a general fallback for arbitrary encrypted data or normal runtime reads.

Server settings themselves are not database-integrity signed.

## Migrations and restore

Migrations are generated and applied through EF Core. Production startup must not edit migration history or snapshots manually.

Automatic database restore is opt-in and only runs for an empty application database. Restore completes before settings warmup and traffic. Operators should avoid starting multiple application instances concurrently against an unmigrated schema.

## Failure behavior

- Invalid bootstrap values fail before the main host starts.
- A master-key mismatch fails closed without changing encrypted evidence.
- An unavailable database or storage backend is reported as infrastructure failure, not as a wrong key.
- Migration and restore failures prevent readiness.
- Settings updates validate complete related configuration before replacing the active snapshot.
- Startup delays in background jobs are cancellation-aware and occur after the application lifecycle has begun.

## Related sections

See [Master Key and Unlock Bootstrap](08-master-key-bootstrap.md), [Database Backup and Restore](21-database-backup-restore.md), [Security Hardening](22-security-hardening.md), and [Deployment and Operations](27-deployment-operations.md).
