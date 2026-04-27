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
> Live demo: [cotton.splidex.com](https://cotton.splidex.com/) - user and password: `demo` / `demo`, or whatever credentials you enter will create your user

<div align="center">

# Cotton Cloud

### Self-hosted file cloud built to stay fast, rollback-friendly, and operationally safe

**All managed .NET/C#** core with content-addressed storage, streaming AES-GCM crypto, and a UI designed for real day-to-day use.

</div>

![Cotton Cloud](src/cotton.client/public/assets/images/social-preview.jpg)

---

## What Is Cotton?

Cotton Cloud is a self-hosted file cloud designed to stay fast, storage-efficient, and predictable as your dataset grows. It is built around its own **content-addressed storage engine**, **streaming AES-GCM crypto**, and a layout model that keeps navigation, restore, sharing, and background maintenance practical instead of fragile.

The server core runs on a modern **ASP.NET Core + EF Core** stack and uses **Kestrel** for high-throughput HTTP streaming and API workloads.

Cotton is intentionally built as one cohesive runtime: web engine, storage pipeline, crypto core, compression, and most preview/image processing run in managed .NET code inside the same ecosystem. That keeps execution flow seamless, reduces cross-environment glue, and helps the codebase behave as one coordinated system rather than many loosely-coupled runtimes. External process tooling is kept narrow on purpose: **FFmpeg/ffprobe** are used for audio/video preview extraction.

This is not architecture for architecture's sake. Cotton is built this way because the design shows up directly in real behavior: the system is more predictable under load, easier to reason about operationally, and less likely to inherit strange edge cases from decades of layered legacy glued around older technology stacks. From web server to storage path, it is meant to behave like one platform.

The core product is intentionally focused rather than trying to be everything at once; custom behavior is meant to live in isolated plugins and marketplace-delivered extensions as that layer settles into place.

Cotton is also an actively developing open-source project. Like any serious storage system, it still needs time to accumulate broader real-world mileage, more operators, and more long-tail edge-case exposure. That should be read as a normal maturity curve, not as "there is no reason to trust it": the core is deliberate, cohesive, and built around stable storage principles rather than accidental behavior.

This is not just a storage engine with a web skin. Cotton is meant to feel good in real use:

- folder and file listing stays fast on very large trees;
- snapshots are first-class operations, with the same model carrying into instant tree rollback and restore workflows;
- uploads stream cleanly in the browser without freezing the UI;
- large files stay seekable, previewable, and streamable without full re-download or reassembly;
- integrity checks and storage consistency work happen in the background and surface real warnings;
- sharing, previews, password reset, notifications, and setup behave like product features, not TODO items.

If you want the architecture details, keep reading below. If you want the 30-second version, start with the next three sections.

---

## What Happens To A File In Cotton?

Upload -> chunked + hashed -> compressed + encrypted -> previewable -> shareable -> seekable -> restorable -> integrity-checked -> reclaim-safe.

That is the product story in one line: a file should not become an opaque blob you are afraid to touch once it enters the system.

---

## Why Cotton Feels Different

Most self-hosted file clouds can describe their internals. Fewer can explain why those internals make daily use feel better.

Cotton is built around a different set of outcomes:

- **The system is designed to stay predictable end-to-end**  
  Cotton is not a patchwork of many runtimes with years of historical baggage pulling in different directions. The same design philosophy runs from HTTP handling to storage layout, which is why behavior under load, cleanup, sharing, and recovery can stay consistent instead of feeling incidental.

- **Restore is normal, even at large scale**  
  Snapshots record references instead of copying data. That keeps large-scale rollback practical and is the same model the instant tree rollback flow is being built around.

- **Navigation stays fast because the metadata model is structural**  
  Cotton separates content from layout and models trees explicitly. That avoids the path-string-heavy behavior that makes many systems feel sluggish or fragile once folders get large.

- **Wide ecosystems are good, but throughput still decides daily usability**  
  A broad app ecosystem looks great on paper, but what is the point if the native client cannot upload fast and keep that speed. Cotton treats ingest throughput as a first-class product requirement: parallel chunk upload, missing-chunk retry, and sustained large-transfer behavior are in the main path so uploads are meant to run against the practical ceiling of the server for the full transfer, not just spike early and sag halfway through.

- **Huge files are not treated like a separate crisis path**  
  By design, a tiny file and a huge file go through the same chunk-plus-manifest model. Size mostly changes transfer duration and chunk count, not the fundamental shape of the operation or the kind of load the system has to invent a special path for.

- **Large media stays usable without a full download**  
  Cotton can serve range reads, seek inside large files, and extract previews or video frames directly from chunked encrypted storage, including S3-backed storage, without reassembling the whole object first.

- **Compression is in the main path, not an afterthought**  
  Compressible data is reduced inline before encryption, so Cotton gets the storage benefit during the actual transfer instead of depending on a later maintenance pass.

- **Cleanup is cautious, not reckless**  
  Unused data is scheduled, re-checked, and only then reclaimed. If something becomes live again before deletion, the reclaim is cancelled. Ingest also coordinates with GC so delete and re-upload do not fight each other.

- **Integrity is an active behavior**  
  Cotton does more than store checksums. It computes manifest hashes in the background, runs storage consistency checks, and raises notifications when upload verification fails or stored file data is missing.

- **Operational polish is part of the product**  
  First-run setup is a guided wizard with practical email modes (Cloud Cotton Mail gateway, custom SMTP, or disabled email), forgot-password and email verification flows exist, notifications are built in, and setup includes explicit timezone selection instead of leaving operators to guess around server-local defaults.

- **Sharing is meant to be used, not merely exposed**  
  Cotton has share pages, rich previews, token expiry and cleanup, and native platform sharing hooks where the browser supports them.

- **Real-time sync is built in, not bolted on later**  
  File and folder changes, preview readiness, notifications, and user preference updates are pushed over SignalR so active clients stay aligned without brute-force polling.

In short: unlike systems that are mostly a filesystem wrapper, Cotton is designed so storage behavior, UI behavior, and operational behavior reinforce each other.

---

## What You Can Actually Do With It

- Use reference-based snapshots designed for one-action large-layout rollback instead of copy-heavy recovery.
- Browse folders with hundreds of thousands or millions of entries without the UI collapsing into a sluggish legacy experience.
- Upload multi-GB files and large folders from the browser while the UI stays responsive.
- Re-send only missing chunks after interruptions instead of restarting an entire upload.
- Inspect active sessions (device/IP/location metadata) and revoke individual sessions without terminating every login.
- Stream, seek, and partially download large media without reassembling the whole file first.
- Extract previews and video frames from chunked encrypted storage without a full download, including when the backend is S3-backed.
- Update file content while preserving previous content versions in a restore-friendly lineage.
- Use built-in deduplication, inline compression, and streaming encryption in the main storage path.
- Share files and folders with expiring links, share pages, previews, and native OS/browser share integration where available.
- Generate previews for images, HEIC, PDF, text, audio, and video content.
- Use the existing **WebDAV v1** implementation today for standard sync clients, phone auto-sync, and other protocol-level workflows while native Cotton clients are still in development. In Cotton, WebDAV is an important compatibility path for the early stage, not the long-term center of gravity for the product.
- Run background manifest verification and storage consistency checks that surface real integrity problems.
- Receive useful notifications for failed logins, successful logins, TOTP events, WebDAV token resets, shared-file downloads, upload verification failures, and missing storage chunks.
- Configure the instance through a setup wizard with safe defaults, cloud email or custom SMTP, storage choices, telemetry preferences, and timezone selection.
- Offer email verification and a forgot-password flow as first-class product behavior.
- Start with a simple Docker + Postgres deployment and grow into filesystem or S3-backed storage.
- Use WebDAV in addition to the web UI when you need protocol-level access.

---

## Compared To The Usual Experience

- Unlike path-string-heavy metadata models, Cotton is built around structural relationships between layouts, nodes, files, manifests, and chunks.
- Unlike systems where restore and cleanup can work against each other, Cotton delays reclaim, re-checks references before delete, and coordinates ingest with GC.
- Unlike products where sharing is just a raw download URL, Cotton has share pages, previews, expiry, cleanup, and native-share integration.
- Unlike setups that stop at "the server started", Cotton includes a guided setup flow, SMTP options, password reset, email verification, and built-in notifications.
- Unlike products that try to ship every niche feature in-core, Cotton stays focused and extends outward through isolated plugins and marketplace distribution as that layer matures.

| Area | Cotton | More typical self-hosted stack |
| --- | --- | --- |
| Web runtime | ASP.NET Core on **Kestrel** with a single app process and built-in SignalR path. Kestrel is commonly regarded as one of the highest-performance modern web servers / web stacks, with that reputation showing up repeatedly in independent [TechEmpower Benchmarks](https://www.techempower.com/benchmarks/#section=data-r23) and Microsoft's own [ASP.NET Core performance notes](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-7.0?view=aspnetcore-10.0). | A more typical self-hosted stack is layered around PHP + Apache or Nginx + FPM, often with Redis and separate workers added as supporting infrastructure |
| Storage model | Content-addressed chunks + manifests + explicit layout graph | Often path-centric metadata over a conventional filesystem view |
| Large media access | Seekable reads, `Range` responses, and preview extraction from chunked encrypted storage without full reassembly | Large-file access is more often optimized around whole-file reads, temp files, or less direct preview paths |
| Compression and encryption | Inline in the main storage pipeline | More often absent, optional, or handled outside the main ingest path |
| Restore and cleanup | Snapshot-first model with reclaim checks designed to coexist with rollback | Cleanup and restore are more likely to be separate concerns that need careful operator coordination |
| Product surface | Focused core with WebDAV, sharing, previews, notifications, and setup built in | Feature breadth is often higher, but the operational surface is also broader and less uniform |

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
| Seekable stream assembly over chunked storage                  | Range reads, media scrubbing, poster extraction, and previews without full-file reassembly or full downloads    |
| Chunk-first upload protocol                                    | Interrupted uploads recover cleanly and retries only send what the server still needs                           |
| Standards-oriented WebDAV v1 as a compatibility path           | Standard sync tools already work today; WebDAV PUT streams directly into chunk storage without full buffering, so small and large files still follow the same ingest model. Retry behavior is naturally narrower than the native chunk protocol because WebDAV PUT is a long-lived protocol request rather than Cotton's own resumable upload flow. In Cotton this is a compatibility bridge, whereas in some other systems WebDAV can end up acting like the primary working path |
| Background manifest hashing and storage consistency jobs       | Upload mismatches and missing stored data become visible operator events instead of silent corruption           |
| Encrypted preview hashes plus dedicated preview generators     | Rich previews and share pages without exposing raw storage identifiers                                          |
| Virtualized large-directory UI backed by structural metadata   | Folder browsing still feels immediate on large trees                                                            |

This is the core difference from the more common self-hosted experience: Cotton's architecture is not interesting for its own sake. It is interesting because it changes how restore, browsing, cleanup, sharing, media access, and operations behave under real load.

---

## Performance Highlights

- **Crypto headroom is deliberately high and memory-bound on modern hardware**  
  Current measurements in this repo put decrypt around **9-10 GB/s** and encrypt around **14-16+ GB/s** (Intel 13th Gen and DDR5 4200 MT/s) on typical development hardware, with encryption scaling into memory-bandwidth limits rather than becoming the first bottleneck. In practice, crypto is strong enough to stay enabled as a default core behavior instead of a feature operators have to disable for throughput.

- **Compression and encryption are in the main pipeline by default**  
  Cotton compresses before encrypting, so storage savings happen inline during the transfer instead of depending on a later maintenance job. Because content addressing and chunk identity are established independently of encrypted blob bytes, dedup remains effective even with crypto fully enabled; the server does not need semantic knowledge of "what the data is" for dedup to keep working. Compression tuning is intentionally chosen so the full pipeline stays out of the way of normal data throughput rather than fighting it.

- **File processing is designed to be practically zero-allocation on the hot path**  
  The streaming pipeline is tuned to reuse buffers and avoid unnecessary allocations, which keeps the process very thin in RAM even during sustained large transfers.

- **Modern server stack with a very fast HTTP engine**  
  Cotton.Server is built on **ASP.NET Core** and **EF Core**, served by **Kestrel** - _One of the highest-performance and most efficient modern web servers._ - so the API and streaming paths keep strong throughput under real transfer load.

- **Partial reads are a first-class path**  
  Range requests, media seeking, and preview extraction are designed into the storage engine, so users do not need to fetch a huge object just to read a slice, resume a transfer, or grab a poster frame. For content-addressed storage systems, having this level of partial-read behavior in the main path is still a notable rarity, because it's really hard to implement efficiently and requires careful coordination between the storage layout, indexing, and streaming logic.

- **Uploads are designed for sustained throughput**  
  The client hashes chunks in a web worker, uploads chunks in parallel, and retries only missing chunks after interruptions. The goal is simple: once a large upload starts, throughput should stay pinned near the real limits of the server's network, storage, and CPU budget for most of the transfer instead of oscillating wildly under load.

- **This project was shaped by the opposite experience**  
  In the author's personal experience, some other self-hosted file cloud setups can fluctuate badly during large uploads and, at times, even collapse to a few hundred KB/s on native desktop clients. Cotton exists partly as a reaction to that: stable near-ceiling throughput matters more than pretty peak numbers for the first few seconds.

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
  Cotton periodically re-checks stored data in the background (batch-by-batch) against the real storage backend. If a disk starts failing and real file chunks go missing or become unreadable, affected users get explicit notifications so operators can react immediately instead of discovering silent data loss later.

- **Background jobs are built into normal operation**  
  Preview generation, manifest hashing, token cleanup, temp cleanup, performance collection, MIME fixes, and storage consistency checks are all part of the system rather than manual maintenance scripts.

- **Maintenance work is load-aware**  
  Cotton tracks active upload activity and quiet-hour windows, and uses that signal to skip, delay, or pace heavier jobs so background work does not sabotage foreground transfers.

- **Operator-facing details are treated explicitly**  
  Setup captures storage mode, email mode, and timezone up front, because things like notifications, disk activity, and retention behavior should be deliberate.

Cotton's current reclaim model is already cautious and restore-friendly. More advanced generational compaction is on the roadmap, but the shipped behavior is already designed around "do not reclaim first and ask questions later."

---

## Small Details That Matter

- Share links can be invalidated in bulk, expire automatically, and be cleaned up in the background.
- Public sharing is backed by real share pages and previews, not just raw opaque URLs.
- Browsers that support the Web Share API get native sharing; everyone else gets a predictable clipboard fallback.
- Preview extraction includes practical media details that quietly improve daily use: embedded cover art from audio tracks (including MP3) and attached cover art from containers like MKV when present.
- Audio playback supports time-synced lyrics (karaoke-style) from a sidecar `.lrc` file located next to the track.
- Search is tuned for responsiveness in normal workflows: debounced client queries with normalized key matching on the server keep lookup behavior fast and predictable.
- WebDAV token reset is immediate in practice: auth cache versioning invalidates old token paths quickly, and failed WebDAV token attempts trigger account notifications.
- User preferences changed in one active client are propagated to other active clients in near real time.
- Password reset, email verification, and email delivery modes are built into setup: use your own SMTP or use Cotton Cloud mail if you do not want to run mail infrastructure (cloud mode requires internet access and telemetry enabled).
- Notifications cover real account and storage events, including failed logins, login success, TOTP lockouts, WebDAV token resets, and shared-file downloads.
- The first-run experience is a guided setup wizard with safe defaults and expert paths instead of a half-documented config scavenger hunt.

Cotton UX is intentional down to small interactions: it is normal here to spend serious design time reducing two buttons to one when that produces a cleaner, more obvious flow.

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

## Database Backup & Auto-Restore

- **Backups are first-class and storage-native**  
  Cotton periodically creates PostgreSQL dumps, chunks them, and stores backup artifacts in Cotton's own storage pipeline (with manifest + pointer metadata for latest backup discovery).

- **Manual trigger exists for operators**  
  Admins can trigger the backup job on demand from the server API when they need an immediate checkpoint.

- **Auto-restore for empty instances is built in**  
  If `COTTON_RESTORE_DATABASE_IF_EMPTY=true` is set, Cotton checks at startup whether the database is empty, finds the latest backup manifest, rebuilds the dump from stored chunks, verifies hash/size integrity, and restores automatically.

- **Recovery is visible, not silent**  
  After auto-restore, Cotton ensures required PostgreSQL extensions and sends high-priority admin notifications with backup metadata.

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

- `CompressionProcessor` — Zstd (streaming) via a managed C# implementation (no external native code).
- `CryptoProcessor` — streaming AES-GCM encrypt/decrypt.
- Storage backend — persists chunk blobs, such as `FileSystemStorageBackend` or `S3StorageBackend`.

Writes flow forward through processors, reads flow backward. In practice, ordering is controlled by processor `Priority`, so writes run **compression -> crypto -> backend**, and reads run **backend -> crypto -> decompression**.

That is why Cotton can do compression, encryption, range reads, previews, and chunk reuse as one coherent storage engine instead of a stack of loosely-related afterthoughts.

---

## Cryptography & Performance

Crypto is powered by **EasyExtensions.Crypto** (NuGet), a streaming AES-GCM engine (`AesGcmStreamCipher`) with:

The encryption core was conceived and built specifically for Cotton as the first architectural step. The broader storage pipeline was pushed forward only after throughput validation showed crypto could carry the workload without becoming the bottleneck.

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

**Session management with actionable telemetry**  
Session inspection groups refresh-token activity by session and surfaces practical metadata (device, IP, location, current-session flag, effective session duration), so users can revoke exactly the session they do not trust instead of forcing a global logout.

**SignalR hub with resilient client behavior**  
Realtime updates are delivered through a dedicated event hub, with client reconnect strategy and transport fallback to keep file/tree/preview/notification flows in sync under imperfect network conditions.

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
Zstandard (zstd) via `ZstdSharp` — a managed C# implementation with no external native code, streaming-enabled by default and integrated into the storage pipeline. Compression is applied _before_ encryption (so it is effective); default compression level is `2` (`CompressionProcessor.CompressionLevel`). Dedup still works with encryption enabled because chunk/content identity is handled by the content-addressed model rather than by inspecting encrypted payload semantics. In practice this typically stays comfortably above multi‑gigabit networking speeds, so compression doesn’t turn into the bottleneck. The processor is implemented as a streaming `Pipe`/`CompressionStream` (no full-file temp files) and is registered via DI; ordering is controlled via processor `Priority`. Tests and benchmarks exercise compressible vs random data.  
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
Auto-applies EF migrations on startup. First admin creation has time-limited window (no eternal backdoor). If `COTTON_RESTORE_DATABASE_IF_EMPTY=true` is set and the DB is empty, startup also attempts automatic restore from the latest backup manifest in storage.  
_See: `src/Cotton.Server/Program.cs`, `src/Cotton.Server/Controllers/AuthController.cs`, `src/Cotton.Server/Controllers/ServerController.cs`, `src/Cotton.Server/Services/DatabaseAutoRestoreService.cs`_

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

<p>
  <img src="https://raw.githubusercontent.com/dotnet/brand/refs/heads/main/logo/dotnet-logo.svg" alt=".NET" width="14" />
  <strong>Built with .NET</strong>
</p>
