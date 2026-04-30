import { chunksApi } from "../api/chunksApi";
import { isAxiosError } from "../api/httpClient";
import { AdaptiveConcurrencyController } from "./AdaptiveConcurrencyController";
import { uploadConfig } from "./config";
import { createIncrementalHasher, hashBytes, toWebCryptoAlgorithm } from "./hash/hashing";
import { canUseHashWorker, HashWorkerClient } from "./hash/hashWorkerClient";
import { globalHashWorkerPool } from "./hash/HashWorkerPool";
import type { UploadFileToNodeOptions, UploadServerParams } from "./types";

interface ChunkSegment {
  id: number;
  start: number;
  end: number;
  updateFileHash: boolean;
  networkFailures: number;
  availableAt: number;
}

interface UploadedChunkSegment {
  start: number;
  end: number;
  hash: string;
}

const getSegmentLength = (segment: ChunkSegment) => segment.end - segment.start;

const getRetryDelayMs = (networkFailures: number) =>
  Math.min(5000, 250 * 2 ** Math.min(Math.max(0, networkFailures - 1), 4));

const isConnectionInterruption = (error: unknown): boolean => {
  if (!isAxiosError(error)) {
    return false;
  }

  if (error.response) {
    return false;
  }

  const code = (error.code ?? "").toUpperCase();
  if (code === "ERR_CANCELED") {
    return false;
  }

  return (
    code === "ERR_NETWORK" ||
    code === "ECONNABORTED" ||
    code === "ETIMEDOUT" ||
    code === "ERR_NETWORK_CHANGED" ||
    Boolean(error.request)
  );
};

const delay = (ms: number) =>
  new Promise<void>((resolve) => {
    globalThis.setTimeout(resolve, ms);
  });

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

  const initialChunkSize = Math.max(1, server.maxChunkSizeBytes);
  const minChunkSize = Math.max(
    1,
    Math.min(uploadConfig.minAdaptiveChunkSizeBytes, initialChunkSize),
  );
  const algorithm = toWebCryptoAlgorithm(server.supportedHashAlgorithm);

  let activeChunkSize = initialChunkSize;
  let nextSegmentId = 0;
  let nextOffset = 0;
  let nextFileHashOffset = 0;
  let completedBytes = 0;
  let lastReportedBytes = 0;
  let fatalError: unknown = null;

  const pendingSegments: ChunkSegment[] = [];
  const uploadedSegments: UploadedChunkSegment[] = [];
  const inFlightBytesById = new Map<number, number>();
  let fileHashWaiters: Array<() => void> = [];

  const report = () => {
    if (!options.onProgress) {
      return;
    }

    let inFlightBytes = 0;
    for (const bytes of inFlightBytesById.values()) {
      inFlightBytes += bytes;
    }

    const nextBytes = Math.min(blob.size, completedBytes + inFlightBytes);
    if (nextBytes < lastReportedBytes) {
      return;
    }

    lastReportedBytes = nextBytes;
    options.onProgress(nextBytes);
  };

  const makeSegment = (
    start: number,
    end: number,
    updateFileHash: boolean,
    networkFailures = 0,
    availableAt = 0,
  ): ChunkSegment => ({
    id: nextSegmentId++,
    start,
    end,
    updateFileHash,
    networkFailures,
    availableAt,
  });

  const takeReadyPendingSegment = (now: number): ChunkSegment | null => {
    const index = pendingSegments.findIndex((segment) => segment.availableAt <= now);
    if (index < 0) {
      return null;
    }

    return pendingSegments.splice(index, 1)[0];
  };

  const getNextPendingTime = (): number | null => {
    let nextTime: number | null = null;
    for (const segment of pendingSegments) {
      if (nextTime === null || segment.availableAt < nextTime) {
        nextTime = segment.availableAt;
      }
    }
    return nextTime;
  };

  const createNextInitialSegment = (): ChunkSegment | null => {
    if (nextOffset >= blob.size) {
      return null;
    }

    const start = nextOffset;
    const end = Math.min(blob.size, start + activeChunkSize);
    nextOffset = end;
    return makeSegment(start, end, true);
  };

  const reduceChunkSize = () => {
    activeChunkSize = Math.max(minChunkSize, Math.floor(activeChunkSize / 2));
  };

  const queueRetrySegments = (segment: ChunkSegment) => {
    reduceChunkSize();

    const failures = segment.networkFailures + 1;
    const availableAt = Date.now() + getRetryDelayMs(failures);
    const retrySize = Math.min(activeChunkSize, getSegmentLength(segment));

    for (let start = segment.start; start < segment.end; start += retrySize) {
      pendingSegments.push(
        makeSegment(
          start,
          Math.min(segment.end, start + retrySize),
          false,
          failures,
          availableAt,
        ),
      );
    }
  };

  const buildOrderedChunkHashes = () => {
    uploadedSegments.sort((a, b) => a.start - b.start);

    let expectedStart = 0;
    for (const segment of uploadedSegments) {
      if (segment.start !== expectedStart || segment.end <= segment.start) {
        throw new Error("Uploaded chunks do not cover the file contiguously.");
      }
      expectedStart = segment.end;
    }

    if (expectedStart !== blob.size) {
      throw new Error("Uploaded chunks do not cover the complete file.");
    }

    return uploadedSegments.map((segment) => segment.hash);
  };

  const wakeFileHashWaiters = () => {
    const waiters = fileHashWaiters;
    fileHashWaiters = [];
    for (const resolve of waiters) {
      resolve();
    }
  };

  const waitForFileHashTurn = async (segment: ChunkSegment) => {
    while (!fatalError && segment.start !== nextFileHashOffset) {
      await new Promise<void>((resolve) => {
        fileHashWaiters.push(resolve);
      });
    }

    if (fatalError) {
      throw fatalError;
    }
  };

  const advanceFileHashOffset = (segment: ChunkSegment) => {
    nextFileHashOffset = segment.end;
    wakeFileHashWaiters();
  };

  let worker: HashWorkerClient | null = null;
  let blobHasher: Awaited<ReturnType<typeof createIncrementalHasher>> | null = null;
  const abortController = new AbortController();

  const failUpload = (error: unknown) => {
    fatalError ??= error;
    abortController.abort();
    wakeFileHashWaiters();
  };

  if (canUseHashWorker()) {
    worker = await globalHashWorkerPool.acquire(algorithm);
  } else {
    blobHasher = await createIncrementalHasher(algorithm);
  }

  try {
    const inFlight = new Set<Promise<void>>();

    const setInFlightProgress = (segmentId: number, bytesUploaded: number) => {
      const previous = inFlightBytesById.get(segmentId) ?? 0;
      inFlightBytesById.set(segmentId, Math.max(previous, bytesUploaded));
      report();
    };

    const uploadSegment = async (segment: ChunkSegment) => {
      const chunk = blob.slice(segment.start, segment.end);
      const chunkBytes = getSegmentLength(segment);
      const startedAt = performance.now();

      try {
        const buffer = await chunk.arrayBuffer();
        let chunkHash: string;

        if (segment.updateFileHash) {
          await waitForFileHashTurn(segment);
        }

        if (worker) {
          chunkHash = await worker.hashChunk(buffer, {
            updateFileHash: segment.updateFileHash,
          });
        } else {
          const bytes = new Uint8Array(buffer);
          if (segment.updateFileHash) {
            blobHasher!.update(bytes);
          }
          chunkHash = await hashBytes(bytes, algorithm);
        }

        if (segment.updateFileHash) {
          advanceFileHashOffset(segment);
        }

        if (sendChunkHashForValidation) {
          const exists = await chunksApi.exists(chunkHash, abortController.signal);
          if (!exists) {
            await chunksApi.uploadChunk({
              blob: chunk,
              fileName,
              hash: chunkHash,
              signal: abortController.signal,
              onProgress: (bytesUploaded) => {
                setInFlightProgress(segment.id, bytesUploaded);
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
              setInFlightProgress(segment.id, bytesUploaded);
            },
          });
        }

        inFlightBytesById.delete(segment.id);
        uploadedSegments.push({ start: segment.start, end: segment.end, hash: chunkHash });
        completedBytes += chunkBytes;
        report();
        chunkUploadConcurrency.observe({
          bytes: chunkBytes,
          durationMs: performance.now() - startedAt,
          succeeded: true,
        });
      } catch (error) {
        inFlightBytesById.delete(segment.id);

        if (!fatalError && isConnectionInterruption(error)) {
          queueRetrySegments(segment);
          chunkUploadConcurrency.observe({
            bytes: chunkBytes,
            durationMs: performance.now() - startedAt,
            succeeded: false,
          });
          return;
        }

        failUpload(error);
      }
    };

    const startUpload = (segment: ChunkSegment) => {
      const uploadPromise = uploadSegment(segment);
      inFlight.add(uploadPromise);
      uploadPromise.finally(() => {
        inFlight.delete(uploadPromise);
      });
    };

    while (!fatalError) {
      let startedAny = false;
      const now = Date.now();

      while (inFlight.size < chunkUploadConcurrency.current && !fatalError) {
        const segment = takeReadyPendingSegment(now) ?? createNextInitialSegment();
        if (!segment) {
          break;
        }

        startUpload(segment);
        startedAny = true;
      }

      if (fatalError) {
        break;
      }

      if (nextOffset >= blob.size && pendingSegments.length === 0 && inFlight.size === 0) {
        break;
      }

      if (inFlight.size > 0) {
        await Promise.race(inFlight);
        continue;
      }

      const nextPendingTime = getNextPendingTime();
      if (nextPendingTime !== null) {
        await delay(Math.max(0, nextPendingTime - Date.now()));
        continue;
      }

      if (!startedAny) {
        break;
      }
    }

    while (inFlight.size > 0) {
      await Promise.race(inFlight);
    }

    if (fatalError) {
      throw fatalError;
    }

    if (nextFileHashOffset !== blob.size) {
      throw new Error("File hash did not cover the complete file.");
    }

    const fileHash = worker ? await worker.digestFile() : blobHasher!.digestHex();
    const chunkHashes = buildOrderedChunkHashes();

    report();
    return { chunkHashes, fileHash };
  } finally {
    if (worker) {
      globalHashWorkerPool.release(worker);
    }
  }
}
