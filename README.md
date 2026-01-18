![Status](https://img.shields.io/badge/status-beta-yellow)
[![License](https://badgen.net/github/license/bvdcode/cotton)](LICENSE)
[![CI](https://github.com/bvdcode/cotton/actions/workflows/docker-image.yml/badge.svg)](https://github.com/bvdcode/cotton/actions)
[![CodeFactor](https://www.codefactor.io/repository/github/bvdcode/cotton/badge)](https://www.codefactor.io/repository/github/bvdcode/cotton)
[![Release](https://badgen.net/github/release/bvdcode/cotton?label=version)](https://github.com/bvdcode/cotton/releases)
[![Docker Pulls](https://badgen.net/docker/pulls/bvdcode/cotton?icon=docker&label=pulls)](https://hub.docker.com/r/bvdcode/cotton)
[![Docker Image Size](https://badgen.net/docker/size/bvdcode/cotton?icon=docker&label=size)](https://hub.docker.com/r/bvdcode/cotton)
[![Github last-commit](https://img.shields.io/github/last-commit/bvdcode/cotton)](https://github.com/bvdcode/cotton/commits/main/)

> ⚠️ **Project status: Beta.**  
> Breaking changes and rough edges are expected.  
> Live demo: [cotton.splidex.com](https://cotton.splidex.com/)

<div align="center">

# Cotton Cloud — self-hosted storage engine with real crypto

</div>

**All managed .NET/C#** — no native deps, no P/Invoke, no "shell out to OpenSSL".

---

## What Is Cotton?

Cotton Cloud is a self-hosted file cloud built around its **own content-addressed storage engine** and **streaming AES-GCM crypto**.

It is **not**:

- a thin web UI over a flat filesystem;
- a Nextcloud-style "kitchen sink" with 50 random features.

Instead, Cotton separates:

- **what you store** (chunks, manifests, ownership)  
- **how you see it** (layouts, folders, mounts)

…with clean APIs and a UI that actually uses them.

Designed to:

- handle **folders with hundreds of thousands / millions of entries** without choking the UI;
- stream **multi-GB uploads in a browser** without locking up;
- keep crypto and storage performance-oriented, not "just good enough".

---

## Why Cotton (vs yet another self-hosted cloud)?

Most self-hosted "clouds" are either:

- filesystem wrappers (no real storage model, weak crypto), or  
- huge PHP/JS monoliths that fall apart under load / big trees.

Cotton is opinionated:

- **Content-addressed core**  
  Files are manifests over chunks addressed by SHA-256. Same chunk, same hash, same storage blob — dedup is built in.

- **Real crypto, not checkbox crypto**  
  Streaming AES-GCM in pure C#, per-file wrapped keys, per-chunk authentication, proper nonces.

- **Engine-first, UI-second**  
  The React UI is thin and fast: virtualized folder views, minimal controls, native OS sharing hooks. No "regenerate metadata" buttons.

- **Self-hosting friendly**  
  Single .NET service, Postgres, filesystem (or other backends via processors). No exotic deps.

If you want a **storage system** rather than "webDAV with a skin", Cotton is meant for you.

---

## Highlights

Engine / protocol:

- Content-addressed chunks and manifests with deduplication by design.
- Chunk-first, idempotent upload protocol resilient to network hiccups and retries.
- Storage pipeline with pluggable processors (crypto, compression, filesystem, cache/replica in future).
- Streaming AES-GCM (pure C#) measured at **memory-bound** throughput on encrypt; decrypt on par with OpenSSL in our tests.
- Postgres metadata with a clear split between **"what" (content)** and **"where" (layout)**.

UX / behavior:

- Browser uploads of **large folders (tens of GB, thousands of files)** without freezing the UI.
- Virtualized folder view tuned for very large directories.
- Simple, focused file viewer: only the controls you actually use (share, download, etc.).
- Time-limited share links backed by one-time / revocable tokens tied to file metadata, not raw paths.

Self-hosting / ops:

- All-managed .NET, easy to run under Docker.
- Automatic EF Core migrations on startup.
- Sensible defaults: modern password hashing, strict name validation, autoconfig from a single master key.

---

## Architecture Overview

Separation of concerns is similar to git:

- **Content ("what")**  
  `Chunk`, `FileManifest`, `FileManifestChunk`, `ChunkOwnership`  
  — see `Sources/Cotton.Database/Models`.

- **Layout ("where")**  
  `Layout`, `Node`, `NodeFile` manage user trees, mounts and projections.

This enables:

- snapshots of layouts without copying content;
- content-level deduplication;
- mounting the same file in multiple places with no duplication.

---

## Chunk-First Upload Model

Client uploads independent chunks (size ≤ server limit) identified by **SHA-256**.

1. **POST chunk** — server verifies hash, runs it through the pipeline, stores or replies "already have it".
2. **POST file manifest** — client sends an ordered list of chunk hashes; server assembles a file at a target node.

Effects:

- Network drops do not corrupt state — partially uploaded chunks are simply cache until GC.
- Retries only send **missing** chunks.
- Different clients can choose small or large chunks; server still assembles the same file.

Selected endpoints:

- `POST /chunks` — upload a chunk with hex SHA-256; server verifies and stores via pipeline.
- `POST /files/from-chunks` — create a file from an ordered list of chunk hashes at a layout node.

See controllers under `Sources/Cotton.Server/Controllers`.

---

## Storage Pipeline

`Sources/Cotton.Storage` implements a processor pipeline:

- `FileSystemStorageProcessor` — persists chunk blobs on disk (`.ctn`).
- `CryptoProcessor` — wraps streams with streaming AES-GCM encrypt/decrypt.
- `CompressionProcessor` — optional Brotli (easily pluggable).

Write flows **forward** through processors; read flows **backwards**.  
This gives a real storage engine, not just `File.WriteAllBytes`.

---

## Cryptography & Performance

`Sources/Cotton.Crypto` contains a streaming AES-GCM engine (`AesGcmStreamCipher`) with:

- Per-file wrapped keys and per-chunk authentication.
- 12-byte nonce layout (4-byte file prefix + 8-byte chunk counter).
- Parallel, chunked pipelines built on `System.IO.Pipelines`.

Measurements (see `Sources/Cotton.Crypto.Tests` and `Cotton.Crypto.Tests.Charts`):

- Decrypt throughput ~9–10 GB/s across large chunk sizes on typical dev hardware.
- Encrypt scales to memory bandwidth (~14–16+ GB/s) around 1–2 threads with ~1 MiB chunks.
- Scaling efficient up to 2–4 threads, then limited by shared resources (memory BW / caches).

Hashing for addressing uses SHA-256 (`Sources/Cotton.Crypto/Hasher.cs`).

---

## Security & Validation

- **Passwords**  
  PBKDF2 service with modern PHC-style handling via `EasyExtensions` (see DI in `Cotton.Server`).

- **Keys & settings**  
  `Cotton.Autoconfig` derives `Pepper` and `MasterEncryptionKey` from a single `COTTON_MASTER_KEY` env var, exposes `CottonSettings`:
  - `MasterEncryptionKeyId`
  - `EncryptionThreads`
  - `CipherChunkSizeBytes`
  - `MaxChunkSizeBytes`

- **Name hygiene**  
  `Cotton.Validators/NameValidator`:
  - enforces Unicode normalization;
  - forbids `.` / `..`, control and zero-width chars;
  - trims trailing dots/spaces;
  - blocks Windows reserved names;
  - provides a case-folded `NameKey` for CI lookups.

---

## Quick Start (dev)

Requires **Docker** and **Postgres**.

1. Start Postgres:

```bash
docker run -d --name cotton-pg \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  postgres:latest
```

2. Run Cotton:

```bash
# COTTON_MASTER_KEY must be exactly 32 characters.
# It is used to derive both the password pepper and the master encryption key.

docker run -d --name cotton \
  -p 8080:8080 \
  -v /data/cotton:/app/files \
  -e COTTON_PG_HOST="host.docker.internal" \
  -e COTTON_PG_PORT="5432" \
  -e COTTON_PG_DATABASE="cotton_dev" \
  -e COTTON_PG_USERNAME="postgres" \
  -e COTTON_PG_PASSWORD="postgres" \
  -e COTTON_MASTER_KEY="devedovolovopeperepolevopopovedo" \
  bvdcode/cotton:latest
```

On startup the app applies EF migrations automatically and serves the UI at `http://localhost:8080` (or whatever port you mapped).

Upload settings are returned by the server; the frontend (`Sources/cotton.client`) uses them for chunking.

---

## Roadmap (short)

- Generational GC with compaction/merging of small "dust" chunks (design complete; implementation pending).
- Additional processors (cache, S3 replica, cold storage, etc.).
- Hardening auth flows and extending UI around uploads/layouts and sharing.
- Native/mobile and desktop clients reusing the same engine.

---

## License & Branding

- License: MIT (see `LICENSE`).
- The "Cotton" name and marks are reserved; forks should use distinct names/marks.

---

## Repo Map

- `Sources/Cotton.Server` — ASP.NET Core API + UI hosting.
- `Sources/Cotton.Database` — EF Core models and migrations.
- `Sources/Cotton.Storage` — storage pipeline and processors.
- `Sources/Cotton.Crypto` — streaming AES-GCM, key derivation, hashing.
- `Sources/Cotton.Topology` — layout/topology manipulation services.
- `Sources/cotton.client` — TypeScript/Vite frontend.

---

Built as a cohesive storage system: clean model, careful crypto, pragmatic performance — with a UI that actually takes advantage of it.
