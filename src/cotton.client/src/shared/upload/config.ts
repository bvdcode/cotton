export const uploadConfig = {
  // If false: the client omits the "hash" field on chunk upload (server sees null).
  // Chunk hashes are still computed client-side because the final "from-chunks" request
  // requires an ordered list of chunk hashes.
  sendChunkHashForValidation: true,

  // Upload at most 4 chunks in parallel inside one large file.
  maxChunkUploadConcurrency: 4,

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
