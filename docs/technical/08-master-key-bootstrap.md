# 08. Master Key and Unlock Bootstrap

Cotton derives process-local secrets from one 32-character root master key. The key is supplied through `COTTON_MASTER_KEY` or the browser unlock flow. There is no built-in development key.

## Derived secrets

HKDF-SHA256 derives independent values for password pepper and storage encryption. Database-integrity signing derives its own domain-separated key from the master encryption key.

Changing the root key changes every derived secret. Cotton does not currently implement transparent key rotation; losing or replacing the key makes existing encrypted data unreadable.

## Environment-key startup

When `COTTON_MASTER_KEY` is present, Cotton validates its length, derives runtime settings, and removes the original value from the process environment as soon as practical.

Before normal traffic or background jobs start, the runtime:

1. builds the normal application dependency graph and runs preflight checks;
2. applies database migrations;
3. optionally restores an empty database from encrypted storage;
4. resolves the configured storage backend;
5. validates the candidate key against existing evidence and validates or creates the sentinel;
6. performs the temporary settings compatibility repair, warms settings, and starts the application.

An existing encrypted object or signed database row that does not validate rejects startup. Infrastructure failures remain distinguishable from a wrong key.

## Browser unlock

Without an environment key, Cotton starts a restricted temporary application. Static assets and the unlock UI remain available; other API requests return `423 Locked`.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/unlock/status` | Reports whether first-unlock authorization is required. |
| `GET` | `/api/v1/unlock/key` | Generates a candidate root key. |
| `POST` | `/api/v1/unlock` | Validates a submitted key and completes unlock. |

A brand-new instance requires a short-lived bootstrap token printed to the server log. The token is random, expires, remains process-local, and is compared in constant time.

## Candidate validation

Browser unlock uses the normal EF entity model and the normal storage-backend factory with candidate-specific cryptographic services. It does not use a second database model or a special probe context.

- Local storage validates the sentinel and, when no sentinel exists, bounded encrypted-storage or signed-database evidence.
- S3 configuration must first decrypt successfully under the candidate key. The normal S3 backend is then used to validate or create the sentinel.
- A genuinely empty database and empty storage backend may establish a new sentinel after first-unlock authorization.

The full application starts only after validation succeeds and repeats validation through its normal dependency graph.

## Sentinel contract

The sentinel uses a deterministic logical storage key so it can be located before the master key is trusted. Its encrypted payload records a schema version, purpose, creation timestamp, and random nonce.

- Correct key and valid sentinel: accepted.
- Authentication failure: candidate rejected.
- Malformed or truncated sentinel: corruption reported.
- Missing sentinel with independent valid key evidence: sentinel created.
- Empty new instance with valid bootstrap authorization: sentinel created.
- Storage transport failure: propagated as infrastructure failure.
- Existing sentinel: never silently overwritten or repaired.

The sentinel proves that a candidate key can decrypt Cotton data. It does not contain or recover the key.

## Error classification

| Failure | HTTP outcome during browser unlock |
| --- | --- |
| Invalid key shape or failed cryptographic evidence | `400 Bad Request` |
| Invalid or expired bootstrap token | `403 Forbidden` |
| Storage or S3 unavailable | `503 Service Unavailable` |

Unlock responses disable caching. Shutdown cancels the temporary unlock wait cleanly.

## Related sections

- [Cryptography engine](07-cryptography-engine.md)
- [Database integrity](20-database-integrity.md)
- [Configuration and startup](25-configuration-startup.md)
- [Deployment and operations](27-deployment-operations.md)
