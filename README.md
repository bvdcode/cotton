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

**All managed .NET/C#** core — crypto and storage engine are pure managed code.

---

## What Is Cotton?

Cotton Cloud is a self-hosted file cloud built around its **own content-addressed storage engine** and **streaming AES-GCM crypto**.

It is **not**:

- a thin web UI over a flat filesystem;
- a \*\*\*\*cloud-style "kitchen sink" with 50 random features.

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

- **Engine‑first; polished, user‑centered UI**  
  The React UI is minimalist by design **because** it was obsessively refined — not because it was an afterthought. Every page and control was iterated on for usability (many hours of design and testing); most common tasks are reachable in **1–2 clicks**. Virtualized folder views, carefully considered controls and native OS sharing hooks make the UI both fast and genuinely productive.

- **Self-hosting friendly**  
  Single .NET service, Postgres, filesystem (or other backends via processors). No exotic deps.

- **Easy for non-technical users; powerful for experts**  
  First-run setup is a guided, in‑browser wizard that actually helps non-technical users — there are explicit “Not sure” buttons and safe defaults so an installer can finish setup with one click. If you prefer control, the same wizard exposes expert knobs (custom SMTP, S3, fine-grained encryption and threading options, or an optional NVIDIA/GPU runner for AI workloads) so power users and operators get full control up-front. In short: one UX for both audiences — friendly defaults for novices, explicit expert paths for administrators.

If you want a **storage system** (engine‑first) rather than a thin filesystem wrapper, Cotton is meant for you — it provides first-class APIs (including a production-quality WebDAV v1 endpoint) while keeping the UI thin and focused.

---

## Highlights

Engine / protocol:

- Content-addressed chunks and manifests with deduplication by design.
- Chunk-first, idempotent upload protocol resilient to network hiccups and retries.
- Storage pipeline with pluggable processors (crypto, filesystem backends, cache/replica planned).
- All stored content is fully _seekable_ at the storage level (see ConcatenatedReadStream) — enables efficient `Range` reads, preview extraction and streaming without reassembling files.
- Optional streaming Zstandard (zstd) compression — enabled by default, streaming via `ZstdSharp` (transparent to clients; effective because compression runs before encryption).
- Production-quality WebDAV v1 (RFC 4918) support — core methods implemented: `OPTIONS`, `PROPFIND`, `GET`, `HEAD`, `PUT`, `DELETE`, `MKCOL`, `MOVE`, `COPY`; includes `Range`/`ETag` semantics and `DAV: 1` header. Optional WebDAV extensions (LOCK/UNLOCK, PROPPATCH) are not implemented; see `src/Cotton.Server/Controllers/WebDavController.cs`.
- Streaming AES-GCM (pure C#) measured at **memory-bound** throughput on encrypt; decrypt on par with OpenSSL in our tests.
- Instant, one‑click snapshots and restores — snapshots record references (not copies) so restoring a layout is an atomic pointer switch (works instantly even for millions of files).
- Opt‑in performance telemetry + adaptive autotuning — with operator consent Cotton can collect anonymized measurements and recommend or apply safer, better defaults (chunk sizes, buffers, threading) based on real‑world signals.
- GC‑safe ingest — the system prevents a concurrent re‑upload of a chunk that is currently being deleted, avoiding rare race conditions between deletes and ingests.
- Postgres metadata with a clear split between **"what" (content)** and **"where" (layout)**.

UX / behavior:

- Browser uploads of **large folders (tens of GB, thousands of files)** without freezing the UI.
- Virtualized folder view tuned for very large directories.
- Simple, focused file viewer: only the controls you actually use (share, download, etc.).
- Time-limited share links backed by one-time / revocable tokens tied to file metadata, not raw paths.

Self-hosting / ops:

- All-managed .NET, easy to run under Docker.
- Optional telemetry (opt-in): enables Cotton Cloud-backed services (email/AI modes) and helps improve reliability and defaults over time.
- Plugin safety & moderation: runtime extensions are sandboxed and supervised (resource limits, timeouts, crash isolation); an operator can disable or revoke plugins and the app‑store is moderated so problematic plugins are removed. This keeps third‑party innovation from impacting platform reliability.
- Automatic EF Core migrations on startup.
- Sensible defaults: modern password hashing, strict name validation, autoconfig from a single master key.

---

## Architecture Overview

Separation of concerns is similar to git:

- **Content ("what")**  
  `Chunk`, `FileManifest`, `FileManifestChunk`, `ChunkOwnership`  
  — see `src/Cotton.Database/Models`.

- **Layout ("where")**  
  `Layout`, `Node`, `NodeFile` manage user trees, mounts and projections.

This enables:

- instant, one‑click snapshots of layouts without copying content — snapshots record references and restores are an atomic pointer switch (fast even for millions of files);
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

See controllers under `src/Cotton.Server/Controllers`.

---

## Storage Pipeline

`src/Cotton.Storage` composes a backend (`IStorageBackend`) with a set of streaming processors (`IStorageProcessor`) into a single pipeline:

- `CompressionProcessor` — Zstd (streaming).
- `CryptoProcessor` — wraps streams with streaming AES-GCM encrypt/decrypt.
- Storage backend — persists chunk blobs (e.g. `FileSystemStorageBackend` or `S3StorageBackend`).

Write flows **forward** through processors; read flows **backwards**.  
In practice, ordering is controlled by processor `Priority`, so writes run **compression → crypto → backend**, and reads run **backend → crypto → decompression**.
This gives a real storage engine, not just `File.WriteAllBytes`.

---

## Cryptography & Performance

Crypto is powered by **EasyExtensions.Crypto** (NuGet) — a streaming AES-GCM engine (`AesGcmStreamCipher`) with:

- Per-file wrapped keys and per-chunk authentication.
- 12-byte nonce layout (4-byte file prefix + 8-byte chunk counter).
- Parallel, chunked pipelines built on `System.IO.Pipelines`.

Performance characteristics:

- Decrypt throughput ~9–10 GB/s across large chunk sizes on typical dev hardware.
- Encrypt scales to memory bandwidth (~14–16+ GB/s; up to ~16–17 GB/s in favorable benchmarks) around 1–2 threads with ~1 MiB chunks.
- Scaling efficient up to 2–4 threads, then limited by shared resources (memory BW / caches).

This isn’t “engineering overkill for bragging rights” — it’s deliberate headroom. When the crypto/compression pipeline is comfortably faster than your storage/network, the app stays responsive on NAS-class hardware, and you can throw very large trees and multi‑GB transfers at it without the system becoming fragile. Benchmarks include modest/NAS‑class machines — in my tests encryption and streaming compression remain faster than a typical 2.5 Gbit network link.

Hashing for addressing uses SHA-256 from EasyExtensions.Crypto.

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

Upload settings are returned by the server; the frontend (`src/cotton.client`) uses them for chunking.

---

## Technical Highlights

Some careful engineering decisions worth highlighting:

**RangeStreamServer for FFmpeg**  
Video preview generation wraps seekable streams in a mini HTTP server with semaphore-protected seek+read. FFmpeg/ffprobe make parallel range requests (moov atom + linear reads) — without this coordination, stream access would deadlock.

**FileSystemStorageBackend atomic writes**  
Chunks are written via temp file + atomic move, then marked read-only and excluded from Windows indexing. This ensures "safe" chunk persistence without partial writes or indexer churn.

**Preview URL security**  
Previews are stored content-addressed by hash, but URLs expose **encrypted** preview hashes (`EncryptedPreviewImageHash`). Server decrypts on request — prevents content enumeration while keeping storage deduped.

**ETag + 304 + range processing**  
Downloads and previews properly support `ETag`, `If-None-Match` (304 responses), and `Range` headers (`enableRangeProcessing`). Browsers and CDNs cache efficiently; partial downloads resume cleanly.

**Download tokens with auto-cleanup**  
Share tokens can be single-use (`DeleteAfterUse`) and expire after configurable retention. Background job sweeps expired tokens — no manual "clean up shares" UI needed.

**GC-aware chunk ingest**  
The garbage collector coordinates with ingestion: if a chunk is currently being deleted, the ingest path will refuse/hold concurrent uploads of that same chunk until the delete completes—this prevents rare races and windows where a delete and an upload could conflict. The behavior is deliberate: safety first, then fast reconciliation.  
_See: `src/Cotton.Server/Jobs/GarbageCollectorJob.cs`, `src/Cotton.Server/Services/ChunkIngestService.cs`_

**Industrial-strength NameValidator**  
Enforces Unicode normalization (NFC), grapheme cluster limits, bans zero-width/control chars, forbids `.`/`..`, blocks Windows reserved names (`CON`, `PRN`, etc.), trims trailing dots/spaces. Generates case-insensitive, diacritic-stripped `NameKey` for collision detection.

**Autoconfig env scrubbing**  
`Cotton.Autoconfig` derives keys from `COTTON_MASTER_KEY`, then **wipes the env var** from Process and User environment after startup. Secrets don't linger in memory dumps or child processes.

**Client-side upload pipeline**  
Browser uploads hash chunks in a Web Worker (one pass for both chunk-hash and rolling file-hash), parallelize uploads (default 4 in-flight), send only missing chunks on retry. UI stays responsive even with 10k+ file folders.

---

## Deep Dive: Implementation Details

### Backend (Cotton.Server)

**Content-addressed storage**  
Chunks are identified by SHA-256 hash (the hash **is** the identifier). Dedup is built into the model, not an "optimization".  
_See: `src/Cotton.Server/Controllers/ChunkController.cs`, `src/Cotton.Database/Models/Chunk.cs`_

**Chunk-first upload protocol**  
Upload chunks first, then assemble the file via manifest. Re-upload is idempotent: server responds "already exists" for duplicate chunks.  
_See: `ChunkController.cs`, `FileController.cs`_

**File manifests and reuse**  
Server creates `FileManifest` + ordered `FileManifestChunk` entries, stores `ProposedContentHash`, then async job computes `ComputedContentHash`. If a file with matching hash exists, manifest is reused.  
_See: `src/Cotton.Database/Models/FileManifest.cs`, `FileController.UpdateFileContent`, `src/Cotton.Server/Jobs/ComputeManifestHashesJob.cs`_

**Download with Range/ETag/tokens**

- `Cache-Control: private, no-store` (no public cache)
- ETag tied to content SHA-256
- `enableRangeProcessing: true` → browser resume/seek works
- `DeleteAfterUse` tokens: one-time links, deleted via `Response.OnCompleted` callback
- Retention job cleans expired tokens in batches  
  _See: `src/Cotton.Server/Controllers/FileController.cs`, `DownloadTokenRetentionJob.cs`_

**Auth: JWT + refresh tokens**  
Access tokens (JWT) + refresh tokens stored in DB with rotation. Refresh cookie uses `HttpOnly`, `Secure`, `SameSite=Strict`.  
_See: `src/Cotton.Server/Controllers/AuthController.cs`_

---

### Storage Engine (Cotton.Storage)

**Processor pipeline with proper directionality**  
`FileStoragePipeline` sorts processors by `Priority`. Writes flow **forward** (compression → crypto → backend), reads flow **backwards** (backend → crypto → decompression).  
_See: `src/Cotton.Storage/Pipelines/FileStoragePipeline.cs`_

**FileSystem backend: atomic writes**  
Writes to temp file, then atomic move. Sets read-only + excludes from Windows indexing.  
_See: `src/Cotton.Storage/Backends/FileSystemStorageBackend.cs`_

**S3 backend with segments**  
Checks existence before write to avoid redundant uploads. Supports namespace "segments" for partitioning.  
_See: `src/Cotton.Storage/Backends/S3StorageBackend.cs`_

**ConcatenatedReadStream: fully seekable stream assembled from chunks**  
A single logical, seekable stream is assembled from multiple chunk streams (no file reassembly). `Seek` is implemented by locating the target chunk (binary search over cumulative offsets) and switching the underlying stream on‑the‑fly so reads always come from the correct chunk.  
Why this matters — and why it's hard: it enables true `Range` downloads, fast partial reads, and efficient preview extraction (no temp files or full-file buffering), and it lets the same storage backends serve CDN/HTTP range requests and FFmpeg without extra I/O. Implementing this required non‑trivial coordination: preserving per‑chunk AES‑GCM authentication/nonce layout while supporting arbitrary seeks, cooperating with compression and pipeline ordering, and supporting concurrent range reads with minimal overhead.  
_See: `src/Cotton.Storage/Streams/ConcatenatedReadStream.cs`, `src/Cotton.Storage/Pipelines/FileStoragePipeline.cs`._

**CachedStoragePipeline for small objects**  
In-memory cache (~100MB default) with per-object size limits. `StoreInMemoryCache=true` forces caching (ideal for previews/icons).  
_See: `src/Cotton.Storage/Pipelines/CachedStoragePipeline.cs`, used in `PreviewController.cs`_

**Compression processor**  
Zstandard (zstd) via `ZstdSharp` — streaming, enabled by default and integrated into the storage pipeline. Compression is applied _before_ encryption (so it is effective); default compression level is `2` (`CompressionProcessor.CompressionLevel`). In practice this typically stays comfortably above multi‑gigabit networking speeds, so compression doesn’t turn into the bottleneck. The processor is implemented as a streaming `Pipe`/`CompressionStream` (no full-file temp files) and is registered via DI; ordering is controlled via processor `Priority`. Tests and benchmarks exercise compressible vs random data.  
_See: `src/Cotton.Storage/Processors/CompressionProcessor.cs` and `src/Cotton.Storage.Tests/Processors/CompressionProcessorTests.cs`._

---

### Cryptography (EasyExtensions.Crypto)

**Streaming AES-GCM cipher**  
Pure C# streaming encryption with:

- The library was especially designed for **Cotton Cloud** but is reusable elsewhere.
- Header + key wrapping
- Per-chunk authentication tags
- 12-byte nonces (4-byte file prefix + 8-byte chunk counter)
- Parallel pipelines for encrypt/decrypt with reordering
- `ArrayPool` + bounded buffers for memory efficiency
- Parameterized window sizes and memory limits  
  _See: `EasyExtensions.Crypto/AesGcmStreamCipher.cs`, `.../Internals/Pipelines/EncryptionPipeline.cs`, `DecryptionPipeline.cs`_

**Performance measurements**  
Separate Charts project with benchmarks/graphs — rare for open source, strong engineering argument.  
_See: `EasyExtensions.Crypto.Tests.Charts/README.md`_

---

### Previews (Cotton.Previews)

**Preview controller: HTTP caching + encrypted hashes**  
Previews stored content-addressed by hash, but URLs expose **encrypted** preview hashes. Server decrypts → fetches blob.  
`ETag` + `Cache-Control: public, max-age=31536000, immutable` → aggressive browser/CDN caching, invalidated by hash change.  
_See: `src/Cotton.Server/Controllers/PreviewController.cs`_

**Text preview generator**  
Reads limited chars, splits by lines, renders on canvas, downscales, saves as WebP. Embeds font from resources.  
_See: `src/Cotton.Previews/TextPreviewGenerator.cs`_

**Image preview generator**  
Resize to preview dimensions, output as WebP.  
_See: `src/Cotton.Previews/ImagePreviewGenerator.cs`_

**PDF preview generator**  
Docnet (MuPDF wrapper) → render first page → WebP.  
_See: `src/Cotton.Previews/PdfPreviewGenerator.cs`_

**Video preview generator**

- Auto-downloads FFmpeg/ffprobe if missing (`FFMpegCore`)
- Uses a pragmatic local HTTP shim (`RangeStreamServer`) — a deliberate "hack" that exposes a seekable `ConcatenatedReadStream` over HTTP so FFmpeg/ffprobe can perform parallel `Range` requests. The server serializes seek+read with a semaphore to avoid deadlocks and avoids writing full temp files.
- Extracts a frame from mid-duration (or other requested time) directly from chunked, encrypted/compressed storage.
- Robust timeouts, process kill and detailed logging to contain external tools.  
  _See: `src/Cotton.Previews/VideoPreviewGenerator.cs`, `src/Cotton.Previews/Http/RangeStreamServer.cs`._
  _See: `src/Cotton.Server/Jobs/GeneratePreviewJob.cs`_

---

### Frontend (cotton.client)

**Chunk upload subsystem**  
`uploadBlobToChunks.ts`:

- Splits blob into chunks by `server.maxChunkSizeBytes`
- Parallelizes uploads via `maxConcurrency`
- Computes hashes in-flight
- Custom throttling via `Set + Promise.race` to control in-flight promises  
  _See: `src/shared/upload/uploadBlobToChunks.ts`_

**Hashing in Web Worker**  
Separate worker holds incremental hash state for file and chunk. Doesn't block UI thread.  
_See: `src/shared/upload/hash.worker.ts`_

**UploadManager & queue widget**  
Tracks tasks, progress, status, errors, speed. `UploadQueueWidget`: drawer/stack from bottom, fill-based progress bars, `pointerEvents: none` where needed to avoid scroll interference.  
_See: `src/shared/upload/UploadManager.ts`, `src/pages/HomePage/components/UploadQueueWidget.tsx`_

**Sharing UX**  
Media lightbox uses `navigator.share`, fallback to clipboard copy. Controls auto-hide on inactivity via `useActivityDetection`. Minimal UI: only essential actions.  
_See: `src/pages/HomePage/components/MediaLightbox.tsx`, `src/shared/hooks/useActivityDetection.ts`_

**In-cloud text editing**  
`TextPreview`: downloads via link, shows content, enables edit mode, saves via same chunk uploader (text files = chunks/manifest like any other file).  
_See: `src/pages/HomePage/components/TextPreview.tsx`_

**Polish details**

- `FileSystemItemCard`: hover marquee for long names via `ResizeObserver` + delays, clean tooltips/menus
- HTTP client: refresh queue to prevent thundering herd of refresh requests on simultaneous 401s
- PWA configured (`vite-plugin-pwa` + manifest/screenshots)  
  _See: `src/pages/HomePage/components/FileSystemItemCard.tsx`, `src/shared/api/httpClient.ts`, `vite.config.ts`_

---

### Validation & Security (Cotton.Validators, Cotton.Autoconfig)

**NameValidator: industrial-grade hygiene**

- Unicode normalization (NFC)
- Grapheme cluster limits
- Forbids zero-width/control characters
- Blocks `.`/`..`
- Windows reserved names (`CON`, `PRN`, `AUX`, etc.)
- Trims trailing dots/spaces
- Case-folded `NameKey` (diacritic-stripped + lowercase) for case-insensitive collision detection  
  _See: `src/Cotton.Validators/NameValidator.cs`, `src/Cotton.Database/Models/Node.cs`_

**Autoconfig: env scrubbing**  
Derives `Pepper` + `MasterEncryptionKey` from `COTTON_MASTER_KEY`, then **wipes** the env var from Process and User environment after startup. Secrets don't leak to child processes or memory dumps.  
_See: `src/Cotton.Autoconfig/Extensions/ConfigurationBuilderExtensions.cs`_

**Bootstrap & setup**  
Auto-applies EF migrations on startup. First admin creation has time-limited window (no eternal backdoor).  
_See: `src/Cotton.Server/Program.cs`, `AuthController.cs`, `SetupController.cs`_

---

## Roadmap (short)

- Generational GC with compaction/merging of small "dust" chunks (design complete; implementation pending).
- Adaptive defaults and auto-tuning (chunk sizes / buffers / threading) informed by opt-in performance telemetry.
- Additional processors (cache, S3 replica, cold storage, etc.).
- Hardening auth flows and extending UI around uploads/layouts and sharing.
- Native/mobile and desktop clients reusing the same engine.

---

## License & Branding

- License: MIT (see `LICENSE`).
- The "Cotton" name and marks are reserved; forks should use distinct names/marks.

---

## Repo Map

- `src/Cotton.Server` — ASP.NET Core API + UI hosting.
- `src/Cotton.Database` — EF Core models and migrations.
- `src/Cotton.Storage` — storage pipeline and processors.
- `src/Cotton.Previews` — preview generators (image, PDF via Docnet/MuPDF, video via FFmpeg, text).
- `src/Cotton.Topology` — layout/topology manipulation services.
- `src/cotton.client` — TypeScript/Vite frontend.
- **EasyExtensions.Crypto** (NuGet) — streaming AES-GCM, key derivation, hashing.

---

Built as a cohesive storage system: clean model, careful crypto, pragmatic performance — with a UI that actually takes advantage of it.
