# 07. Cryptography Engine

Cotton encrypts storage streams with AES-256-GCM using a random per-object data key wrapped by the configured master encryption key. Content is processed in independently authenticated chunks through a bounded asynchronous pipeline.

## Security properties

- AES-256-GCM with 16-byte authentication tags.
- A fresh random 32-byte data key for every encrypted stream.
- A 12-byte nonce composed from a random 4-byte stream prefix and an 8-byte chunk index.
- Metadata bound through GCM associated data.
- An authenticated zero-length terminator that detects truncation and appended data.
- Bounded channels and pooled buffers with backpressure.
- Sensitive key and buffer material cleared when ownership ends.

Nonce counters never wrap. Reusing a nonce under the same data key is prevented by rejecting index overflow.

## CTN2 stream format

Every encrypted stream is:

```text
[file header][chunk header + ciphertext]...[authenticated terminator header]
```

Integers are little-endian. The only accepted current magic is `CTN2`.

### File header

The current file header is 84 bytes.

| Offset | Size | Field |
| ---: | ---: | --- |
| 0 | 4 | Magic `CTN2` |
| 4 | 4 | Header length |
| 8 | 8 | Total plaintext length, or `0` when unavailable |
| 16 | 4 | Key identifier |
| 20 | 4 | Random nonce prefix |
| 24 | 12 | Nonce used to wrap the data key |
| 36 | 16 | Authentication tag for the wrapped data key |
| 52 | 32 | Encrypted data key |

The wrapped-key associated data binds the magic, header length, total length, key identifier, nonce prefix, and wrapping nonce.

### Chunk header

Each chunk header is 36 bytes, followed by ciphertext with the same length as its plaintext.

| Offset | Size | Field |
| ---: | ---: | --- |
| 0 | 4 | Magic `CTN2` |
| 4 | 4 | Header length |
| 8 | 8 | Plaintext length; `0` identifies the terminator |
| 16 | 4 | Key identifier |
| 20 | 16 | Authentication tag |

Chunk associated data binds format version, key identifier, chunk index, and plaintext length. A valid chunk cannot be moved to another position or assigned another length without authentication failure.

The terminator authenticates an empty plaintext at the next chunk index. EOF before it, or bytes after it, are corruption.

## Compatibility

CTN1 is not accepted by the current runtime. Attempting to read it produces a dedicated compatibility error directing the operator to complete the transition on Cotton 0.4.35 before upgrading.

Unknown magic, mixed chunk formats, invalid lengths, key-ID mismatch, and authentication failures remain hard errors. The decryptor does not guess formats or repair streams.

## Processing model

Encryption reads bounded plaintext chunks, encrypts them in parallel, and writes results in original order. Decryption authenticates framed ciphertext in parallel and similarly restores order before exposing plaintext.

Backpressure limits in-flight work. Cancellation or failure in any stage cancels the complete pipeline, observes worker completion, recycles owned buffers, and rethrows the original error.

The stream API respects caller ownership flags for input and output streams. Internal storage paths use asynchronous reads, writes, copies, and hashing.

## Key derivation

Domain-specific subkeys use HKDF-SHA256. Purpose strings are part of the derivation contract and prevent one derived key from being substituted for another purpose.

Changing a purpose string, master key, key identifier, header layout, AAD layout, or nonce construction changes a persisted cryptographic contract and requires an explicit migration or compatibility plan.

## Failure classification

- Invalid framing or lengths: data corruption.
- GCM tag mismatch: authentication failure.
- CTN1 magic: known unsupported legacy format.
- Missing terminator or trailing bytes: incomplete or appended stream.
- Cancellation: operation cancellation, not corruption.
- Backend transport failure: storage failure, not an invalid master key by itself.

## Performance

Chunk size and worker count trade memory for throughput. Larger chunks reduce framing overhead but increase the memory held by each in-flight operation. Bounded buffer ownership is a correctness requirement, not merely an optimization.

Benchmark numbers are hardware-specific and must include CPU, memory, chunk size, worker count, runtime version, and whether storage I/O was involved.

## Related sections

- [Master-key bootstrap](08-master-key-bootstrap.md)
- [Storage pipeline](06-storage-pipeline.md)
- [Performance and testing](26-performance-benchmarking-testing.md)
