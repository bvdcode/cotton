import { chunksApi } from "../api/chunksApi";
import { uploadConfig } from "./config";
import { createIncrementalHasher, hashBytes, toWebCryptoAlgorithm } from "./hash/hashing";
import { canUseHashWorker, HashWorkerClient } from "./hash/hashWorkerClient";
import { globalHashWorkerPool } from "./hash/HashWorkerPool";
import type { UploadFileToNodeOptions, UploadServerParams } from "./types";

export async function uploadBlobToChunks(options: {
  blob: Blob;
  fileName: string;
  server: UploadServerParams;
  client?: UploadFileToNodeOptions;
  onProgress?: (bytesUploaded: number) => void;
}): Promise<{ chunkHashes: string[]; fileHash: string }> {
  const { blob, fileName, server } = options;

  const sendChunkHashForValidation =
    options.client?.sendChunkHashForValidation ?? uploadConfig.sendChunkHashForValidation;

  // Dynamic concurrency: start at 1 chunk to probe throughput, then ramp up to max
  // if the first chunk finishes quickly.
  const maxConcurrency = Math.max(1, options.client?.concurrency ?? uploadConfig.maxChunkUploadConcurrency);
  let concurrency = 1;

  const chunkSize = Math.max(1, server.maxChunkSizeBytes);
  const algorithm = toWebCryptoAlgorithm(server.supportedHashAlgorithm);

  const chunkCount = Math.ceil(blob.size / chunkSize);
  const chunkHashesByIndex: string[] = new Array(chunkCount);

  let completedBytes = 0;
  const report = () => {
    options.onProgress?.(completedBytes);
  };

  // Compute whole-blob hash in a single sequential pass while we iterate chunks.
  // Uploads (exists/uploadChunk) still run concurrently via a small in-flight pool.
  
  // Use worker pool to avoid WASM memory exhaustion
  let worker: HashWorkerClient | null = null;
  let blobHasher: Awaited<ReturnType<typeof createIncrementalHasher>> | null = null;

  if (canUseHashWorker()) {
    worker = await globalHashWorkerPool.acquire(algorithm);
  } else {
    blobHasher = await createIncrementalHasher(algorithm);
  }

  try {
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
      const end = Math.min(blob.size, start + chunkSize);
      const chunk = blob.slice(start, end);
      const chunkBytes = end - start;

      // Read this server-sized chunk ONCE and feed both hash computations.
      const buffer = await chunk.arrayBuffer();

      // Offload hashing to worker when available to keep UI responsive.
      let chunkHash: string;
      if (worker) {
        // Worker handles both blob hash and chunk hash.
        // Buffer is transferred to worker, so create it before transferring.
        chunkHash = await worker.hashChunk(buffer); // transfers buffer
      } else {
        // Non-worker path: update blob hasher and compute chunk hash.
        const bytes = new Uint8Array(buffer);
        blobHasher!.update(bytes);
        chunkHash = await hashBytes(bytes, algorithm);
      }
      chunkHashesByIndex[index] = chunkHash;

      const doUpload = async () => {
        if (sendChunkHashForValidation) {
          const exists = await chunksApi.exists(chunkHash);
          if (!exists) {
            await chunksApi.uploadChunk({ blob: chunk, fileName, hash: chunkHash });
          }
        } else {
          // Server should compute its own hash and skip validation.
          await chunksApi.uploadChunk({ blob: chunk, fileName, hash: null });
        }

        completedBytes += chunkBytes;
        report();
      };

      // Dynamic concurrency probe: upload the first chunk sequentially.
      // If it completes quickly, ramp up to maxConcurrency for the rest.
      if (index === 0) {
        const startedAt = performance.now();
        await doUpload();
        const elapsedMs = performance.now() - startedAt;
        if (elapsedMs < 1000 && maxConcurrency > 1) {
          concurrency = maxConcurrency;
        }
        continue;
      }

      await waitForSlot();
      startUpload(doUpload());
    }

    await Promise.all(inFlight);

    const fileHash = worker ? await worker.digestFile() : blobHasher!.digestHex();

    // Return worker to pool instead of terminating it
    if (worker) {
      globalHashWorkerPool.release(worker);
    }

    return { chunkHashes: chunkHashesByIndex, fileHash };
  } catch (error) {
    // In case of error, still return worker to pool
    if (worker) {
      globalHashWorkerPool.release(worker);
    }
    throw error;
  }
}
