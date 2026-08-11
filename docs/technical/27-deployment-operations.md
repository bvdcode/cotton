# 27. Deployment and Operations

Cotton runs as one ASP.NET Core application serving the API, SignalR hub, and built web client. It requires PostgreSQL and either persistent local storage or an S3-compatible backend.

## Runtime requirements

- a supported container runtime or .NET runtime;
- PostgreSQL reachable from the application;
- a persistent `/app/files` volume for local storage;
- `pg_dump` and `pg_restore` compatible with the database server;
- `ffmpeg` and `ffprobe` for media features;
- optional format-specific preview tools for the formats an operator chooses to support.

The official container listens on port `8080` and includes the PostgreSQL and media tools used by built-in jobs.

## Bootstrap environment

| Variable | Default | Purpose |
| --- | --- | --- |
| `COTTON_PG_HOST` | `localhost` | PostgreSQL host. |
| `COTTON_PG_PORT` | `5432` | PostgreSQL port. |
| `COTTON_PG_DATABASE` | `cotton_dev` | PostgreSQL database. |
| `COTTON_PG_USERNAME` | `postgres` | PostgreSQL user. |
| `COTTON_PG_PASSWORD` | `postgres` | PostgreSQL password. Use a deployment secret in production. |
| `COTTON_MASTER_KEY` | unset | Optional non-interactive 32-character root key. |
| `COTTON_RESTORE_DATABASE_IF_EMPTY` | `false` | Restore the latest storage backup into an empty database. |
| `COTTON_PUBLIC_INSTANCE` | `false` | Enable explicit public/demo behavior. |
| `COTTON_PROCESS_HARDENING` | image default `true` | Request Linux non-dumpable process state. |
| `COTTON_STORAGE_PATH` | `/app/files` | Entrypoint path used for permission preparation. |
| `COTTON_PERMISSION_FIX` | `auto` | Entrypoint ownership policy: `auto`, `always`, or `never`. |
| `COTTON_RUN_AS` | `app` | Runtime user selected by the entrypoint. |

The database password and root key are consumed during bootstrap and cleared from the application's process environment. Container configuration metadata may still retain environment values, so production deployments should use the platform's secret facilities and restrict inspection access.

## Persistent state

Back up these as one recovery set:

- the PostgreSQL database;
- the selected storage backend;
- the exact root master key;
- deployment configuration needed to reach both systems.

Database metadata without storage cannot reconstruct file bytes. Storage without the database loses the current logical filesystem, although the latest embedded database backup may permit recovery. Encrypted storage without the master key is unrecoverable.

For bind-mounted local storage, pre-own the volume for the runtime UID when possible and use `COTTON_PERMISSION_FIX=never` to avoid a recursive ownership scan. `auto` changes ownership only when the target user cannot write the prepared directory.

## Master-key options

With `COTTON_MASTER_KEY`, startup is non-interactive. The key must remain stable for the lifetime of the deployment.

Without it, Cotton starts the limited `/unlock` surface. A genuinely new non-development instance requires the short-lived bootstrap token printed in server logs before accepting its first key. Existing instances validate the submitted key against their database and storage evidence.

Never replace the key merely to make startup pass. Diagnose database, backend, sentinel, and credential availability separately from a confirmed key mismatch.

## Storage setup

The administrator selects local or S3 storage through runtime settings.

For local storage:

- persist `/app/files`;
- provide sufficient free space and inode capacity;
- keep temporary storage writable for backups and format-specific preview tools.

For S3-compatible storage:

- configure endpoint, region, bucket, access key, and secret;
- test access before saving the configuration;
- grant only required object operations;
- preserve stable endpoint and bucket identity across restarts.

Stored credentials are encrypted with the active master key. Normal startup resolves the backend through the application provider. Interactive unlock uses the standard database model and shared backend factory before the full application is available; it does not define a shadow entity model or a separate storage implementation.

## Reverse proxy

Configure the public base URL and select one trusted-proxy mode:

- direct connection;
- exact immediate proxy address;
- CIDR network containing the possible immediate proxy peers.

Docker bridge addresses may change. When the observed peer is in `172.16.0.0/12`, trusting that explicit private bridge range is more stable than storing one ephemeral `172.x` address. Do not use a broader network than the deployment actually needs.

The immediate proxy must overwrite or strip client-supplied `CF-Connecting-IP`, `X-Real-IP`, and `X-Forwarded-For` headers. Cotton rejects forwarded-header authority from connections outside the configured boundary.

## Startup and readiness

Normal readiness follows key validation, preflight checks, migrations, optional restore, and settings initialization. A container restart loop should be diagnosed from the first startup exception rather than from reverse-proxy symptoms.

Common failure classes are:

- wrong or malformed master key;
- database connection or migration failure;
- encrypted settings written under incompatible legacy behavior;
- inaccessible local volume or S3 credentials;
- unresolved backup pointer during restore;
- missing required runtime license or external executable.

## Upgrades

Read release notes before changing major or storage-format versions. The version 0.5 cutover requires that the same database, storage, and master key have run successfully on Cotton 0.4.35 for the documented transition period before upgrading.

Version 0.5 does not decrypt CTN1 or backfill unsigned protected rows. Encountering either produces a targeted compatibility error rather than silent repair.

Upgrade one application instance at a time when migrations may be pending. Verify startup, settings decryption, file download, preview, authentication, backup visibility, and a representative upload before considering the upgrade complete.

## Backup and recovery

Keep the scheduled storage-native database backup enabled and test `COTTON_RESTORE_DATABASE_IF_EMPTY` against an isolated empty database. Also maintain provider-level PostgreSQL and object-storage backups according to the required recovery point.

Before destructive recovery, preserve the failing database and storage state for diagnosis. Do not manually edit EF migration history unless a verified recovery plan explicitly requires it.

## Hardening checklist

- run as a non-root user;
- keep the root filesystem read-only where compatible and mount only required writable paths;
- drop Linux capabilities and enable no-new-privileges;
- disable .NET diagnostics and core dumps;
- do not mount the Docker socket;
- avoid host PID namespace sharing;
- configure an exact trusted proxy boundary;
- require TOTP for administrators;
- protect PostgreSQL and storage with network and least-privilege controls;
- review the administrator security diagnostics after deployment changes.

## Related sections

See [Configuration and Startup](25-configuration-startup.md), [Master Key and Unlock Bootstrap](08-master-key-bootstrap.md), [Database Backup and Restore](21-database-backup-restore.md), and [Security Hardening](22-security-hardening.md).
