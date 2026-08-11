# Cotton Cloud Technical Documentation

This directory describes Cotton's architecture and operational contracts. It is intended for contributors, operators, and security reviewers.

## Documentation standard

Technical documentation must explain behavior that remains important when implementation details move:

- public HTTP, storage, cryptographic, and configuration contracts;
- domain concepts and architectural boundaries;
- security and tenancy invariants;
- concurrency, failure, recovery, and cancellation behavior;
- operational requirements and upgrade constraints;
- known limitations and intentional trade-offs.

Avoid duplicating the source tree. In particular, do not document line numbers, commit history, exhaustive file or class inventories, package patch versions, or step-by-step private-method implementations. Those details become stale without helping an operator or contributor make a decision.

Concrete identifiers are appropriate when they are themselves stable contracts: routes, configuration keys, persisted formats, database fields involved in migrations, public DTOs, and deliberate extension interfaces. Source file references should be exceptional rather than the organizing structure of a document.

When code and documentation disagree, treat the current code and migrations as authoritative and update the affected contract documentation in the same change.

## Architecture at a glance

Cotton is a self-hosted file cloud with a .NET backend, PostgreSQL persistence, and a React frontend. Files are split into content-addressed chunks, compressed, encrypted, and written through a storage pipeline to a filesystem or S3-compatible backend. Mutable user-visible paths are stored separately from immutable file content.

The runtime also provides background maintenance through Quartz, realtime updates through SignalR, WebDAV access, media previews, database integrity protection, and backup/restore support. One master key anchors storage encryption and related derived keys.

## Contents

### Foundations

- [01. System overview](01-system-overview.md)
- [02. Solution boundaries and build](02-solution-layout.md)
- [03. Data model and persistence](03-data-model.md)

### Storage core

- [04. Content-addressed storage](04-content-addressed-storage.md)
- [05. Logical filesystem](05-logical-filesystem.md)
- [06. Storage pipeline and backends](06-storage-pipeline.md)
- [07. Cryptography engine](07-cryptography-engine.md)
- [08. Master-key bootstrap](08-master-key-bootstrap.md)

### Content lifecycle

- [09. Upload and file lifecycle](09-upload-file-lifecycle.md)
- [10. Garbage collection](10-garbage-collection.md)
- [11. Sharing, versions, trash, archives, and quotas](11-sharing-versioning-trash-archives-quotas.md)

### Application surfaces

- [12. HTTP API and mediator](12-http-api-mediator.md)
- [13. Authentication and sessions](13-authentication-sessions.md)
- [14. Passkeys and OIDC](14-passkeys-oidc.md)
- [15. Background jobs](15-background-jobs.md)
- [16. Realtime events, notifications, and email](16-realtime-notifications-email.md)
- [17. WebDAV](17-webdav.md)
- [18. Previews and media](18-previews-media.md)
- [19. Search](19-search.md)

### Integrity and operations

- [20. Database integrity](20-database-integrity.md)
- [21. Database backup and restore](21-database-backup-restore.md)
- [22. Security hardening](22-security-hardening.md)
- [23. Frontend architecture](23-frontend-architecture.md)
- [24. Frontend features and upload](24-frontend-features-upload.md)
- [25. Configuration and startup](25-configuration-startup.md)
- [26. Performance and testing](26-performance-benchmarking-testing.md)
- [27. Deployment and operations](27-deployment-operations.md)
- [28. Glossary](28-glossary.md)

## Suggested reading paths

- Contributors: 01 → 03 → the subsystem being changed.
- Operators: 27 → 25 → 08 → 21.
- Security reviewers: 07 → 08 → 20 → 22 → 13 and 14.
- Frontend contributors: 23 → 24 → 12.
