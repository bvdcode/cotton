> ⚠️ **The Project Status: Pre-Alpha**

<div align="center">

# Cotton Cloud — a real cloud that doesn’t suck

![status-prealpha](https://img.shields.io/badge/status-pre--alpha-red)
[![CI](https://github.com/bvdcode/cotton/actions/workflows/docker-image.yml/badge.svg)](https://github.com/bvdcode/cotton/actions/workflows/docker-image.yml)
[![Release](https://img.shields.io/github/v/release/bvdcode/cotton?sort=semver)](https://github.com/bvdcode/cotton/releases)
[![Docker Pulls](https://img.shields.io/docker/pulls/bvdcode/cotton)](https://hub.docker.com/r/bvdcode/cotton)
[![Image Size](https://img.shields.io/docker/image-size/bvdcode/cotton/latest)](https://hub.docker.com/r/bvdcode/cotton/tags)
[![License](https://img.shields.io/github/license/bvdcode/cotton)](LICENSE)
[![CodeFactor](https://www.codefactor.io/repository/github/bvdcode/cotton/badge)](https://www.codefactor.io/repository/github/bvdcode/cotton)

**Files. Snapshots. Search. Stream. Restore.**  
One container. Two faces: simple up top, full control under the hood.  
Built with **.NET / C#** because performance and sanity matter.

**100% pure C# - zero native deps. Zero P/Invoke. All managed.**

</div>

---

## TL;DR

- ⚡ Instant snapshots and restore - flip a pointer, not terabytes. “As it was yesterday at 19:03” in ~seconds.
- 🧠 Real search - single indexer for docs, images, audio, video. Names, content, speech, EXIF. All of it.
- 🎬 Media that works - originals preserved, background transcode to HLS/DASH, chunked streaming, optional speech-to-text.
- 🔐 Encryption by default - per-object keys, modern AEAD, share and restore without leaking guts.
- 🧩 Plugins, not bloat - hard isolation, quotas, signed releases. No messenger or CRM junk.
- 🧰 Zero-bullshit install - one container. First launch benchmarks hardware → picks **Minimal / Standard / Full** profile.

---

## Why Cotton

- S3-first storage with hot local cache
- Content-addressable chunks with GC and retention graph
- One-click migration from Nextcloud and other clouds
- White-label instances and SSO integrations ready for business
- Clean APIs and SDKs for C# and TypeScript

---

## 🔧 Pure C#

**All .NET. All managed. No native binaries. No excuses.**

- No native deps - no P/Invoke - no platform SDK glue
- Identical behavior on Linux, Windows, macOS
- Container friendly - deterministic builds and CI


---

## Why this exists

There isn’t a single open-source “cloud” you can **run in minutes** that then **stays fast**, **indexes everything**, **streams video right**, and **restores instantly** without becoming a plugin junkyard. COTTON is a rebuttal to that.

---

## What it is (and isn’t)

**Is**

* A **file cloud** with first-class **snapshots**, **search**, **streaming**, and **encryption**.
* A **single-container** service that self-profiles your machine and configures itself.
* A **.NET-powered** engine with predictable performance and sane ergonomics.

**Isn’t**

* A messenger, a CRM, a wiki, or a kitchen sink.
* A plugin bazaar that lets anything poke your data or eat your CPU.

---

## Core pillars

1. **State over files**: files matter, but **state (snapshots)** is the product.
2. **Immediate restore**: snapshot “promotion” is atomic; no mass copy/rename storms.
3. **Minimum ceremony**: install, pick template, done.
4. **Performance honesty**: visible P95s, predictable memory, visible GC/retention.
5. **Isolation by default**: plugins run **outside** the gateway with strict capabilities.

---

## Features (vibe check)

### Snapshots & instant restore

* Immutable Merkle-style trees; versions never mutate in place.
* Restore = **switch root** to snapshot ID (atomic).
* Million-file trees? Restore time ≈ switching pointers + cache warmup.
* Retention policies (hours/days/weeks/months).
* Shared, content-addressed chunks mean dedupe is automatic.

### Storage & chunks

* Content-addressed **chunks** (e.g., BLAKE3) \~4–8 MiB (content-defined later).
* Originals preserved; index/derivatives stored separately.
* FS backend first-class; S3-compatible storage **optional** for backup/export.

### Encryption (default-on)

* Per-object CEKs (XChaCha20-Poly1305 / AES-GCM), envelope-wrapped by per-user KEK.
* No plaintext indexing leakage; index carries only what you allow.
* Key rotation without rewriting whole objects.

### The “Big Indexer”

* One module that does the boring glue **for you**:

  * **Docs** → text (PDF/Office/HTML/Markdown/…);
  * **Images** → EXIF, OCR;
  * **Audio/Video** → **Whisper-grade** transcripts + segmentation;
  * **Vectors** for semantic search (multi-lang embeddings).
* Toggle sub-modules (docs/images/audio/video) per template/profile.

### Search

* Hybrid: BM25 + vector similarity.
* Query across filenames, text content, speech, EXIF, tags.
* Snippets in results; jump-to-time in videos from transcript hits.

### Video pipeline

* Background transcode to HLS/DASH; chunked streaming; adaptive bitrates.
* Original kept; stream served from transcodes.
* Optional subtitle generation from transcripts.

### First-run experience

* Hardware probe (CPU, RAM, disk, IO) → suggests **Minimal / Standard / Full**.
* **Templates** by role:

  * **Family Photos**: photos + video streaming + face/search basics.
  * **Power User**: full indexer, transcripts, OCR, vectors, snapshots & retention.
* “Under the hood” switch to expose every knob.

### WebDAV (beta compatibility)

* Enough of WebDAV for real clients: `OPTIONS`, `PROPFIND` (Depth 0/1/∞), `MKCOL`, `PUT`, `GET`, `DELETE`, `COPY`, `MOVE`, `HEAD`.
* Correct ETag/If-Match/Range; property caching to keep clients snappy.
* **Own REST** for real features (resumable uploads, deltas, shares, snapshots, search).

### Plugins (without the landfill)

* **Out-of-process** PluginHost (or OCI container) → no code in gateway’s address space.
* **Capabilities**: `ReadFiles`, `WriteExternal:S3`, `NeedsNetwork:Egress`, `HighCPU`, … granted per install.
* **Quotas**: CPU/mem/IO/time; **kill policies** on abuse.
* **Signed** releases only; marketplace tracks fail-rates, resource usage, complaints.
* **Backup to S3** ships as a reference plugin; others can be NuGet/OCI.

### Storage / GC panel (visible truth)

* Retention **timeline** (Recent/Warm/Cold/Expired) with **forecast**: “in 3 days frees \~128 GB”.
* Snapshot list with **unique vs shared** sizes.
* Dry-run GC: show deltas before deletion.
* Pin files/snapshots to keep; explain why something is retained.
* Audit log: who/what/when removed or pinned.

---

## Architecture (high level)

```
[ Clients ]  →  [ Gateway (.NET Kestrel): WebDAV + REST + Auth ]
                    │
                    ├─ [ Metadata (PostgreSQL): items / versions / chunks / snapshots / jobs / audit ]
                    ├─ [ Blobstore: FS (primary), S3-compatible (optional) ]
                    ├─ [ Worker: background jobs / schedules / queues ]
                    └─ [ Plugin Orchestrator → PluginHost processes/containers (capabilities, quotas) ]
```

* **Auth**: OIDC/OAuth2 (external provider or built-in minimal issuer).
* **Jobs**: deadlines + budgets; backoff; quarantine on repeated violations.
* **API**: WebDAV for compatibility; REST/gRPC internally for speed/features.

---

## Personas & templates

* **“I’m a parent; photos first.”** → Family Photos template: photos/video, simple sharing, basic search, snapshots; heavy indexers off.
* **“I’m a power user / small team.”** → Everything on: docs, OCR, vectors, A/V transcripts, aggressive snapshots & retention tuning.
* Switch personas anytime; “advanced mode” exposes every setting.

---

## Install (placeholder, first public draft)

```bash
docker run -d --name cotton \
  -p 8080:8080 \
  -v /cotton/data:/var/lib/cotton \
  -v /cotton/config:/etc/cotton \
  ghcr.io/your-org/cotton:latest
# Open http://localhost:8080 → first-run wizard (profile + template)
```

Or `docker-compose.yml` with `cotton`, optional `postgres`, optional `cotton-pluginhost`.

---

## Performance targets (what we hold ourselves to)

* **Restore promotion**: O(1) snapshot root switch; UI visible in < 1–5 s on 1M items.
* **Listing**: P95 `PROPFIND Depth:1` < 150 ms on warm cache.
* **Search**: content hit (BM25) < 300 ms; vector fallback < 700 ms typical.
* **Transcode**: background, throttled by profile; streaming starts < 2 s on local network.

(These are targets, not promises; instrumentation will be visible.)

---

## Telemetry & privacy

* Local, transparent, **opt-out** switch.
* Aggregated only: error rates, resource spikes, plugin health.
* No content or identifiers exfiltrated. Ever.

---

## Security notes

* Encryption by default; keys scoped and rotated.
* Plugins **never** see your KEKs; secrets are per-plugin namespace.
* Egress off by default; allow-list per plugin (e.g., only S3 endpoint).
* Audit every sensitive action.

---

## License / branding

* **License**: **TBD** (AGPL-class likely) to keep derivatives open while allowing commercial licensing.
* **Name & marks**: “COTTON” branding reserved; forks use their own names and marks.

---

## Roadmap & timeline (ambitious; subject to reality)

### 2025 — Ignition

* Repo live; one-container launch; FS storage.
* WebDAV beta; resumable uploads; stable ETags.
* Snapshots v0; first-run hardware probe → **Minimal/Standard/Full**.

### 2026 — Spine

* Indexer v0 (docs → text, BM25); video streaming v0 (HLS/DASH).
* Vectors + transcripts (Whisper-grade); Family/Power templates.
* Encryption default-on; snapshots v1 with retention policies.
* Big Indexer modularization.

### 2027 — Ecosystem

* PluginHost isolation + quotas + kill policies.
* Signed marketplace with health metrics & ratings; opt-out telemetry.
* Real one-click first-run wizard.
* 5–10 public references with honest metrics.

### 2028 — Proof

* Public, reproducible benchmarks; LTS 1.x line.
* “Restore as of yesterday 19:03” on large sets in minutes-scale UX.
* PWA client with offline cache.

### 2029 — Mass & polish

* NAS/mini-PC partner builds (“home memory box”).
* 15–30 high-quality plugins; landfill blocked by policy.
* Smooth migrations; zero-downtime upgrades.

### 2030 — The show

* Live demo: instant snapshot promotion on a huge tree; search inside old videos; adaptive streaming; box \~400 USD.
* Name recognized as the **.NET self-host cloud** that “just works”.

---

## Non-goals

* No messenger, no CRM, no social feed.
* No “app zoo” inside the core.
* No magic sync that corrupts state; deltas are explicit.

---

## FAQ (short)

* **Is WebDAV the main API?** No. It’s a beta-compat layer. The real surface is REST (and internal gRPC).
* **Can I run it without Docker?** Technically yes; the path is not curated right now.
* **Does it rely on S3?** No. FS is primary. S3 is optional for backup/export.

---

## Status

Early. Repo exists. Manifest locked: **store → index → stream → snapshot → restore**.
Everything else is scaffolding around that spine.

---

*Built by someone who got tired of “clouds” that lag, thrash, and pretend search is a filename filter.*
