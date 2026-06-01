# Cotton Cloud — Technical Documentation

This is the canonical engineering documentation for **Cotton Cloud** — a self-hosted, encrypted, content-addressed file cloud. It is a living technical reference for two audiences: **contributors** who modify the code, and **operators** who deploy and run it. Every section is written against the actual source in this repository (the marketing-toned root `README.md` is treated as a secondary source — where code and README disagree, the code wins and the docs say so).

## What Cotton Cloud Is, in one paragraph

Cotton stores user files as **content-addressed, Zstd-compressed, AES-GCM-encrypted chunks**. A file is split into chunks keyed by the SHA-256 hash of their plaintext; each chunk flows through a streaming storage pipeline (compress → encrypt → backend) and the visible file is reconstructed on demand from an ordered **manifest** of chunk hashes. Borrowing git's model, Cotton separates **content** ("what" a file is — immutable, deduplicated, encrypted) from **layout** ("where" it appears — the user-visible folder tree). The backend is a single .NET / ASP.NET Core runtime over PostgreSQL (EF Core), Quartz background jobs, and SignalR; the frontend is a React/TypeScript/Vite SPA. Everything cryptographic bottoms out at one root **master key**, from which the storage key, password pepper, database-integrity signing key, and backup scoping are deterministically derived.

## Technology stack

| Layer | Technology |
|-------|-----------|
| Backend runtime | .NET 10 / ASP.NET Core (`src/Cotton.Server`) |
| Persistence | EF Core + PostgreSQL/Npgsql (`src/Cotton.Database`) |
| Application logic | EasyExtensions.Mediator (commands/queries), Mapster |
| Background jobs | Quartz (EasyExtensions.Quartz `JobTrigger`) |
| Realtime | SignalR (`EventHub`) |
| Storage engine | `src/Cotton.Storage` (pipeline + processors + backends) |
| Cryptography | `src/Cotton.Crypto` (streaming AES-GCM) |
| Compression | Zstd via ZstdSharp |
| Previews/media | `src/Cotton.Previews` (FFmpeg, Docnet/MuPDF, f3d, …) |
| Frontend | React 19 + Vite, MUI 7, TanStack Query, Zustand, react-router |

## Table of contents

**Foundations**
- [01. System Overview & Design Philosophy](01-system-overview.md)
- [02. Solution Layout, Projects & Build](02-solution-layout.md)
- [03. Data Model & Persistence (EF Core)](03-data-model.md)

**Storage core**
- [04. Content-Addressed Storage: Chunks, Manifests & Deduplication](04-content-addressed-storage.md)
- [05. Logical Filesystem: Layouts, Nodes & Topology](05-logical-filesystem.md)
- [06. Storage Pipeline & Backends](06-storage-pipeline.md)
- [07. Cryptography Engine: Streaming AES-GCM](07-cryptography-engine.md)
- [08. Master Key, Autoconfig & Unlock Bootstrap](08-master-key-bootstrap.md)

**Content lifecycle**
- [09. Upload & File Lifecycle (Chunk-First Protocol)](09-upload-file-lifecycle.md)
- [10. Garbage Collection & Storage Consistency](10-garbage-collection.md)
- [11. Sharing, Versioning, Trash, Archives & Quotas](11-sharing-versioning-trash-archives-quotas.md)

**Application & API**
- [12. HTTP API & Application (Mediator) Layer](12-http-api-mediator.md)
- [13. Authentication, Sessions & Password Security](13-authentication-sessions.md)
- [14. Passkeys (WebAuthn) & OIDC SSO](14-passkeys-oidc.md)
- [15. Background Jobs & Scheduling](15-background-jobs.md)
- [16. Real-time Events, Notifications & Email](16-realtime-notifications-email.md)
- [17. WebDAV Interface](17-webdav.md)

**Media & search**
- [18. Previews & Media Processing](18-previews-media.md)
- [19. Search](19-search.md)

**Integrity, security & backup**
- [20. Database Integrity & Tamper Evidence](20-database-integrity.md)
- [21. Database Backup & Auto-Restore](21-database-backup-restore.md)
- [22. Security Hardening, Diagnostics & Validation](22-security-hardening.md)

**Frontend**
- [23. Frontend: Architecture, State & API Layer](23-frontend-architecture.md)
- [24. Frontend: Features & Upload Pipeline](24-frontend-features-upload.md)

**Operations & quality**
- [25. Configuration, Settings & Server Startup](25-configuration-startup.md)
- [26. Performance, Benchmarking & Testing](26-performance-benchmarking-testing.md)
- [27. Deployment & Operations Guide](27-deployment-operations.md)

**Reference**
- [28. Glossary](28-glossary.md)

## Where to start

- **New contributor:** 01 → 03 → 04 → 06 → 07, then the area you'll touch.
- **Operator / SRE:** [27 Deployment & Operations Guide](27-deployment-operations.md) → [25 Configuration, Settings & Server Startup](25-configuration-startup.md) → [08 Master Key](08-master-key-bootstrap.md) → [21 Database Backup & Auto-Restore](21-database-backup-restore.md).
- **Security reviewer:** [07 Cryptography Engine](07-cryptography-engine.md) → [08 Master Key](08-master-key-bootstrap.md) → [20 Database Integrity](20-database-integrity.md) → [22 Security Hardening](22-security-hardening.md) → [13](13-authentication-sessions.md)/[14](14-passkeys-oidc.md) Auth.
- **Frontend developer:** [23](23-frontend-architecture.md) → [24](24-frontend-features-upload.md), with [12 (HTTP API)](12-http-api-mediator.md) as the contract.

## Conventions in these docs

- Source files are cited as backticked repo-relative paths, e.g. `src/Cotton.Server/Controllers/ChunkController.cs`.
- Identifiers (classes, methods, enums, routes, config keys) are spelled exactly as in code.
- Diagrams use Mermaid. Tables enumerate endpoints, settings, enums, and entity fields.
- Cross-references name the target section by title (italicized).

---

*This documentation was generated from a multi-agent study of the codebase: every section was drafted by reading the source, then independently fact-checked against the code a second time. The same content set is intended for publication to the Cotton Cloud Wiki once the wiki connector has write access.*
