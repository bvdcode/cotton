# 08. Master Key, Autoconfig & Unlock Bootstrap

Cotton derives every process-local encryption secret from one 32-character root master key. The key comes either from `COTTON_MASTER_KEY` or from the browser `/unlock` flow. Cotton has no built-in development key and does not start the full application until a submitted key has been validated.

## Components

| Component | Responsibility |
|---|---|
| `ConfigurationBuilderExtensions` | Validates the root key and derives `Pepper` and `MasterEncryptionKey`. |
| `MasterKeyUnlockServer` | Serves the locked UI, enforces the first-unlock bootstrap token, and accepts a candidate key. |
| `MasterKeyUnlockValidator` | Reads the regular EF model with the candidate key and resolves Local or S3 storage. |
| `StorageBackendFactory` | Creates the selected Local or S3 backend for both unlock and normal runtime paths. |
| `MasterKeyValidator` | Applies the common sentinel, storage-evidence, and database-integrity checks. |
| `MasterKeyStartupValidator` | Resolves the runtime backend through DI and invokes `MasterKeyValidator` before traffic and jobs start. |
| `MasterKeySentinelStore` | Creates or validates the encrypted storage sentinel. |
| `MasterKeyRuntimeState` | Records whether the key came from the environment or browser unlock. |

There is one EF entity-model definition and one backend-selection implementation. Browser unlock uses a short-lived instance of the regular `CottonDbContext`; it does not define startup-only EF entities or a second database context type.

## Key derivation

`ConfigurationBuilderExtensions.DeriveEncryptionSettings` requires exactly 32 characters and derives independent subkeys with HKDF-SHA256:

```csharp
return new CottonEncryptionSettings
{
    Pepper = KeyDerivation.DeriveSubkeyBase64(rootMasterEncryptionKey, "CottonPepper", DefaultKeyLength),
    MasterEncryptionKey = KeyDerivation.DeriveSubkeyBase64(rootMasterEncryptionKey, "CottonMasterEncryptionKey", DefaultKeyLength),
    MasterEncryptionKeyId = DefaultMasterKeyId,
};
```

- `Pepper` is used by password hashing and database-integrity key derivation.
- `MasterEncryptionKey` encrypts storage streams and protected database fields.
- `MasterEncryptionKeyId` is currently `1`; it is an authenticated format tag, not an implemented rotation mechanism.

Changing the root key for an existing instance changes every derived secret and makes existing protected data unreadable.

## Environment-key startup

When `COTTON_MASTER_KEY` is present, `Program` derives `CottonEncryptionSettings` and clears the variable from the process and user environment in a `finally` block. The full application is then built.

Before traffic or Quartz jobs start, Cotton:

1. applies database migrations;
2. attempts the configured empty-database restore;
3. resolves the configured storage backend through `StorageBackendProvider`;
4. runs `MasterKeyStartupValidator`;
5. warms the settings cache;
6. starts the application.

An existing sentinel must decrypt successfully. Without a sentinel, Local storage must either be empty, contain a decryptable encrypted object, or have a valid signed user row as database evidence. Existing encrypted objects that do not decrypt reject startup.

## Browser unlock

Without `COTTON_MASTER_KEY`, `Program` starts a small temporary web application and waits for `MasterKeyUnlockServer.WaitForUnlockAsync`.

The locked server exposes:

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/v1/unlock/status` | Reports whether the first-unlock bootstrap token is required. |
| `GET` | `/api/v1/unlock/key` | Generates a new 32-character candidate key. |
| `POST` | `/api/v1/unlock` | Validates a submitted key and completes unlock. |

Other `/api/v1/*` requests return `423 Locked`. Static assets and the SPA remain available so `/unlock` can render.

### Bootstrap token

A production instance with no existing Cotton rows requires a one-time 32-character hexadecimal bootstrap token. The token:

- is generated with `RandomNumberGenerator`;
- exists only for the unlock-server lifetime;
- is printed to the server log;
- expires after five minutes;
- is compared in constant time.

`MasterKeyUnlockValidator.HasExistingCottonDataAsync` checks the ordinary `CottonDbContext` model with `AnyAsync`. These existence queries do not materialize encrypted fields and therefore do not require a master key.

### Candidate validation

The POST handler derives candidate encryption settings and calls `MasterKeyUnlockValidator`. The validator uses a candidate-specific `DatabaseFieldProtector` and a regular `CottonDbContext` instance:

```text
candidate key
  -> regular CottonDbContext
  -> latest StorageType
  -> StorageBackendFactory
       -> FileSystemStorageBackend
       -> S3StorageBackend
  -> MasterKeyValidator
  -> MasterKeySentinelStore
```

For Local storage, the validator checks the sentinel and, when necessary, bounded encrypted-storage or database-integrity evidence.

For S3, the regular encrypted EF conversion must first decrypt `S3SecretAccessKeyEncrypted` with the submitted key. Successful AES-GCM authentication proves the candidate key before the ordinary `S3Provider` and `S3StorageBackend` are created. Cotton then validates an existing S3 sentinel or creates one when upgrading an instance that predates S3 sentinel storage.

The temporary unlock application stops only after validation succeeds. The full application then starts with the same derived settings and repeats startup validation through its normal DI graph.

## Sentinel format and behavior

The sentinel logical key is `cotton.master-key.sentinel.v1`. Its storage key is a deterministic hash of that logical key, so Cotton can locate it before validating a master key.

The encrypted JSON payload contains:

```csharp
private record MasterKeySentinelPayload(
    int SchemaVersion,
    string Purpose,
    DateTimeOffset CreatedAtUtc,
    string Nonce);
```

The payload is encrypted with the same authenticated stream cipher used by Cotton storage. Validation is strict:

- valid sentinel and correct key: accepted;
- sentinel authentication failure: key rejected;
- malformed or truncated sentinel: reported as corruption;
- missing sentinel after independent key evidence: a new sentinel is written;
- storage transport failure: propagated as a storage error; it is not reported as a wrong key;
- an existing sentinel is never silently repaired or overwritten.

`MasterKeySentinelResult` contains `Success`, `Created`, and `Error`. Compatibility modes and repair results are not part of the current flow.

## Error classification

Browser unlock keeps cryptographic failures separate from infrastructure failures:

| Failure | Response |
|---|---|
| Invalid root-key length | `400 Bad Request` |
| Protected S3 settings do not authenticate | `400 Bad Request` |
| Sentinel or existing storage does not authenticate | `400 Bad Request` |
| Missing or malformed storage configuration | `400 Bad Request` |
| S3/network/storage unavailable | `503 Service Unavailable` |
| Invalid or expired first-unlock token | `403 Forbidden` |

This prevents a temporary S3 outage from being misreported as a bad master key.

## Security and lifecycle properties

- The browser value is trimmed before derivation.
- Unlock responses disable caching.
- Candidate keys use the regular EF entity model with their own field protector; there is no parallel probe model.
- Unlock contexts disable EF internal service-provider caching so failed candidate keys do not accumulate cached models.
- The main application is not started until unlock succeeds.
- `CompleteUnlockAsync` waits briefly before stopping the temporary host so the success response can reach the browser.
- `ApplicationStopping` cancels the unlock wait and allows a clean process exit.
- Losing the master key still makes encrypted data unrecoverable; the sentinel is validation evidence, not key recovery material.

## Related sections

- [Cryptography Engine](07-cryptography-engine.md)
- [Configuration & Startup](25-configuration-startup.md)
- [Deployment & Operations](27-deployment-operations.md)
- [Database Integrity](20-database-integrity.md)
