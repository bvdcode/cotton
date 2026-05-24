# Cotton Performance Baselines

Cotton keeps reviewed performance baselines in git, but raw benchmark runs stay local.

## Modes

- `machine` runs portable benchmarks without PostgreSQL. The `quick` profile uses relaxed regression gates because it has few iterations; `standard` and `full` keep stricter gates for reviewed regression checks. The expensive Zstd -5..22 sweep is excluded from normal suites and should be run explicitly only when investigating compression-level tradeoffs. Use it to compare raw machine capability across CPUs, runtimes, and disks.
- `development` is the local Cotton regression suite. It includes filesystem, storage-pipeline, and image-preview memory capacity scenarios; PostgreSQL-backed listing, upload, download, WebDAV, archive, and integrity scenarios should be added next.

## Common Commands

List scenarios:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --mode machine --list
```

Run a quick local check:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --mode machine --profile quick
```

Create or refresh the reviewed baseline for the current hardware key with defaults:

```bash
dotnet run --project src/Cotton.Benchmark -c Release
```

Run the expensive Zstd extreme-level sweep only on purpose:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --mode machine --profile quick --scenario extreme
```

Explicit equivalent of the default baseline command:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --mode machine --profile standard --update-baseline
```

Compare a run against the committed baseline for the current hardware key:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --mode machine --profile standard --compare
```

## Artifact Policy

- `performance/baselines/` is tracked. Commit only reviewed baselines.
- `performance/results/` is ignored. It contains raw run output and local noise.
- Compare only runs with the same hardware key, mode, and profile.

## Development Regression Roadmap

- Folder listing with 1k, 10k, and 50k files.
- Direct download with 1, 100, and 10k chunks.
- Archive ticket creation and stored ZIP streaming.
- WebDAV `PROPFIND` and `GET`.
- Upload many small files.
- Upload one large chunked file.
- Database integrity backfill and read-boundary verification.
- [x] Image preview memory capacity: quick=4096x3072, standard=10000x10000, full=20000x10000 BMP sources.
- PostgreSQL-backed preview queue throughput and realtime notification scenarios.
