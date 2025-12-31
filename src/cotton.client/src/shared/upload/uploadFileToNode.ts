import type { Guid } from "../api/layoutsApi";
import { chunksApi } from "../api/chunksApi";
import { filesApi } from "../api/filesApi";
import { uploadConfig } from "./config";
import { createIncrementalHasher, hashBytes, toWebCryptoAlgorithm } from "./hash/hashing";

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

  let completedBytes = 0;
  const report = () => {
    options.onProgress?.(completedBytes);
  };

  // Compute whole-file hash in a single sequential pass while we iterate chunks.
  // Uploads (exists/uploadChunk) still run concurrently via a small in-flight pool.
  const fileHasher = await createIncrementalHasher(algorithm);

  const inFlight = new Set<Promise<void>>();
  const waitForSlot = async () => {
    while (inFlight.size >= concurrency) {
      await Promise.race(inFlight);
    }
  };

  const startUpload = (p: Promise<void>) => {
    inFlight.add(p);
    p.finally(() => inFlight.delete(p));
  };

  for (let index = 0; index < chunkCount; index += 1) {
    const start = index * chunkSize;
    const end = Math.min(file.size, start + chunkSize);
    const chunk = file.slice(start, end);
    const chunkBytes = end - start;

    // Read this server-sized chunk ONCE and feed both hash computations.
    const buffer = await chunk.arrayBuffer();
    const bytes = new Uint8Array(buffer);

    fileHasher.update(bytes);
    const chunkHash = await hashBytes(bytes, algorithm);
    chunkHashesByIndex[index] = chunkHash;

    await waitForSlot();

    startUpload(
      (async () => {
        if (sendChunkHashForValidation) {
          const exists = await chunksApi.exists(chunkHash);
          if (!exists) {
            await chunksApi.uploadChunk({ blob: chunk, fileName: file.name, hash: chunkHash });
          }
        } else {
          // Server should compute its own hash and skip validation.
          await chunksApi.uploadChunk({ blob: chunk, fileName: file.name, hash: null });
        }

        completedBytes += chunkBytes;
        report();
      })(),
    );
  }

  await Promise.all(inFlight);

  const fileHash = fileHasher.digestHex();

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
