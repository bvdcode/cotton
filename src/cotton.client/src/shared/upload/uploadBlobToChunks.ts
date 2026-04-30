import { chunksApi } from "../api/chunksApi";
import { AdaptiveConcurrencyController } from "./AdaptiveConcurrencyController";
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

  const chunkUploadConcurrency = new AdaptiveConcurrencyController({
    maxConcurrency: Math.max(
      1,
      options.client?.concurrency ?? uploadConfig.maxChunkUploadConcurrency,
    ),
    rampUpDurationMs: uploadConfig.concurrencyRampUpMs,
  });

  const chunkSize = Math.max(1, server.maxChunkSizeBytes);
  const algorithm = toWebCryptoAlgorithm(server.supportedHashAlgorithm);

  const chunkCount = Math.ceil(blob.size / chunkSize);
  const chunkHashesByIndex: string[] = new Array(chunkCount);

  let completedBytes = 0;
  let lastReportedBytes = 0;
  const inFlightBytesByIndex = new Map<number, number>();

  const report = () => {
    if (!options.onProgress) {
      return;
    }

    let inFlightBytes = 0;
    for (const bytes of inFlightBytesByIndex.values()) {
      inFlightBytes += bytes;
    }

    const nextBytes = Math.min(blob.size, completedBytes + inFlightBytes);
    if (nextBytes < lastReportedBytes) {
      return;
    }

    lastReportedBytes = nextBytes;
    options.onProgress(nextBytes);
  };

  let worker: HashWorkerClient | null = null;
  let blobHasher: Awaited<ReturnType<typeof createIncrementalHasher>> | null = null;
  const abortController = new AbortController();
  let firstUploadError: unknown = null;

  if (canUseHashWorker()) {
    worker = await globalHashWorkerPool.acquire(algorithm);
  } else {
    blobHasher = await createIncrementalHasher(algorithm);
  }

  try {
    const inFlight = new Set<Promise<void>>();

    const waitForSlot = async () => {
      while (inFlight.size >= chunkUploadConcurrency.current) {
        await Promise.race(inFlight);
      }
    };

    const waitForAllUploads = async () => {
      while (inFlight.size > 0) {
        await Promise.race(inFlight);
      }
    };

    const setInFlightProgress = (index: number, bytesUploaded: number) => {
      const previous = inFlightBytesByIndex.get(index) ?? 0;
      inFlightBytesByIndex.set(index, Math.max(previous, bytesUploaded));
      report();
    };

    const startUpload = (
      index: number,
      chunk: Blob,
      chunkHash: string,
      chunkBytes: number,
    ) => {
      const startedAt = performance.now();

      const uploadPromise = (async () => {
        try {
          if (sendChunkHashForValidation) {
            const exists = await chunksApi.exists(chunkHash, abortController.signal);
            if (!exists) {
              await chunksApi.uploadChunk({
                blob: chunk,
                fileName,
                hash: chunkHash,
                signal: abortController.signal,
                onProgress: (bytesUploaded) => {
                  setInFlightProgress(index, bytesUploaded);
                },
              });
            }
          } else {
            await chunksApi.uploadChunk({
              blob: chunk,
              fileName,
              hash: null,
              signal: abortController.signal,
              onProgress: (bytesUploaded) => {
                setInFlightProgress(index, bytesUploaded);
              },
            });
          }

          inFlightBytesByIndex.delete(index);
          completedBytes += chunkBytes;
          report();
          chunkUploadConcurrency.observe({
            bytes: chunkBytes,
            durationMs: performance.now() - startedAt,
            succeeded: true,
          });
        } catch (error) {
          firstUploadError ??= error;
          abortController.abort();
          inFlightBytesByIndex.delete(index);
          chunkUploadConcurrency.observe({
            bytes: chunkBytes,
            durationMs: performance.now() - startedAt,
            succeeded: false,
          });
        }
      })();

      inFlight.add(uploadPromise);
      uploadPromise.finally(() => {
        inFlight.delete(uploadPromise);
      });
    };

    for (let index = 0; index < chunkCount && !firstUploadError; index += 1) {
      const start = index * chunkSize;
      const end = Math.min(blob.size, start + chunkSize);
      const chunk = blob.slice(start, end);
      const chunkBytes = end - start;

      const buffer = await chunk.arrayBuffer();

      let chunkHash: string;
      if (worker) {
        chunkHash = await worker.hashChunk(buffer);
      } else {
        const bytes = new Uint8Array(buffer);
        blobHasher!.update(bytes);
        chunkHash = await hashBytes(bytes, algorithm);
      }
      chunkHashesByIndex[index] = chunkHash;

      if (firstUploadError) {
        break;
      }

      await waitForSlot();
      if (firstUploadError) {
        break;
      }

      startUpload(index, chunk, chunkHash, chunkBytes);
    }

    await waitForAllUploads();

    if (firstUploadError) {
      throw firstUploadError;
    }

    const fileHash = worker ? await worker.digestFile() : blobHasher!.digestHex();

    return { chunkHashes: chunkHashesByIndex, fileHash };
  } finally {
    if (worker) {
      globalHashWorkerPool.release(worker);
    }
  }
}
