# Cotton.Benchmark

Professional performance benchmarking tool for Cotton Cloud storage engine.

## Overview

Cotton.Benchmark is a comprehensive console application designed to measure and report performance metrics of Cotton Cloud's core operations: compression, encryption, hashing, and the complete storage pipeline.

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
- **HashingBenchmark** - SHA-256 hashing for content addressing
- **CompressionBenchmark** - Zstd compression performance
- **DecompressionBenchmark** - Zstd decompression performance
- **EncryptionBenchmark** - AES-GCM encryption with streaming
- **DecryptionBenchmark** - AES-GCM decryption with streaming
- **PipelineBenchmark** - Full cycle: compression ? encryption ? decryption ? decompression

### Infrastructure (`Infrastructure/`)
- **BenchmarkRunner** - Main orchestration engine with logging

### Reporting (`Reporting/`)
- **TableResultFormatter** - Formats results as ASCII tables
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

- **Data Size**: 10 MB
- **Warmup Iterations**: 2
- **Measured Iterations**: 5
- **Encryption Threads**: 2
- **Cipher Chunk Size**: 1 MB
- **Compression Level**: 3 (Zstd)

## Output

The tool produces a comprehensive report with:
- Individual benchmark results in table format
- Performance metrics (throughput in MB/s or GB/s)
- Min/Max/Average statistics
- Success/failure status
- Summary with total execution time

### Sample Output
```
???????????????????????????????????????????????????????????????
                    BENCHMARK RESULTS SUMMARY                   
???????????????????????????????????????????????????????????????

?????????????????????????????????????????????????????????????
? Hashing (SHA-256)                 ? Status                  ?
?????????????????????????????????????????????????????????????
? Result                            ? ? SUCCESS               ?
? AvgThroughput                     ? 2500.45 MB/s            ?
...
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
3. Register in `Program.CreateBenchmarks()`

### Adding New Formatters
1. Implement `IResultFormatter`
2. Register in DI container
3. Inject into reporter

### Adding New Reporters
1. Implement `IReporter`
2. Register in DI container
3. Use in main execution flow

## Dependencies

- **Microsoft.Extensions.DependencyInjection** - IoC container
- **Microsoft.Extensions.Logging** - Structured logging
- **ZstdSharp.Port** - Zstd compression
- **Cotton.Storage** - Storage pipeline components
  - Includes EasyExtensions.Crypto for AES-GCM

## Performance Considerations

- Warmup iterations prevent JIT compilation effects
- Multiple measured iterations provide statistical reliability
- Release build recommended for accurate measurements
- Large data sizes (10 MB default) minimize measurement overhead

## Future Enhancements

- [ ] Command-line parameter parsing for configuration
- [ ] JSON/CSV output formats
- [ ] Comparative benchmarking across runs
- [ ] Memory allocation tracking
- [ ] Percentile calculations (P50, P95, P99)
- [ ] Storage backend benchmarks (FileSystem, S3)
- [ ] Concurrent operation benchmarks

## License

SPDX-License-Identifier: MIT
Copyright (c) 2025 Vadim Belov <https://belov.us>
