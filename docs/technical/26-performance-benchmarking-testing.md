# 26. Performance and Testing

Cotton separates correctness tests, repeatable benchmarks, and live telemetry. A fast test is not a benchmark, and a synthetic benchmark is not proof of production capacity.

## Correctness suites

The .NET solution contains focused unit and integration projects for cryptography, storage, previews, validators, and the server. Server integration tests use the real application composition and PostgreSQL where database behavior, migrations, concurrency, or SQL translation matters.

The frontend uses Vitest and Testing Library for models, API modules, hooks, and user-visible component behavior.

The standard local verification commands are:

```text
dotnet build src/Cotton.sln
dotnet test src/Cotton.sln --no-build

cd src/cotton.client
npm run lint
npm run test
npm run i18n:check
npm run build
```

Do not run `--no-build` tests after a failed or skipped build and treat their result as current; they may execute stale assemblies.

Tests requiring external binaries, S3, Docker, or platform-specific facilities must state and detect those prerequisites. A missing optional tool is an environment failure or an explicit skip, not evidence that the application behavior passed.

## Integration-test isolation

Tests that use encrypted storage isolate both database and storage state. Fixtures with different master keys must not share encrypted sentinel or object evidence.

Concurrency tests exercise the actual boundary they claim to protect. For example, quota tests use parallel mutations across different layouts, and authentication limiter tests issue concurrent failures rather than asserting only sequential counters.

Integration cleanup removes created databases, storage objects, processes, listeners, and temporary files even when an assertion fails.

## Performance harness

`Cotton.Benchmark` is a standalone production-path harness for storage-related scenarios. It measures operations such as SHA-256 hashing, compression, encryption, filesystem I/O, and the combined storage pipeline.

Profiles control payload size, warmup, measured iterations, encryption concurrency, and cipher chunk size. Results include throughput, latency distribution, managed allocation, working set, environment fingerprint, and source revision.

Reviewed baselines are hardware-specific. A result from one CPU, storage device, runtime, or power profile must not be used as a regression gate for materially different hardware.

The benchmark command supports listing scenarios, selecting a profile, filtering scenarios, storing scratch results, updating an intentional baseline, and comparing with the matching reviewed result. Baseline updates are reviewable product changes, not an automatic way to make a regression pass.

## Live storage probe

When telemetry and the selected reporting mode permit it, the server may run a small synthetic round trip through the configured storage pipeline and report aggregate performance. The probe uses bounded data, cleans up its object, and does not contain user content.

Live telemetry is useful for detecting deployment degradation but is not a load test. It must not contend materially with normal uploads.

## Performance review rules

- Measure the complete path affected by the change, not only a convenient helper.
- Use asynchronous storage and hashing APIs for remote or slow streams.
- Keep test payload, warmup, iteration count, hardware, runtime, and configuration in the result.
- Compare medians or distributions in addition to one peak throughput value.
- Investigate correctness and allocation regressions even when raw throughput improves.
- Do not weaken encryption, integrity, cancellation, or final consistency checks to improve a benchmark.

## CI expectations

The release pipeline restores dependencies, builds and tests the .NET solution, validates locales, builds the frontend, builds the SDK, and only then publishes branch-appropriate artifacts. A failed quality gate prevents publication.

Development pipelines may be optimized for turnaround only through an explicit repository policy; local documentation must describe the checks that actually run rather than assuming a shortened branch path.

## Related sections

See [Solution Boundaries and Build](02-solution-layout.md), [Storage Pipeline and Backends](06-storage-pipeline.md), and [Background Jobs](15-background-jobs.md).
