# Cotton Performance Results

Cotton keeps reviewed storage-path benchmark results in git.

## New Machine Result

Run exactly one script on a machine that should publish a reviewed storage-path result.

Linux/macOS:

```bash
./performance/run-benchmark-baseline.sh
```

Windows PowerShell:

```powershell
.\performance\run-benchmark-baseline.ps1
```

The script runs the standard `storage-paths` benchmark set, writes one reviewed compact JSON file under `performance/results/`, stages that file, and creates a git commit.

## Benchmark Set

- `storage-paths` runs the public write/read storage-path set: SHA-256, Zstd compression/decompression, AES-GCM encryption/decryption, filesystem write/read, chunk upload processing, and full read pipeline.

## Direct CLI

List scenarios:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --list
```

Run a quick local check:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --profile quick --no-update-baseline
```

Create or refresh the reviewed result for the current hardware key with defaults:

```bash
dotnet run --project src/Cotton.Benchmark -c Release
```

Explicit equivalent of the default reviewed-result command:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --mode storage-paths --profile standard --update-baseline
```

Compare a run against the committed result for the current hardware key:

```bash
dotnet run --project src/Cotton.Benchmark -c Release -- --mode storage-paths --profile standard --compare
```

## Artifact Policy

- `performance/results/` is tracked and contains one compact JSON file per measured machine.
- Scratch runs created with `--no-update-baseline` go to `.temp/benchmark-results/`.
- Compare only runs with the same hardware key, mode, and profile.
