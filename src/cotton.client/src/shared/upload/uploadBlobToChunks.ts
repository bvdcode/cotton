import { ChunkHashSession } from "./ChunkHashSession";
import { ChunkUploadPipeline } from "./ChunkUploadPipeline";
import { uploadConfig } from "./config";
import { toWebCryptoAlgorithm } from "./hash/hashing";
import type {
  UploadFileToNodeOptions,
  UploadProgressSnapshot,
  UploadServerParams,
} from "./types";

export async function uploadBlobToChunks(options: {
  blob: Blob;
  fileName: string;
  server: UploadServerParams;
  client?: UploadFileToNodeOptions;
  onProgress?: (
    bytesUploaded: number,
    snapshot?: UploadProgressSnapshot,
  ) => void;
}): Promise<{ chunkHashes: string[]; fileHash: string }> {
  const algorithm = toWebCryptoAlgorithm(options.server.supportedHashAlgorithm);
  const chunkSizeBytes = Math.max(1, options.server.maxChunkSizeBytes);
  const hashSession = await ChunkHashSession.create(
    options.blob,
    algorithm,
    uploadConfig.chunkHashConcurrency,
  );

  try {
    const pipeline = new ChunkUploadPipeline({
      blob: options.blob,
      fileName: options.fileName,
      hashSession,
      hashPreparationConcurrency: hashSession.maxParallelPreparations,
      sendChunkHashForValidation:
        options.client?.sendChunkHashForValidation ??
        uploadConfig.sendChunkHashForValidation,
      chunkSizeBytes,
      minRetryChunkSizeBytes: Math.max(
        1,
        Math.min(uploadConfig.minAdaptiveChunkSizeBytes, chunkSizeBytes),
      ),
      uploadConcurrency: Math.max(
        1,
        options.client?.concurrency ?? uploadConfig.maxChunkUploadConcurrency,
      ),
      maxQueuedChunks: uploadConfig.maxPreparedChunkCount,
      maxQueuedBytes: uploadConfig.maxPreparedChunkBytes,
      onProgress: options.onProgress,
    });

    return await pipeline.run();
  } finally {
    await hashSession.release();
  }
}
