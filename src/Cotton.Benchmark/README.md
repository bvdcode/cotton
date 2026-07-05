# Cotton.Benchmark

Storage-path benchmark harness for Cotton published results.

## One-Command Result

Run the repository script on the machine being measured.

Linux/macOS:

```bash
./performance/run-benchmark-baseline.sh
```

Windows PowerShell:

```powershell
.\performance\run-benchmark-baseline.ps1
```

The script runs the standard storage-path benchmark set, writes one reviewed compact JSON file under `performance/results/`, stages that file, and creates a git commit.

## Benchmark Set

The storage-path set measures the write and read sides separately:

- write stages: SHA-256 hashing, Zstd compression, AES-GCM encryption, filesystem write
- write full path: chunk upload processing with SHA-256, buffering, compression, and encryption
- read stages: filesystem read, AES-GCM decryption, Zstd decompression
- read full path: backend read, decryption, and decompression

The compact JSON artifact in `performance/results/` is the stable file intended for published benchmark tables. Full timestamped JSON is only written for scratch runs.

## Configuration

The script uses the `standard` profile:

- 100 MiB data size
- 3 warmup iterations
- 10 measured iterations
- 2 encryption threads
- 1 MiB cipher chunks
- default Cotton Zstd compression level
- 256-bit AES key

Reviewed result files are written to `performance/results/`, which is tracked. Scratch runs created with `--no-update-baseline` are written to `.temp/benchmark-results/`, which is ignored by git.
