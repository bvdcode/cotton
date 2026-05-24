# Cotton.Benchmark

Performance benchmarking tool for the Cotton Cloud storage engine.

## Overview

Cotton.Benchmark measures Cotton Cloud storage performance across compression, encryption, hashing, filesystem I/O, and pipeline scenarios.

## Architecture

The application follows SOLID principles and object-oriented design:

### Core Abstractions (`Abstractions/`)
- **IBenchmark** - Contract for individual benchmarks
- **IBenchmarkResult** - Represents benchmark execution results
- **IBenchmarkRunner** - Orchestrates benchmark execution
- **IReporter** - Handles result reporting
- **IResultFormatter** - Formats results for display

### Domain Models (`Models/`)
- **BenchmarkResult** - Concrete result implementation with success/failure factories
- **BenchmarkConfiguration** - Configurable benchmark parameters
- **PerformanceMetrics** - Performance measurement data with throughput and memory observations

### Benchmarks (`Benchmarks/`)
- **BenchmarkBase** - Abstract base with warmup/measurement logic
- **MemoryStreamBenchmark** - Baseline memory stream operations
- **HashingBenchmark** - SHA-256 hashing for content addressing
- **ChunkUploadProcessingBenchmark** - combined server write-path processing: SHA-256, buffering, compression, and encryption
- **CompressionBenchmark** - `Cotton.Storage.Processors.CompressionProcessor` with compressible text
- **DecompressionBenchmark** - Zstd decompression with compressible text
- **EncryptionBenchmark** - `Cotton.Storage.Processors.CryptoProcessor` with AES-GCM streaming
- **DecryptionBenchmark** - AES-GCM decryption with streaming
- **FileSystemBenchmark** - `Cotton.Storage.Backends.FileSystemStorageBackend` disk I/O
- **PipelineBenchmark** - `Cotton.Storage.Pipelines.FileStoragePipeline` full-cycle test

### Infrastructure (`Infrastructure/`)
- **BenchmarkRunner** - Main orchestration engine with logging
- **MemoryMonitor** - Memory usage tracking
- **SystemInfo** - System information reporting
- **TestDataGenerator** - Deterministic test data generation:
  - `GenerateCompressibleText()` - Log-like text data (highly compressible)
  - `GenerateJsonData()` - JSON documents (moderately compressible)
  - `GenerateMixedData()` - Semi-compressible realistic file content
  - `GenerateRandomBinary()` - Pure random data (incompressible)

### Reporting (`Reporting/`)
- **TableResultFormatter** - Formats results as ASCII tables
- **SummaryResultFormatter** - Compact summary format
- **ConsoleReporter** - Outputs to console with color support

## Usage

### Basic Usage
```bash
dotnet run --project Cotton.Benchmark --configuration Release
```

### Command Line Options
```bash
dotnet run --project Cotton.Benchmark -- --help
dotnet run --project Cotton.Benchmark --configuration Release
dotnet run --project Cotton.Benchmark -- --mode machine --profile quick --no-update-baseline
dotnet run --project Cotton.Benchmark -- --mode machine --profile standard --compare
```

Run the image preview memory capacity probe:

```bash
dotnet run --project Cotton.Benchmark -- --mode development --profile quick --scenario image-preview
```

### Regression Baselines

Reviewed baselines live under `performance/baselines`. A full non-compare run updates the reviewed baseline by default; use `--no-update-baseline` to save only an unreviewed result. Unreviewed run output is written to `performance/results` and ignored by git. Baselines are scoped by hardware key, benchmark mode, and profile so local machines can track their own history without pretending GitHub runner timings are stable.

Modes:

- `machine` runs portable benchmarks without PostgreSQL. The `quick` profile uses relaxed regression gates because it has few iterations; `standard` and `full` keep stricter gates for reviewed regression checks. The expensive Zstd -5..22 sweep is excluded from normal suites and should be run explicitly only when investigating compression-level tradeoffs.
- `development` is the local Cotton regression suite. It includes filesystem, storage-pipeline, and image-preview memory capacity scenarios; PostgreSQL-backed flows belong there next.

Run the expensive Zstd extreme-level sweep only on purpose:

```bash
dotnet run --project Cotton.Benchmark -- --mode machine --profile quick --scenario extreme
```


## Default Configuration

- **Data Size**: **100 MB** (realistic workload)
- **Warmup Iterations**: **3**
- **Measured Iterations**: **10** (statistical validity)
- **Encryption Threads**: **2**
- **Cipher Chunk Size**: 1 MB
- **Compression Level**: `CompressionProcessor.DefaultCompressionLevel` (Zstd)
- **Encryption Key**: 256-bit AES

## Benchmark Scope

### 1. Production Code Paths
- Uses `CompressionProcessor` from `Cotton.Storage.Processors`
- Uses `CryptoProcessor` with `AesGcmStreamCipher` from `Cotton.Crypto`
- Includes combined chunk-upload processing scenarios for compressible text, mixed content, and random binary data
- Uses `FileSystemStorageBackend` from `Cotton.Storage.Backends`
- Uses `FileStoragePipeline` from `Cotton.Storage.Pipelines`

### 2. Deterministic Test Data
- **Compressible Text**: Log-like patterns (compression benchmark)
- **JSON Data**: Structured documents (pipeline benchmark)
- **Mixed Data**: Semi-compressible file content (upload processing benchmark)
- **Random Binary**: Already-compressed media/archive-like content (upload processing benchmark)
- Avoids zero-filled arrays and trivial patterns

### 3. Large Workloads
- **100 MB** data size (not tiny inputs)
- **10 measured iterations** for statistical significance
- **3 warmup iterations** to eliminate JIT effects

### 4. Filesystem I/O
- FileSystemBenchmark performs file system write/read/delete cycles
- Measures configured storage path performance
- Exercises the storage backend lifecycle

## Sample Output

```
==================================================================

           Cotton Cloud - Performance Benchmark Suite

  Local machine benchmarks and Cotton regression baselines


==================================================================

System Information:
  • OS:          Windows 11 ...
  • Framework:   .NET 10.0.0
  • Architecture: X64
  • Processors:  16
  • Memory:      32.50 GB

Configuration:
  • Data Size:           100.00 MB
  • Warmup Iterations:   3
  • Measured Iterations: 10
  • Encryption Threads:  2
  • Cipher Chunk Size:   1.00 MB
  • Compression Level:   <DefaultCompressionLevel>
  • Encryption Key Size: 256 bits

======================================================================
                    BENCHMARK RESULTS SUMMARY
======================================================================

----------------------------------------------------------------
| Cotton.Storage Zstd Compression   | Status                      |
----------------------------------------------------------------
| Result                               | SUCCESS                     |
| AvgThroughput                        | 2100.45 MB/s                |
| Processor                            | CompressionProcessor        |
| DataType                             | Compressible Text (Logs)    |
...

Total Benchmarks:  11
Successful:        11
Failed:            0
Total Time:        9.50 sec

Memory Statistics:
  • Current Usage:  950.25 MB
  • GC Collections: Gen0: 45, Gen1: 35, Gen2: 28
```

## Design Principles

### SOLID Compliance
- **Single Responsibility**: Each class has one reason to change
- **Open/Closed**: Extensible through abstractions
- **Liskov Substitution**: Implementations are substitutable
- **Interface Segregation**: Focused, minimal interfaces
- **Dependency Inversion**: Depends on abstractions, not concretions

### Patterns Used
- **Template Method** - BenchmarkBase provides execution flow
- **Strategy** - Different formatters and reporters
- **Factory Method** - BenchmarkResult static factories
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection

## Extension Points

### Adding New Benchmarks
1. Inherit from `BenchmarkBase`
2. Implement `ExecuteIterationAsync` and `MeasureIterationAsync`
3. Use components from Cotton.Storage
4. Register in `Program.CreateBenchmarks()`

### Adding New Test Data Patterns
1. Add method to `TestDataGenerator`
2. Use in benchmark constructor
3. Document compression characteristics

## Dependencies

- **Microsoft.Extensions.DependencyInjection** - IoC container
- **Microsoft.Extensions.Logging** - Structured logging
- **ZstdSharp.Port 0.8.6** - Zstd compression
- **Cotton.Storage** - Storage pipeline components
  - Includes `CompressionProcessor`, `CryptoProcessor`
  - Includes `FileSystemStorageBackend`, `FileStoragePipeline`
  - Includes `Cotton.Crypto` for `AesGcmStreamCipher`

## Performance Considerations

- **Warmup iterations** prevent JIT compilation effects
- **10 measured iterations** provide statistical reliability
- **Release build** mandatory for accurate measurements
- **Large data sizes** (100 MB) minimize measurement overhead
- Production code paths keep measurements representative
- **Compressible data** provides realistic compression ratios

## Benchmark Design Constraints

| Aspect | This Benchmark | Ad-hoc microbenchmarks |
|--------|---------------|----------------------|
| Components | production code | Mock/stub implementations |
| Data Size | **100 MB** per test | 1-10 MB |
| Test Data | Deterministic compressible patterns | Zero-filled or sequential |
| Iterations | 10 measured + 3 warmup | 1-3 total |
| Disk I/O | file system operations | In-memory only |
| Pipeline | multi-processor chain | Single operations |

## Future Enhancements

- [x] Command-line mode, profile, and scenario filtering
- [x] JSON result and baseline output
- [x] Comparative benchmarking across runs
- [x] Percentile calculations (P50, P95)
- [x] Managed allocation, working set, and peak working set metrics
- [x] Image preview memory capacity benchmark with isolated worker process
- [ ] GC pause time tracking
- [ ] S3StorageBackend benchmarks
- [ ] Concurrent operation benchmarks
- [ ] CPU/memory profiling integration

## License

SPDX-License-Identifier: MIT
Copyright (c) 2025 Vadim Belov <https://belov.us>
