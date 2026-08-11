# 12. HTTP API and Mediator Layer

The HTTP layer translates transport concerns into application requests. Controllers own routing, authentication metadata, request binding, response headers, and status-code mapping. New business mutations and queries belong in mediator handlers or focused domain components. A small number of older controller actions still access persistence directly; they are migration debt, not the pattern to extend.

## Route families

Versioned JSON APIs use the `/api/v1` prefix. The principal families are:

- `/auth` for credentials, sessions, passkeys, and OIDC;
- `/chunks` and `/files` for content transfer and file operations;
- `/layouts` for filesystem topology, navigation, search, and folder shares;
- `/archives` for ticketed multi-item downloads;
- `/notifications` for persisted user notifications;
- `/preview` for derived media;
- `/settings` and `/server` for configuration and administration;
- `/users` for user and profile operations;
- `/webdav` for the WebDAV protocol surface.

Stable route constants shared with clients should be preferred over duplicated strings. This document intentionally does not enumerate every action; current route declarations and typed client contracts remain the endpoint-level authority.

## Request flow

A normal request passes through these boundaries:

1. ASP.NET Core establishes the trusted client address, authentication principal, rate-limit policy, and request cancellation token.
2. The controller validates transport-specific input and creates a typed mediator request.
3. The handler loads only the domain state it needs and enforces ownership, integrity, concurrency, and business invariants.
4. The handler commits the mutation and returns a typed result.
5. The controller maps that result to the public HTTP contract and emits real-time events only after success.

Controllers must not open an alternative application path merely to avoid defining a handler. Shared behavior belongs in a focused service only when it is infrastructure or a reusable domain capability rather than an individual use case.

## Identity and authorization

Authenticated handlers receive the user identifier from the validated principal, never from an untrusted body field. Administrative operations use role authorization in addition to normal authentication. Anonymous routes rely on purpose-specific credentials such as share or archive tokens and still constrain every lookup to the token's authority.

Trusted proxy processing occurs before IP-based policies. Application code consumes the normalized client address and must not read raw forwarding headers independently.

## Response conventions

- Successful collection queries return typed payloads.
- Paged endpoints place the total result count in `X-Total-Count`; SDK paged methods treat the header as required.
- Validation errors return `400` with a stable error body.
- Missing or inaccessible resources return `404` without disclosing cross-user existence.
- Name and optimistic-concurrency conflicts return `409`.
- Authentication and authorization failures return `401` and `403` respectively.
- Quota exhaustion uses the response appropriate to the transport; WebDAV maps it to insufficient storage.
- Abuse limits return `429` with `Retry-After`.

Unhandled failures are logged by the server boundary and do not expose secrets, stack traces, or database details to the client.

## Cancellation and streaming

Request cancellation must flow through mediator handlers, EF Core, storage, hashing, encryption, and external processes. Streaming endpoints avoid loading complete files or archives in memory and must not dispose a response-owned stream before ASP.NET Core finishes writing it.

## Concurrency

Different operations use different concurrency boundaries:

- optimistic tokens protect client-visible edits;
- per-layout gates serialize topology mutations that must observe one coherent tree;
- per-user quota gates serialize final quota checks and reference commits;
- content-addressed uniqueness converges duplicate chunk and manifest work;
- job-level single-flight prevents overlapping maintenance runs.

No controller-local lock should invent a second policy for a domain operation already coordinated elsewhere.

## Client contracts

Public DTOs, route shapes, header requirements, and status meanings are compatibility surfaces. Private handler names, file organization, and dependency-injection registration are implementation details and may change without requiring API documentation updates.

## Related sections

See [Authentication and Sessions](13-authentication-sessions.md), [Passkeys and OIDC](14-passkeys-oidc.md), [Sharing, Versions, Trash, Archives, and Quotas](11-sharing-versioning-trash-archives-quotas.md), and [Frontend Architecture](23-frontend-architecture.md).
