# Cotton Keyring V2 MVP

Keyring V2 changes the master key from a data-key source into an unlock secret. Data keys are random keyring entries stored in an encrypted state snapshot. The access envelope wraps only the random `KeyringRootKey`.

## Threat Model Boundary

V2 protects new key ids from `COTTON_MASTER_KEY + chunks` offline decryption. This is true for V2 keys because chunk, DB-field, TOTP, and DB-integrity keys are random state entries.

Legacy key id `1` is compatibility debt. Existing V1 blobs and fields were encrypted with material derived from the old master key, so V2 cannot remove that offline decryption ability without re-encrypting those blobs. V2 only marks legacy keys decrypt-only or verify-only and stops using them for new writes.

V2 does not protect against root/admin on a live unlocked server. Data keys are in process memory after unlock. Stronger live-server protection would require HSM/KMS/TEE support.

## MVP Object Model

The journaled keyring store has two immutable object kinds:

- Access envelope: recipient slots wrapping the random `KeyringRootKey`.
- State snapshot: encrypted full keyring state with data keys, primary pointers, key statuses, origins, and generations.

Heads are authoritative. Latest pointers are cache only. Startup and diagnostics must be able to recover from a missing or corrupted latest pointer by scanning heads and immutable objects.

The current opt-in local replica stores objects under:

```text
<COTTON_KEYRING_PATH or COTTON_STORAGE_PATH or AppContext.BaseDirectory/files>/.cotton/system/keyring/v2/
```

Enable runtime V2 with:

```text
COTTON_KEYRING_V2=1
```

Old runtime mode remains the default when the flag is absent.

## Current Migration Behavior

On first V2 bootstrap, Cotton creates a state with:

- chunk key id `1`: legacy, decrypt-only;
- chunk key id `2`: random V2 primary;
- DB field key id `1`: legacy, decrypt-only;
- DB field key id `10`: random V2 primary;
- DB integrity key id `1`: legacy, verify-only;
- DB integrity key id `20`: random V2 primary;
- TOTP key id `1`: legacy, decrypt-only;
- TOTP key id `40`: random V2 primary.

New chunk writes use the chunk primary. New TOTP secrets use the TOTP primary. New encrypted preview/avatar DB hashes use the DB-field primary. DB integrity signs new rows with the random primary and verifies legacy MACs during the transition.

Decryption is fail-closed by key id. Unknown key ids must not fall back to the current key or master key.

## Unlock Rotation

Unlock rotation creates a new root epoch, generates a new `KeyringRootKey`, re-encrypts the small state snapshot, and writes a new access envelope for the new unlock secret. It does not touch chunks.

Old access envelopes may still exist in replicas or backups. Rotation updates the latest valid head path; it is not cryptographic erasure of old offline backups.

## Remaining Work

- Add DB and object-storage replicas for the canonical keyring objects, not only the local file replica.
- Expose diagnostics in the admin security checkup UI.
- Add recovery kit export/import and recovery slots.
- Add background or explicit chunk re-encryption for legacy id `1`.
- Add fuller crash-point tests around partial commits and replica skew.
- Add cleanup rules for old access envelopes after a successful rotation.
