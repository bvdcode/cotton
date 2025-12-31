import type { Guid } from "../api/layoutsApi";
import { chunksApi } from "../api/chunksApi";
import { filesApi } from "../api/filesApi";
import { uploadConfig } from "./config";
import { hashBlob, hashFile, toWebCryptoAlgorithm } from "./hash/hashing";

export interface UploadServerParams {
  maxChunkSizeBytes: number;
  supportedHashAlgorithm: string;
}

export interface UploadFileToNodeOptions {
  // When false, we omit the chunk hash field on upload (server sees null).
  sendChunkHashForValidation?: boolean;

  // Parallel chunk uploads per file.
  concurrency?: number;
}

export interface UploadFileToNodeCallbacks {
  onProgress?: (bytesUploaded: number) => void;
  onFinalizing?: () => void;
}

export async function uploadFileToNode(options: {
  file: File;
  nodeId: Guid;
  server: UploadServerParams;
  client?: UploadFileToNodeOptions;
  onProgress?: UploadFileToNodeCallbacks["onProgress"];
  onFinalizing?: UploadFileToNodeCallbacks["onFinalizing"];
}): Promise<void> {
  const { file, nodeId, server } = options;

  const sendChunkHashForValidation =
    options.client?.sendChunkHashForValidation ?? uploadConfig.sendChunkHashForValidation;

  const concurrency =
    Math.max(1, options.client?.concurrency ?? uploadConfig.chunkUploadConcurrency);

  const chunkSize = Math.max(1, server.maxChunkSizeBytes);
  const algorithm = toWebCryptoAlgorithm(server.supportedHashAlgorithm);

  const chunkCount = Math.ceil(file.size / chunkSize);
  const chunkHashesByIndex: string[] = new Array(chunkCount);

  // Completion order (for diagnostics / if server ever needs it).
  const completedChunkHashes: string[] = [];

  let completedBytes = 0;
  const report = () => {
    options.onProgress?.(completedBytes);
  };

  let nextIndex = 0;

  const worker = async () => {
    while (true) {
      const index = nextIndex;
      nextIndex += 1;
      if (index >= chunkCount) return;

      const start = index * chunkSize;
      const end = Math.min(file.size, start + chunkSize);
      const chunk = file.slice(start, end);
      const chunkBytes = end - start;

      const chunkHash = await hashBlob(chunk, algorithm);
      chunkHashesByIndex[index] = chunkHash;

      if (sendChunkHashForValidation) {
        const exists = await chunksApi.exists(chunkHash);
        if (!exists) {
          await chunksApi.uploadChunk({ blob: chunk, fileName: file.name, hash: chunkHash });
        }
      } else {
        // Server should compute its own hash and skip validation.
        await chunksApi.uploadChunk({ blob: chunk, fileName: file.name, hash: null });
      }

      completedChunkHashes.push(chunkHash);
      completedBytes += chunkBytes;
      report();
    }
  };

  await Promise.all(Array.from({ length: concurrency }, () => worker()));

  const fileHash = await hashFile(file, algorithm);

  options.onFinalizing?.();

  await filesApi.createFromChunks({
    nodeId,
    chunkHashes: chunkHashesByIndex,
    name: file.name,
    contentType: file.type && file.type.length > 0 ? file.type : "application/octet-stream",
    hash: fileHash,
    originalNodeFileId: null,
  });
}
