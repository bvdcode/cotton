![status-prealpha](https://img.shields.io/badge/status-pre--alpha-red)
[![CI](https://github.com/bvdcode/cotton/actions/workflows/docker-image.yml/badge.svg)](https://github.com/bvdcode/cotton/actions/workflows/docker-image.yml)
[![Release](https://img.shields.io/github/v/release/bvdcode/cotton?sort=semver)](https://github.com/bvdcode/cotton/releases)
[![Docker Pulls](https://img.shields.io/docker/pulls/bvdcode/cotton)](https://hub.docker.com/r/bvdcode/cotton)
[![Image Size](https://img.shields.io/docker/image-size/bvdcode/cotton/latest)](https://hub.docker.com/r/bvdcode/cotton/tags)
[![License](https://img.shields.io/github/license/bvdcode/cotton)](LICENSE)

> ⚠️ The Project Status: Pre-Alpha | Live: [cotton.splidex.com](https://cotton.splidex.com/)

<div align="center">

# Cotton Cloud — self‑hosted storage engine with real crypto

</div>


**All managed .NET/C#** — no native deps, no P/Invoke.

---

## What Is Cotton

Cotton Cloud is a self‑hosted file cloud with its own content‑addressed storage engine and streaming AES‑GCM crypto. It is not a “web UI over a filesystem” — objects (chunks, manifests) live independently from user trees (layouts), with clean APIs and a working UI.

---

## Highlights

- Content‑addressed chunks and manifests with deduplication by design.
- Chunk‑first, idempotent upload protocol resilient to network hiccups.
- Storage pipeline with pluggable processors (crypto, compression, filesystem).
- Streaming AES‑GCM (pure C#) measured at memory‑bound throughput on encrypt; decrypt on par with OpenSSL in our tests.
- Postgres metadata with clear split of “what” (content) vs “where” (layout).
- Sensible defaults: modern password hashing, strict name validation, autoconfig.

---

## Architecture Overview

Separation of concerns similar to git:

- What (content): `Chunk`, `FileManifest`, `FileManifestChunk`, `ChunkOwnership` in `Sources/Cotton.Database/Models`.
- Where (projection): `Layout`, `Node`, `NodeFile` for user trees and mounts.

This enables snapshots of layouts, content‑level dedup, and mounting the same file in multiple places without copies.

---

## Chunk‑First Upload Model

Client uploads independent chunks (any size ≤ server limit) identified by SHA‑256.

1. POST chunk: server stores or replies “already have it”.
2. POST manifest from existing chunk hashes to assemble a file at a target node.

Effects:

- Network drops do not corrupt state — partial uploads act as cache until GC.
- Retries only send missing chunks.
- Different clients can choose small or large chunks; server assembles the same file.

API surface (selected):

- `POST /chunks` — upload a chunk with hex SHA‑256; server verifies and stores via pipeline.
- `POST /files/from-chunks` — create a file from an ordered list of chunk hashes at a layout node.

See controllers in `Sources/Cotton.Server/Controllers`.

---

## Storage Pipeline

`Sources/Cotton.Storage` implements a processor pipeline:

- `FileSystemStorageProcessor` — persists chunk blobs on disk (`.ctn`).
- `CryptoProcessor` — wraps streams with streaming AES‑GCM encrypt/decrypt.
- `CompressionProcessor` — optional Brotli (easily pluggable).

Write flows forward through processors; read flows backward. This gives a real storage engine, not just `File.WriteAllBytes`.

---

## Cryptography & Performance

`Sources/Cotton.Crypto` contains a streaming AES‑GCM engine (`AesGcmStreamCipher`) with:

- Per‑file wrapped keys and per‑chunk authentication.
- 12‑byte nonce layout (4‑byte file prefix + 8‑byte chunk counter).
- Parallel, chunked pipelines built on `System.IO.Pipelines`.

Measurements (see `Sources/Cotton.Crypto.Tests` and `Cotton.Crypto.Tests.Charts`):

- Decrypt throughput ~9–10 GB/s across large chunk sizes on typical dev hardware.
- Encrypt scales to memory bandwidth (~14–16+ GB/s) around 1–2 threads with ~1 MiB chunks.
- Scaling efficient up to 2–4 threads, then limited by shared resources (memory BW/caches).

Hashing for addressing uses SHA‑256 (see `Sources/Cotton.Crypto/Hasher.cs`).

---

## Security & Validation

- Passwords: PBKDF2 service with modern PHC‑style handling via `EasyExtensions` (see DI in `Cotton.Server`).
- Keys & settings: `Cotton.Autoconfig` derives `Pepper` and `MasterEncryptionKey` from a single `COTTON_MASTER_KEY` env, exposes `CottonSettings` (`MasterEncryptionKeyId`, `EncryptionThreads`, `CipherChunkSizeBytes`, `MaxChunkSizeBytes`).
- Name hygiene: `Cotton.Validators/NameValidator` enforces Unicode normalization, forbids `.`/`..`, control and zero‑width chars, trims trailing dots/spaces, and blocks Windows reserved names; also provides case‑folded `NameKey` for CI lookups.

---

## Quick Start (dev)

Requires Docker

1. Start Postgres:

```bash
docker run -d --name cotton-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:latest
```

2. Set env and run server:

```bash
export COTTON_PG_HOST="localhost"
export COTTON_PG_PORT="5432"
export COTTON_PG_DATABASE="cotton_dev"
export COTTON_PG_USERNAME="postgres"
export COTTON_PG_PASSWORD="postgres"

# Exactly 32 characters; used to derive pepper and master encryption key
export COTTON_MASTER_KEY="devedovolovopeperepolevopopovedo"

docker run -d --name cotton -p 8080:8080 \
  -v /data/cotton:/app/files
  -e COTTON_PG_HOST="localhost" \
  -e COTTON_PG_PORT="5432" \
  -e COTTON_PG_DATABASE="cotton_dev" \
  -e COTTON_PG_USERNAME="postgres" \
  -e COTTON_PG_PASSWORD="postgres" \
  -e COTTON_MASTER_KEY="devedovolovopeperepolevopopovedo" \
    bvdcode/cotton:latest
```

The app applies EF migrations automatically and serves the UI at http://localhost:5000 (or the shown port). Upload settings are returned by the server; the frontend (`Sources/cotton.client`) uses them for chunking.

---

## Roadmap (short)

- Generational GC with compaction/merging of small “dust” chunks (design complete; implementation pending).
- Additional processors (cache, S3 replica, cold storage).
- Hardening auth flows and extending UI around uploads/layouts.

---

## License & Branding

- License: AGPL‑3.0‑only (see `LICENSE`).
- The “Cotton” name and marks are reserved; forks should use distinct names/marks.

---

## Repo Map

- `Sources/Cotton.Server` — ASP.NET Core API + UI hosting.
- `Sources/Cotton.Database` — EF Core models and migrations.
- `Sources/Cotton.Storage` — storage pipeline and processors.
- `Sources/Cotton.Crypto` — streaming AES‑GCM, key derivation, hashing.
- `Sources/Cotton.Topology` — layout manipulation services.
- `Sources/cotton.client` — TypeScript/Vite frontend.

---

Built as a cohesive storage system: clean model, careful crypto, pragmatic performance — with a UI that actually uses it.
