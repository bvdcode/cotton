# 02. Solution Boundaries and Build

Cotton is a modular monolith: one deployable server composes focused libraries and a separately built browser application.

## Production modules

| Module | Responsibility |
| --- | --- |
| Cotton.Server | Application composition, HTTP API, authentication, WebDAV, jobs, realtime events, and SPA hosting. |
| Cotton.Database | EF Core model, PostgreSQL persistence, migrations, and integrity shadow-column configuration. |
| Cotton.Storage | Streaming pipeline, processors, backend contracts, filesystem storage, and S3-compatible storage. |
| Cotton.Crypto | Streaming authenticated encryption and key derivation. |
| Cotton.Topology | Logical layout traversal and path resolution. |
| Cotton.Previews | Preview generation for supported media and document formats. |
| Cotton.Validators | Shared validation and normalized-name rules. |
| Cotton.Autoconfig | Master-key startup and browser-unlock bootstrap. |
| Cotton.Localization | Server-side notification text. |
| Cotton.Shared | Cross-module models, routes, constants, and contracts reused by the server and SDK. |
| Cotton.Sdk | Typed HTTP and SignalR client packaged for external .NET consumers. |
| cotton.client | React/TypeScript browser application. |

Dependencies flow toward lower-level contracts. Storage may use cryptography; topology may use the database model; the server may compose every module. Lower-level libraries must not depend on server concerns.

## Build contracts

Backend and SDK projects target the repository's configured .NET SDK.

Typical verification:

```powershell
dotnet restore src/Cotton.sln
dotnet build src/Cotton.sln --configuration Release --no-restore
dotnet test src/Cotton.sln --configuration Release --no-build
```

Frontend verification runs independently:

```powershell
Set-Location src/cotton.client
npm ci
npm run build
npm run test
npm run lint
npm run i18n:check
```

Some preview tests require external tools such as FFmpeg or f3d. Missing optional executables are environment failures, not evidence that application behavior is broken; CI and release gates must state which external tools they provide.

## Build and packaging boundaries

- The server build consumes the compiled frontend assets for production images.
- Database migrations are produced and owned by the database project but applied by the server startup path.
- The shared SDK is packaged separately from the application image.
- Benchmark and diagnostic projects are not part of the production runtime.
- Test projects may use real PostgreSQL and external storage endpoints; their prerequisites must be explicit.

## Continuous integration expectations

A release-quality pipeline should:

1. restore dependencies from locked project definitions;
2. build backend and frontend in release mode;
3. run unit, integration, lint, localization, and formatting checks;
4. build the container from the same revision;
5. publish packages and images only after verification succeeds;
6. retain test results and enough metadata to identify the built revision.

Development pipelines may use a faster subset, but they must still compile both languages and enforce lint/type safety. Expensive integration and external-tool suites can run in a later gate rather than being silently removed.

## Repository-wide conventions

- Nullable reference types and asynchronous I/O are expected throughout backend code.
- Application behavior belongs behind mediator requests rather than controllers.
- Entity relationships use restrictive deletion; lifecycle code performs explicit cleanup.
- Frontend state uses query/store abstractions rather than browser local storage.
- Persisted formats and migrations are compatibility contracts and require dedicated review.

## Related sections

- [Data model and persistence](03-data-model.md)
- [HTTP API and mediator](12-http-api-mediator.md)
- [Performance and testing](26-performance-benchmarking-testing.md)
- [Deployment and operations](27-deployment-operations.md)
