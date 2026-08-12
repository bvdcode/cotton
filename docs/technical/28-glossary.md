# 28. Glossary

## A–C

### AES-GCM

The authenticated-encryption algorithm used by the CTN2 storage format. It provides confidentiality and detects modification of encrypted bytes and authenticated headers.

### Canonical payload

A deterministic typed binary representation of a protected database row used as input to its integrity MAC.

### Chunk

The independently hashed, compressed, encrypted, and stored unit of file content. Its plaintext SHA-256 hash is its content identity.

### Chunk ownership

Proof that a user uploaded or already possessed a chunk. It authorizes manifest assembly but is not a durable storage-liveness reference.

### Client-side encryption

Optional browser encryption whose master key is not available to the server. It is distinct from server-managed encryption at rest.

### Content addressing

Naming immutable content by a digest of its plaintext bytes. Identical bytes share an address, and modified bytes produce a different address.

### CTN2

The current chunked authenticated-encryption container format written by Cotton storage encryption.

## D–H

### Database integrity signature

An HMAC-SHA-256 value over selected security-sensitive fields of a database row. It detects database-only modification but does not encrypt the row.

### Download token

A purpose-specific credential granting temporary or single-use access to one file or file version and its supported derived playback routes.

### File manifest

Immutable logical content metadata: ordered chunk references, size, proposed and computed hashes, content type, and derived media references.

### File version

A retained historical file state in the same ownership, manifest, quota, and integrity model as current files.

### Garbage collection

Delayed removal of storage objects that have no durable database or protected-system reference, with liveness rechecked before deletion.

### HLS

HTTP Live Streaming. Cotton can generate playlists and video segments on demand for eligible non-native browser video.

### HKDF

A standard key-derivation function used to create domain-separated keys from Cotton's root or master encryption material.

## I–N

### Integrity descriptor

A typed policy defining the stable identity and security-sensitive fields included in one protected entity's database signature.

### Layout

A user-owned logical filesystem tree with a root node and a defined semantic role.

### Live reference

A database or protected-system relationship that keeps a storage object out of garbage collection.

### Manifest deduplication

Reuse of existing immutable file content when ordered chunks and content identity match, while logical files remain separate references.

### Master key

The deployment's single 32-character root secret. Cotton derives storage, integrity, backup, and password-related key material from it.

### Mediator request

A typed command or query representing one application use case outside HTTP transport concerns.

### Name key

The normalized representation used for case- and diacritic-insensitive sibling uniqueness and search.

### Node

A folder-like topology entry in a layout. A node owns child nodes and logical files.

### Node file

A user-owned logical file reference connecting a name and node location to an immutable file manifest.

## O–R

### OIDC external identity

A durable binding between one configured OpenID Connect provider subject and one Cotton user.

### Orphan chunk

A chunk registered in the database or backend but not reachable through any live content or protected-system reference.

### Passkey

A WebAuthn public-key credential used for phishing-resistant registration and authentication ceremonies.

### Pepper

Master-key-derived secret material used to strengthen password-related protection independently from storage encryption keys.

### Preview

An immutable derived representation of a file or avatar, normally stored as content-addressed WebP data.

### Quota

A per-user limit on logical referenced file bytes, including retained versions. Raw unreferenced chunks are governed by storage pressure instead.

### Refresh session

A server-side session associated with a rotating refresh credential and the short-lived access tokens issued from it.

## S–Z

### Sentinel

A small encrypted storage object used as strong evidence that a submitted master key matches the deployment.

### Share token

A purpose-specific credential granting public access to a file or folder subtree under the token's expiry and use policy.

### Storage backend

The final filesystem or S3-compatible object store used by the storage pipeline.

### Storage pipeline

The ordered composition of compression, authenticated encryption, caching behavior, and backend I/O around content-addressed objects.

### Storage pressure

Backend-capacity protection that reserves in-flight space and rejects new writes with insufficient-storage semantics before the configured reserve is exhausted.

### Trash

The soft-delete domain that retains an item and restoration metadata until it is restored or permanently deleted.

### Trusted proxy

The exact immediate peer or CIDR network allowed to supply forwarded client-address and protocol headers.

### Unlock bootstrap

The limited startup flow used to submit and validate the master key when it is not provided through the environment.

### WebDAV

The HTTP filesystem protocol surface backed by Cotton's normal logical filesystem, quota, versioning, and storage rules.

### Zstandard

The compression algorithm applied before storage encryption where configured by the pipeline.

## Related sections

Use the [technical documentation index](README.md) for the full subsystem descriptions.
