export const uploadConfig = {
  // If true, the client probes chunk ownership before uploading. The raw upload
  // endpoint still receives the hash so the server can validate bytes on ingest.
  sendChunkHashForValidation: true,

  // Fixed network consumer pool for prepared chunks inside one file.
  maxChunkUploadConcurrency: 8,

  // Native WebCrypto chunk digests run concurrently. Ordered batch copies feed
  // the whole-file hash worker while original buffers enter the network queue.
  chunkHashConcurrency: 4,

  // Backpressure for chunks waiting between hashing and network consumers.
  maxPreparedChunkCount: 8,
  maxPreparedChunkBytes: 64 * 1024 * 1024,

  // If a chunk upload is interrupted by transport/network failure, retry that
  // byte range with smaller chunks down to this floor.
  minAdaptiveChunkSizeBytes: 128 * 1024,

  // Upload at most 4 files in parallel. The manager still starts from one file
  // and opens more lanes only after uploads prove they benefit from it.
  maxConcurrentFileUploads: 4,

  // A completed transfer faster than this is likely latency/overhead-bound,
  // so opening another lane usually improves throughput.
  concurrencyRampUpMs: 1200,

  // If the first active file is large but visibly moving, cautiously open one
  // more file lane so queued small files are not stuck behind it.
  fileHeadOfLineProbeMs: 1500,

  // UI refresh throttling for upload progress callbacks.
  progressEmitIntervalMs: 100,
} as const;
