![Status](https://img.shields.io/badge/status-beta-yellow)
[![SHCS Class A](https://img.shields.io/badge/SHCS-Class%20A-brightgreen)](docs/specs/SHCS-0001.md)
[![License](https://badgen.net/github/license/bvdcode/cotton)](LICENSE)
[![CI](https://github.com/bvdcode/cotton/actions/workflows/docker-image.yml/badge.svg)](https://github.com/bvdcode/cotton/actions)
[![CodeFactor](https://www.codefactor.io/repository/github/bvdcode/cotton/badge)](https://www.codefactor.io/repository/github/bvdcode/cotton)
[![Release](https://badgen.net/github/release/bvdcode/cotton?label=version)](https://github.com/bvdcode/cotton/releases)
[![Docker Pulls](https://badgen.net/docker/pulls/bvdcode/cotton?icon=docker&label=pulls)](https://hub.docker.com/r/bvdcode/cotton)
[![Docker Image Size](https://badgen.net/docker/size/bvdcode/cotton?icon=docker&label=size)](https://hub.docker.com/r/bvdcode/cotton)
[![Github last-commit](https://img.shields.io/github/last-commit/bvdcode/cotton)](https://github.com/bvdcode/cotton/commits/main/)

> **[Using Cotton? Say hi here!](https://github.com/bvdcode/cotton/discussions/3)**  
> Live demo: [cotton.splidex.com](https://cotton.splidex.com/)

<div align="center">

# Cotton Cloud

### Self-hosted file cloud built to stay fast, rollback-friendly, and operationally safe

**All managed .NET/C#** core with content-addressed storage, streaming AES-GCM crypto, and a UI designed for real day-to-day use.

</div>

![Cotton Cloud](src/cotton.client/public/assets/images/social-preview.jpg)

---

## What Is Cotton?

Cotton Cloud is a self-hosted file cloud designed to stay fast, storage-efficient, and predictable as your dataset grows. It is built around its own **content-addressed storage engine**, **streaming AES-GCM crypto**, and a layout model that keeps navigation, restore, sharing, and background maintenance practical instead of fragile.

This is not just a storage engine with a web skin. Cotton is meant to feel good in real use:

- folder and file listing stays fast on very large trees;
- snapshots and restores are first-class operations, not a disaster-only afterthought;
- uploads stream cleanly in the browser without freezing the UI;
- integrity checks and storage consistency work happen in the background and surface real warnings;
- sharing, previews, password reset, notifications, and setup behave like product features, not TODO items.

If you want the architecture details, keep reading below. If you want the 30-second version, start with the next three sections.

---

## Why Cotton Feels Different

Most self-hosted file clouds can describe their internals. Fewer can explain why those internals make daily use feel better.

Cotton is built around a different set of outcomes:

- **Restore is normal, even at large scale**  
  Snapshots record references instead of copying data. Restoring a large layout is an atomic layout switch, so rollback stays practical even when the dataset is huge.

- **Navigation stays fast because the metadata model is structural**  
  Cotton separates content from layout and models trees explicitly. That avoids the path-string-heavy behavior that makes many systems feel sluggish or fragile once folders get large.

- **Cleanup is cautious, not reckless**  
  Unused data is scheduled, re-checked, and only then reclaimed. If something becomes live again before deletion, the reclaim is cancelled. Ingest also coordinates with GC so delete and re-upload do not fight each other.

- **Integrity is an active behavior**  
  Cotton does more than store checksums. It computes manifest hashes in the background, runs storage consistency checks, and raises notifications when upload verification fails or stored file data is missing.

- **Operational polish is part of the product**  
  First-run setup is a guided wizard, SMTP is a first-class setting, forgot-password and email verification flows exist, notifications are built in, and setup includes explicit timezone selection instead of leaving operators to guess around server-local defaults.

- **Sharing is meant to be used, not merely exposed**  
  Cotton has share pages, rich previews, token expiry and cleanup, and native platform sharing hooks where the browser supports them.

In short: unlike systems that are mostly a filesystem wrapper, Cotton is designed so storage behavior, UI behavior, and operational behavior reinforce each other.

---

## What You Can Actually Do With It

- Roll back very large layouts in one action because snapshots are reference-based and restore is an atomic pointer switch.
- Browse folders with hundreds of thousands or millions of entries without the UI collapsing into a sluggish legacy experience.
- Upload multi-GB files and large folders from the browser while the UI stays responsive.
- Re-send only missing chunks after interruptions instead of restarting an entire upload.
- Use built-in deduplication, streaming compression, and streaming encryption in the main storage path.
- Share files and folders with expiring links, share pages, previews, and native OS/browser share integration where available.
- Generate previews for images, HEIC, PDF, text, audio, and video content.
- Run background manifest verification and storage consistency checks that surface real integrity problems.
- Receive useful notifications for failed logins, successful logins, TOTP events, WebDAV token resets, shared-file downloads, upload verification failures, and missing storage chunks.
- Configure the instance through a setup wizard with safe defaults, custom SMTP support, storage choices, telemetry preferences, and timezone selection.
- Offer email verification and a forgot-password flow instead of treating account recovery as somebody else's problem.
- Start with a simple Docker + Postgres deployment and grow into filesystem or S3-backed storage.
- Use WebDAV in addition to the web UI when you need protocol-level access.

---

## Compared To The Usual Experience

- Unlike path-string-heavy metadata models, Cotton is built around structural relationships between layouts, nodes, files, manifests, and chunks.
- Unlike systems where restore and cleanup can work against each other, Cotton delays reclaim, re-checks references before delete, and coordinates ingest with GC.
- Unlike products where sharing is just a raw download URL, Cotton has share pages, previews, expiry, cleanup, and native-share integration.
- Unlike setups that stop at "the server started", Cotton includes a guided setup flow, SMTP options, password reset, email verification, and built-in notifications.

---

## Product Snapshots

Current screenshots from the shipped web client:

| Large-folder navigation                                                      | Gallery browsing                                                            | Search in the same workflow                                                |
| ---------------------------------------------------------------------------- | --------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| ![Folder navigation](src/cotton.client/public/assets/images/screenshot1.jpg) | ![Gallery browsing](src/cotton.client/public/assets/images/screenshot2.jpg) | ![Search workflow](src/cotton.client/public/assets/images/screenshot5.jpg) |

The UI matters here because Cotton is not trying to prove a storage thesis in isolation. The engine is built so the product can stay responsive, preview-rich, and straightforward to operate.

---

## What This Enables In Practice

| Design choice                                                  | Practical outcome                                                                                               |
| -------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| Layout graph/tree separated from content storage               | Fast listing, navigation, snapshotting, and remounting without turning every operation into string-path surgery |
| Content-addressed chunks and manifests                         | Deduplication, safe content reuse, idempotent uploads, and restore that does not require re-copying data        |
| Streaming pipeline ordered as compression -> crypto -> backend | Efficient storage and encryption without offline repack jobs or giant temporary files                           |
| Chunk-first upload protocol                                    | Interrupted uploads recover cleanly and retries only send what the server still needs                           |
| Background manifest hashing and storage consistency jobs       | Upload mismatches and missing stored data become visible operator events instead of silent corruption           |
| Encrypted preview hashes plus dedicated preview generators     | Rich previews and share pages without exposing raw storage identifiers                                          |
| Virtualized large-directory UI backed by structural metadata   | Folder browsing still feels immediate on large trees                                                            |

This is the core difference from the usual bad experience: Cotton's architecture is not interesting for its own sake. It is interesting because it changes how restore, browsing, cleanup, sharing, and operations behave under real load.

---

## Performance Highlights

- **Crypto headroom is deliberately high**  
  Current measurements in this repo put decrypt around **9-10 GB/s** and encrypt around **14-16+ GB/s** on typical development hardware, with encryption scaling into memory-bandwidth limits rather than becoming the first bottleneck.

- **Compression and encryption are in the main pipeline**  
  Cotton compresses before encrypting, so storage savings happen inline instead of depending on a later maintenance job.

- **Uploads are designed for sustained throughput**  
  The client hashes chunks in a web worker, uploads chunks in parallel, and retries only missing chunks after interruptions.

- **Large-tree UX is part of the performance story**  
  The virtualized folder UI and structural metadata model are designed so listing and navigation remain responsive on ordinary hardware.

- **Benchmarks use real production code**  
  The dedicated benchmark suite exercises the real compression processor, crypto processor, filesystem backend, and full storage pipeline rather than mocks. See [src/Cotton.Benchmark/README.md](src/Cotton.Benchmark/README.md).

If you are comparing Cotton to the usual self-hosted stack, this matters: the engine is built with enough throughput headroom that storage and networking stay the dominant limits on normal hardware.

---

## Reliability & Safety

- **Restore-friendly delete model**  
  Cotton does not immediately destroy unreferenced content. Orphaned manifests and chunks are scheduled for cleanup, re-checked before deletion, and left alone if they become live again.

- **GC and ingest cooperate**  
  If a chunk is being deleted, the ingest path waits instead of racing the delete. That closes an ugly class of "delete while re-uploading" edge cases.

- **Background verification exists today**  
  Manifest hashes are computed after upload. If the computed hash does not match the proposed one, Cotton raises a notification instead of silently trusting bad data.

- **Storage consistency is checked against reality**  
  Cotton periodically compares DB-tracked chunks against the actual storage backend. Missing preview-only blobs are cleared safely; missing real file data triggers explicit user notifications.

- **Background jobs are built into normal operation**  
  Preview generation, manifest hashing, token cleanup, temp cleanup, performance collection, MIME fixes, and storage consistency checks are all part of the system rather than manual maintenance scripts.

- **Operator-facing details are treated explicitly**  
  Setup captures storage mode, email mode, and timezone up front, because things like notifications, disk activity, and retention behavior should be deliberate.

Cotton's current reclaim model is already cautious and restore-friendly. More advanced generational compaction is on the roadmap, but the shipped behavior is already designed around "do not reclaim first and ask questions later."

---

## Small Details That Matter

- Share links can be invalidated in bulk, expire automatically, and be cleaned up in the background.
- Public sharing is backed by real share pages and previews, not just raw opaque URLs.
- Browsers that support the Web Share API get native sharing; everyone else gets a predictable clipboard fallback.
- Password reset, email verification, and SMTP configuration are part of the main product flow.
- Notifications cover real account and storage events, including failed logins, login success, TOTP lockouts, WebDAV token resets, and shared-file downloads.
- The first-run experience is a guided setup wizard with safe defaults and expert paths instead of a half-documented config scavenger hunt.

---

## Quick Start

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

On startup the app applies EF migrations automatically, serves the UI at `http://localhost:8080` (or whatever port you mapped), and walks you through the in-browser setup wizard.

Upload settings are returned by the server; the frontend (`src/cotton.client`) uses them for chunking.

For deeper internals after the quick start, continue below. For benchmark details, see [src/Cotton.Benchmark/README.md](src/Cotton.Benchmark/README.md).

---

## Architecture Overview

Separation of concerns is similar to git:

- **Content ("what")**  
  `Chunk`, `FileManifest`, `FileManifestChunk`, `ChunkOwnership`  
  — content-addressed storage, deduplication, hashing, and chunk ownership.

- **Layout ("where")**  
  `Layout`, `Node`, `NodeFile`  
  — user-visible trees, mounts, projections, and restore points.

This split is what gives Cotton several of its user-facing strengths:

- snapshots record references instead of copying content;
- restoring a layout is an atomic switch rather than a bulk copy;
- the same content can be mounted in multiple places with no duplication;
- layout operations stay fast because structure is modeled directly.

---

## Chunk-First Upload Model

Clients upload independent chunks (size <= server limit) identified by **SHA-256**.

1. **POST chunk** — server verifies the hash, runs the chunk through the storage pipeline, and stores it or replies that it already exists.
2. **POST file manifest** — client sends the ordered list of chunk hashes and Cotton assembles the file at the target node.

Practical effect:

- interrupted uploads do not corrupt the dataset;
- retries only send **missing** chunks;
- different clients can choose different chunk sizes while still assembling the same file;
- the server can verify content integrity before the file becomes part of the visible layout.

Selected endpoints:

- `POST /chunks` — upload a chunk with hex SHA-256.
- `POST /files/from-chunks` — create a file from an ordered list of chunk hashes at a layout node.

See controllers under `src/Cotton.Server/Controllers`.

---

## Storage Pipeline

`src/Cotton.Storage` composes a backend (`IStorageBackend`) with streaming processors (`IStorageProcessor`) into a single pipeline:

- `CompressionProcessor` — Zstd (streaming).
- `CryptoProcessor` — streaming AES-GCM encrypt/decrypt.
- Storage backend — persists chunk blobs, such as `FileSystemStorageBackend` or `S3StorageBackend`.

Writes flow forward through processors, reads flow backward. In practice, ordering is controlled by processor `Priority`, so writes run **compression -> crypto -> backend**, and reads run **backend -> crypto -> decompression**.

That is why Cotton can do compression, encryption, range reads, previews, and chunk reuse as one coherent storage engine instead of a stack of loosely-related afterthoughts.

---

## Cryptography & Performance

Crypto is powered by **EasyExtensions.Crypto** (NuGet), a streaming AES-GCM engine (`AesGcmStreamCipher`) with:

- per-file wrapped keys and per-chunk authentication;
- a 12-byte nonce layout (4-byte file prefix + 8-byte chunk counter);
- parallel chunked pipelines built on `System.IO.Pipelines`.

Performance characteristics:

- decrypt throughput around **9-10 GB/s** across large chunk sizes on typical development hardware;
- encrypt throughput around **14-16+ GB/s**, often limited by memory bandwidth rather than the cipher itself;
- efficient scaling up to a few threads before shared hardware limits take over.

This headroom is deliberate. When crypto and compression stay comfortably faster than storage and network I/O, the system remains responsive on NAS-class hardware and under large transfers.

Hashing for content addressing uses SHA-256.

---

## Security & Validation

- **Passwords**  
  PBKDF2 service with modern PHC-style handling via `EasyExtensions`.

- **Keys & settings**  
  `Cotton.Autoconfig` derives `Pepper` and `MasterEncryptionKey` from a single `COTTON_MASTER_KEY` environment variable and exposes `CottonSettings` such as:
  - `MasterEncryptionKeyId`
  - `EncryptionThreads`
  - `CipherChunkSizeBytes`
  - `MaxChunkSizeBytes`

- **Name hygiene**  
  `Cotton.Validators/NameValidator` enforces Unicode normalization, blocks `.` / `..`, control and zero-width characters, trims trailing dots and spaces, rejects Windows reserved names, and provides a case-folded `NameKey` for collision-safe lookups.

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
