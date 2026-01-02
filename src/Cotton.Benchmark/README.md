# Cotton.Benchmark

Professional performance benchmarking tool for Cotton Cloud storage engine.

## Overview

Cotton.Benchmark is a comprehensive console application designed to measure and report performance metrics of Cotton Cloud's **REAL** storage components: compression, encryption, hashing, disk I/O, and the complete storage pipeline.

**All benchmarks use REAL production code from Cotton.Storage**, not mock implementations.

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
- **PerformanceMetrics** - Performance measurement data with throughput calculations

### Benchmarks (`Benchmarks/`)
- **BenchmarkBase** - Abstract base with warmup/measurement logic
- **MemoryStreamBenchmark** - Baseline memory stream operations
- **HashingBenchmark** - SHA-256 hashing for content addressing
- **CompressionBenchmark** - **REAL** `Cotton.Storage.Processors.CompressionProcessor` with compressible text
- **DecompressionBenchmark** - **REAL** Zstd decompression with compressible text
- **EncryptionBenchmark** - **REAL** `Cotton.Storage.Processors.CryptoProcessor` with AES-GCM streaming
- **DecryptionBenchmark** - **REAL** AES-GCM decryption with streaming
- **FileSystemBenchmark** - **REAL** `Cotton.Storage.Backends.FileSystemStorageBackend` disk I/O
- **PipelineBenchmark** - **REAL** `Cotton.Storage.Pipelines.FileStoragePipeline` full cycle test

### Infrastructure (`Infrastructure/`)
- **BenchmarkRunner** - Main orchestration engine with logging
- **MemoryMonitor** - Memory usage tracking
- **SystemInfo** - System information reporting
- **TestDataGenerator** - Realistic test data generation:
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
```

## Default Configuration

- **Data Size**: **100 MB** (realistic workload)
- **Warmup Iterations**: **3**
- **Measured Iterations**: **10** (statistical validity)
- **Encryption Threads**: **2**
- **Cipher Chunk Size**: 1 MB
- **Compression Level**: 3 (Zstd)
- **Encryption Key**: 256-bit AES

## What Makes This Benchmark Real

### 1. Real Components
- Uses **actual** `CompressionProcessor` from `Cotton.Storage.Processors`
- Uses **actual** `CryptoProcessor` with `AesGcmStreamCipher` from `EasyExtensions.Crypto`
- Uses **actual** `FileSystemStorageBackend` from `Cotton.Storage.Backends`
- Uses **actual** `FileStoragePipeline` from `Cotton.Storage.Pipelines`

### 2. Realistic Test Data
- **Compressible Text**: Log-like patterns (compression benchmark)
- **JSON Data**: Structured documents (pipeline benchmark)
- **Mixed Data**: Semi-compressible file content (encryption benchmark)
- **NOT** zero-filled arrays or trivial patterns

### 3. Large Workloads
- **100 MB** data size (not 10 MB toys)
- **10 measured iterations** for statistical significance
- **3 warmup iterations** to eliminate JIT effects

### 4. Real Disk I/O
- FileSystemBenchmark tests **actual** file system write/read/delete cycles
- Measures real storage path performance
- Tests the complete storage backend lifecycle

## Sample Output

```
==================================================================
                                                                  
           Cotton Cloud - Performance Benchmark Suite            
                                                                  
  Testing cloud storage pipeline: compression, encryption,       
  hashing, and full cycle performance                            
                                                                  
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
  • Compression Level:   3
  • Encryption Key Size: 256 bits

======================================================================
                    BENCHMARK RESULTS SUMMARY                   
======================================================================

----------------------------------------------------------------
| Compression (Real Zstd Processor)   | Status                      |
----------------------------------------------------------------
| Result                               | SUCCESS                     |
| AvgThroughput                        | 2100.45 MB/s                |
| Processor                            | CompressionProcessor        |
| DataType                             | Compressible Text (Logs)    |
...

Total Benchmarks:  8
Successful:        8
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
3. Use **REAL** components from Cotton.Storage
4. Register in `Program.CreateBenchmarks()`

### Adding New Test Data Patterns
1. Add method to `TestDataGenerator`
2. Use in benchmark constructor
3. Document compression characteristics

## Dependencies

- **Microsoft.Extensions.DependencyInjection** - IoC container
- **Microsoft.Extensions.Logging** - Structured logging
- **ZstdSharp.Port 0.8.6** - Zstd compression (REAL)
- **Cotton.Storage** - Storage pipeline components (REAL)
  - Includes `CompressionProcessor`, `CryptoProcessor`
  - Includes `FileSystemStorageBackend`, `FileStoragePipeline`
  - Includes `EasyExtensions.Crypto` for `AesGcmStreamCipher`

## Performance Considerations

- **Warmup iterations** prevent JIT compilation effects
- **10 measured iterations** provide statistical reliability
- **Release build** mandatory for accurate measurements
- **Large data sizes** (100 MB) minimize measurement overhead
- **Real components** ensure production-representative results
- **Compressible data** provides realistic compression ratios

## Key Differences from Toy Benchmarks

| Aspect | This Benchmark | Typical Toy Benchmarks |
|--------|---------------|----------------------|
| Components | **REAL** production code | Mock/stub implementations |
| Data Size | **100 MB** per test | 1-10 MB |
| Test Data | Realistic compressible patterns | Zero-filled or sequential |
| Iterations | 10 measured + 3 warmup | 1-3 total |
| Disk I/O | **REAL** file system operations | In-memory only |
| Pipeline | **REAL** multi-processor chain | Single operations |

## Future Enhancements

- [ ] Command-line parameter parsing for configuration
- [ ] JSON/CSV output formats
- [ ] Comparative benchmarking across runs
- [ ] GC pause time tracking
- [ ] Percentile calculations (P50, P95, P99)
- [ ] S3StorageBackend benchmarks
- [ ] Concurrent operation benchmarks
- [ ] CPU/memory profiling integration

## License

SPDX-License-Identifier: MIT
Copyright (c) 2025 Vadim Belov <https://belov.us>
