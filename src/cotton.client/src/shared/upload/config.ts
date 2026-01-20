export const uploadConfig = {
  // If false: the client omits the "hash" field on chunk upload (server sees null).
  // Chunk hashes are still computed client-side because the final "from-chunks" request
  // requires an ordered list of chunk hashes.
  sendChunkHashForValidation: true,

  // Upload at most 4 chunks in parallel for better throughput.
  maxChunkUploadConcurrency: 4,
} as const;
