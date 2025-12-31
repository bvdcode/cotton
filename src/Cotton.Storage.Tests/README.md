# Cotton.Storage Tests

Comprehensive test suite for Cotton.Storage components according to the detailed testing requirements.

## Test Structure

### Helpers Tests
- **StorageKeyHelperTests.cs** - Tests for UID normalization and segmentation
  - Trim and lowercase validation
  - Minimum length enforcement (6 chars)
  - Invalid character detection
  - Idempotency checks
  - Segment splitting (p1, p2, fileName)

### Streams Tests
- **ConcatenatedReadStreamTests.cs** - Tests for stream concatenation
  - Multi-stream concatenation
  - Empty stream handling
  - Small and large buffer reads
  - Boundary crossing validation
  - Error propagation
  - Dispose/DisposeAsync behavior

### Processors Tests
- **CompressionProcessorTests.cs** - Tests for Zstd compression
  - Round-trip tests (empty, 1 byte, 1KB, 1MB, random data)
  - Stream position validation
  - Compressible data verification
  - Priority validation

- **CryptoProcessorTests.cs** - Tests for encryption/decryption
  - Round-trip tests (various data sizes)
  - Stream position validation
  - Priority validation
  - Uses mocked IStreamCipher for testing

### Backends Tests
- **FileSystemStorageBackendTests.cs** - Tests for file system storage
  - Write and read operations
  - Delete operations
  - Directory structure creation
  - ReadOnly attribute setting
  - Duplicate UID handling
  - Large file support (10MB)
  - Invalid UID handling
  - Parallel operations

### Pipelines Tests
- **FileStoragePipelineTests.cs** - Tests for processor pipeline
  - Processor ordering (Priority-based)
  - Stream.Null protection
  - Round-trip with multiple processors
  - Empty processor list handling
  - Marker-based processor ordering verification

### Integration Tests
- **IntegrationTests.cs** - End-to-end integration tests
  - FileSystem + Crypto round-trip
  - FileSystem + Compression + Crypto round-trip
  - Multiple files independent operations
  - Large file streaming (5MB)
  - Processor order verification
  - Parallel operations (20 concurrent)

## Test Coverage

All tests follow the detailed requirements specified in the original document:

1. **Helpers**: NormalizeUid and GetSegments validation
2. **Streams**: ConcatenatedReadStream behavior
3. **Processors**: Crypto and Compression round-trips
4. **Backends**: FileSystem atomicity and operations
5. **Pipelines**: Processor ordering and Stream.Null protection
6. **Integration**: Real-world scenarios with multiple components

## Running Tests

```bash
dotnet test Cotton.Storage.Tests
```

## Notes

- Uses NUnit framework
- Mocking with Moq for external dependencies
- File system tests include proper cleanup in TearDown
- All tests follow AAA pattern (Arrange-Act-Assert)
- Integration tests use realistic data sizes balanced for test speed
