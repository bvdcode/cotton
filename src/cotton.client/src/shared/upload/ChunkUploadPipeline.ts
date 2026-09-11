import { chunksApi } from "../api/chunksApi";
import { isAxiosError } from "../api/httpClient";
import { BoundedByteQueue } from "./BoundedByteQueue";
import { ChunkHashSession } from "./ChunkHashSession";
import { ChunkUploadProgressTracker } from "./ChunkUploadProgressTracker";
import {
  getChunkLength,
  type ChunkSegment,
  type PreparedChunk,
  type UploadedChunkSegment,
} from "./chunkUploadPipelineTypes";
import type { UploadProgressSnapshot } from "./types";

interface ChunkUploadPipelineOptions {
  blob: Blob;
  fileName: string;
  hashSession: ChunkHashSession;
  hashPreparationConcurrency: number;
  sendChunkHashForValidation: boolean;
  chunkSizeBytes: number;
  minRetryChunkSizeBytes: number;
  uploadConcurrency: number;
  maxQueuedChunks: number;
  maxQueuedBytes: number;
  onProgress?: (
    bytesUploaded: number,
    snapshot?: UploadProgressSnapshot,
  ) => void;
}

interface ChunkUploadPipelineResult {
  chunkHashes: string[];
  fileHash: string;
}

const getRetryDelayMs = (networkFailures: number): number =>
  Math.min(5000, 250 * 2 ** Math.min(Math.max(0, networkFailures - 1), 4));

const isConnectionInterruption = (error: Error): boolean => {
  if (!isAxiosError(error)) {
    return false;
  }

  if (error.response) {
    switch (error.response.status) {
      case 502:
      case 503:
      case 504:
        return true;
      default:
        return false;
    }
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

const waitForDelay = (
  milliseconds: number,
  signal: AbortSignal,
): Promise<void> =>
  new Promise<void>((resolve, reject) => {
    if (signal.aborted) {
      reject(new Error("Upload was cancelled."));
      return;
    }

    const timeoutId = globalThis.setTimeout(() => {
      signal.removeEventListener("abort", onAbort);
      resolve();
    }, milliseconds);
    const onAbort = () => {
      globalThis.clearTimeout(timeoutId);
      reject(new Error("Upload was cancelled."));
    };

    signal.addEventListener("abort", onAbort, { once: true });
  });

const waitForBrowserOnline = (signal: AbortSignal): Promise<void> =>
  new Promise<void>((resolve, reject) => {
    if (signal.aborted) {
      reject(new Error("Upload was cancelled."));
      return;
    }

    if (
      typeof navigator === "undefined" ||
      typeof window === "undefined" ||
      navigator.onLine
    ) {
      resolve();
      return;
    }

    const onOnline = () => {
      signal.removeEventListener("abort", onAbort);
      resolve();
    };
    const onAbort = () => {
      window.removeEventListener("online", onOnline);
      reject(new Error("Upload was cancelled."));
    };

    window.addEventListener("online", onOnline, { once: true });
    signal.addEventListener("abort", onAbort, { once: true });
  });

export class ChunkUploadPipeline {
  private readonly queue: BoundedByteQueue<PreparedChunk>;
  private readonly progress: ChunkUploadProgressTracker;
  private readonly abortController = new AbortController();
  private readonly uploadedSegments: UploadedChunkSegment[] = [];
  private fatalError: Error | null = null;
  private readonly options: ChunkUploadPipelineOptions;

  constructor(options: ChunkUploadPipelineOptions) {
    this.options = options;
    this.queue = new BoundedByteQueue<PreparedChunk>(
      options.maxQueuedChunks,
      Math.max(options.maxQueuedBytes, options.chunkSizeBytes),
    );
    this.progress = new ChunkUploadProgressTracker(
      options.blob.size,
      options.onProgress,
    );
  }

  async run(): Promise<ChunkUploadPipelineResult> {
    const tasks: Promise<void>[] = [this.protect(this.produce())];
    for (let index = 0; index < this.options.uploadConcurrency; index += 1) {
      tasks.push(this.protect(this.consume()));
    }

    await Promise.all(tasks);
    if (this.fatalError) {
      throw this.fatalError;
    }

    return {
      chunkHashes: this.buildOrderedChunkHashes(),
      fileHash: await this.options.hashSession.digestFile(),
    };
  }

  private async protect(task: Promise<void>): Promise<void> {
    try {
      await task;
    } catch (error) {
      const failure =
        error instanceof Error ? error : new Error("Upload pipeline failed.");
      this.fail(failure);
    }
  }

  private async produce(): Promise<void> {
    let batch: ChunkSegment[] = [];
    for (
      let start = 0;
      start < this.options.blob.size;
      start += this.options.chunkSizeBytes
    ) {
      this.throwIfFailed();
      const segment: ChunkSegment = {
        start,
        end: Math.min(
          this.options.blob.size,
          start + this.options.chunkSizeBytes,
        ),
        networkFailures: 0,
      };
      batch.push(segment);
      if (batch.length >= this.options.hashPreparationConcurrency) {
        await this.prepareAndEnqueue(batch);
        batch = [];
      }
    }

    if (batch.length > 0) {
      await this.prepareAndEnqueue(batch);
    }

    this.queue.close();
  }

  private async prepareAndEnqueue(segments: ChunkSegment[]): Promise<void> {
    const preparedChunks =
      await this.options.hashSession.prepareBatch(segments);
    for (const prepared of preparedChunks) {
      this.throwIfFailed();
      await this.queue.enqueue(prepared, getChunkLength(prepared.segment));
    }
  }

  private async consume(): Promise<void> {
    while (!this.fatalError) {
      const prepared = await this.queue.dequeue();
      if (!prepared) {
        return;
      }

      await this.processPreparedChunk(prepared);
    }
  }

  private async processPreparedChunk(
    initialPrepared: PreparedChunk,
  ): Promise<void> {
    let prepared = initialPrepared;
    let probeBeforeUpload = this.options.sendChunkHashForValidation;

    while (!this.fatalError) {
      let attemptId: number | null = null;

      try {
        if (probeBeforeUpload && (await this.chunkExists(prepared))) {
          this.completeWithoutTransfer(prepared);
          return;
        }

        attemptId = this.progress.beginAttempt();
        const uploadAttemptId = attemptId;
        const chunkBytes = getChunkLength(prepared.segment);
        await chunksApi.uploadChunk({
          blob: new Blob([prepared.buffer], { type: prepared.contentType }),
          fileName: this.options.fileName,
          hash: prepared.hash,
          signal: this.abortController.signal,
          onProgress: (bytesUploaded) => {
            this.progress.updateTransmission(uploadAttemptId, bytesUploaded);
          },
        });
        this.progress.completeAttempt(uploadAttemptId, chunkBytes);
        this.recordUploadedSegment(prepared);
        return;
      } catch (error) {
        if (attemptId !== null) {
          this.progress.discardAttempt(attemptId);
        }

        const failure =
          error instanceof Error ? error : new Error("Chunk upload failed.");
        if (!this.fatalError && isConnectionInterruption(failure)) {
          const retry = await this.recoverInterruptedChunk(prepared);
          if (!retry) {
            return;
          }

          prepared = retry;
          probeBeforeUpload = false;
          continue;
        }

        throw failure;
      }
    }

    this.throwIfFailed();
  }

  private async chunkExists(prepared: PreparedChunk): Promise<boolean> {
    let networkFailures = prepared.segment.networkFailures;

    while (!this.fatalError) {
      try {
        return await chunksApi.exists(
          prepared.hash,
          this.abortController.signal,
        );
      } catch (error) {
        const failure =
          error instanceof Error
            ? error
            : new Error("Chunk verification failed.");
        if (!isConnectionInterruption(failure)) {
          throw failure;
        }

        networkFailures += 1;
        await waitForDelay(
          getRetryDelayMs(networkFailures),
          this.abortController.signal,
        );
        await waitForBrowserOnline(this.abortController.signal);
        this.throwIfFailed();
      }
    }

    this.throwIfFailed();
    throw new Error("Upload pipeline failed.");
  }

  private async recoverInterruptedChunk(
    prepared: PreparedChunk,
  ): Promise<PreparedChunk | null> {
    let networkFailures = prepared.segment.networkFailures + 1;

    while (!this.fatalError) {
      await waitForDelay(
        getRetryDelayMs(networkFailures),
        this.abortController.signal,
      );
      await waitForBrowserOnline(this.abortController.signal);
      this.throwIfFailed();

      if (!this.options.sendChunkHashForValidation) {
        break;
      }

      try {
        const exists = await chunksApi.exists(
          prepared.hash,
          this.abortController.signal,
        );
        if (exists) {
          this.completeWithoutTransfer(prepared);
          return null;
        }
        break;
      } catch (error) {
        const failure =
          error instanceof Error
            ? error
            : new Error("Chunk verification failed.");
        if (!isConnectionInterruption(failure)) {
          throw failure;
        }
        networkFailures += 1;
      }
    }

    this.throwIfFailed();
    const chunkBytes = getChunkLength(prepared.segment);
    const retrySize = Math.min(
      chunkBytes,
      Math.max(this.options.minRetryChunkSizeBytes, Math.floor(chunkBytes / 2)),
    );

    if (retrySize === chunkBytes) {
      return {
        ...prepared,
        segment: {
          ...prepared.segment,
          networkFailures,
        },
      };
    }

    await this.uploadRetrySegments(prepared, networkFailures, retrySize);
    return null;
  }

  private async uploadRetrySegments(
    prepared: PreparedChunk,
    networkFailures: number,
    retrySize: number,
  ): Promise<void> {
    for (
      let start = prepared.segment.start;
      start < prepared.segment.end;
      start += retrySize
    ) {
      this.throwIfFailed();
      const segment: ChunkSegment = {
        start,
        end: Math.min(prepared.segment.end, start + retrySize),
        networkFailures,
      };
      const retry = await this.options.hashSession.prepare(segment, false);
      await this.processPreparedChunk(retry);
    }
  }

  private completeWithoutTransfer(prepared: PreparedChunk): void {
    this.progress.completeWithoutTransfer(getChunkLength(prepared.segment));
    this.recordUploadedSegment(prepared);
  }

  private recordUploadedSegment(prepared: PreparedChunk): void {
    this.uploadedSegments.push({
      start: prepared.segment.start,
      end: prepared.segment.end,
      hash: prepared.hash,
    });
  }

  private buildOrderedChunkHashes(): string[] {
    this.uploadedSegments.sort((left, right) => left.start - right.start);

    let expectedStart = 0;
    for (const segment of this.uploadedSegments) {
      if (segment.start !== expectedStart || segment.end <= segment.start) {
        throw new Error("Uploaded chunks do not cover the file contiguously.");
      }
      expectedStart = segment.end;
    }

    if (expectedStart !== this.options.blob.size) {
      throw new Error("Uploaded chunks do not cover the complete file.");
    }

    return this.uploadedSegments.map((segment) => segment.hash);
  }

  private throwIfFailed(): void {
    if (this.fatalError) {
      throw this.fatalError;
    }
  }

  private fail(error: Error): void {
    if (this.fatalError) {
      return;
    }

    this.fatalError = error;
    this.abortController.abort();
    this.queue.fail(error);
  }
}
