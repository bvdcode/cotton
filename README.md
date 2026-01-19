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

**All managed .NET/C#** core — crypto and storage engine are pure managed code. Preview generators (video/PDF) are optional and use native dependencies (FFmpeg, MuPDF via Docnet).

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
  — see `src/Cotton.Database/Models`.

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

See controllers under `src/Cotton.Server/Controllers`.

---

## Storage Pipeline

`src/Cotton.Storage` implements a processor pipeline:

- `FileSystemStorageProcessor` — persists chunk blobs on disk (`.ctn`).
- `CryptoProcessor` — wraps streams with streaming AES-GCM encrypt/decrypt.
- `CompressionProcessor` — Zstd.

Write flows **forward** through processors; read flows **backwards**.  
This gives a real storage engine, not just `File.WriteAllBytes`.

---

## Cryptography & Performance

Crypto is powered by **EasyExtensions.Crypto** (NuGet) — a streaming AES-GCM engine (`AesGcmStreamCipher`) with:

- Per-file wrapped keys and per-chunk authentication.
- 12-byte nonce layout (4-byte file prefix + 8-byte chunk counter).
- Parallel, chunked pipelines built on `System.IO.Pipelines`.

Performance characteristics:

- Decrypt throughput ~9–10 GB/s across large chunk sizes on typical dev hardware.
- Encrypt scales to memory bandwidth (~14–16+ GB/s) around 1–2 threads with ~1 MiB chunks.
- Scaling efficient up to 2–4 threads, then limited by shared resources (memory BW / caches).

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
`FileStoragePipeline` sorts processors by `Priority`. Writes flow **forward** through processors (FS → crypto → compression), reads flow **backwards**.  
_See: `src/Cotton.Storage/Pipelines/FileStoragePipeline.cs`_

**FileSystem backend: atomic writes**  
Writes to temp file, then atomic move. Sets read-only + excludes from Windows indexing.  
_See: `src/Cotton.Storage/Backends/FileSystemStorageBackend.cs`_

**S3 backend with segments**  
Checks existence before write to avoid redundant uploads. Supports namespace "segments" for partitioning.  
_See: `src/Cotton.Storage/Backends/S3StorageBackend.cs`_

**ConcatenatedReadStream: seekable stream over chunks**  
Single logical stream assembled from multiple chunk streams. Supports `Seek` via binary search over cumulative offsets, switches underlying stream on-the-fly.  
This is **why** Range downloads/video streaming/previews work seamlessly without reassembling files on disk.  
_See: `src/Cotton.Storage/Streams/ConcatenatedReadStream.cs`_

**CachedStoragePipeline for small objects**  
In-memory cache (~100MB default) with per-object size limits. `StoreInMemoryCache=true` forces caching (ideal for previews/icons).  
_See: `src/Cotton.Storage/Pipelines/CachedStoragePipeline.cs`, used in `PreviewController.cs`_

**Compression processor**  
Zstd via `ZstdSharp`. Currently buffers in `MemoryStream` (controlled allocation within chunk limits).  
_See: `src/Cotton.Storage/Processors/CompressionProcessor.cs`_

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
- Launches `RangeStreamServer`: mini HTTP server over seekable stream with semaphore-protected seek+read (FFmpeg makes parallel range requests)
- Extracts frame from mid-duration
- Timeouts + process kill, detailed logging  
  _See: `src/Cotton.Previews/VideoPreviewGenerator.cs`, `src/Cotton.Previews/Http/RangeStreamServer.cs`_

**Background preview job**  
Generates previews only for supported MIME types. Stores preview as blob by hash, saves encrypted preview hash in DB.  
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
