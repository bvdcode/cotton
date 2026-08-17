import {
  createIncrementalHasher,
  hashBuffer,
  type IncrementalHasher,
  type SupportedHashAlgorithm,
} from "./hash/hashing";
import { canUseHashWorker, HashWorkerClient } from "./hash/hashWorkerClient";
import { globalHashWorkerPool } from "./hash/HashWorkerPool";
import type { ChunkSegment, PreparedChunk } from "./chunkUploadPipelineTypes";

export class ChunkHashSession {
  private released = false;
  private readonly blob: Blob;
  private readonly algorithm: SupportedHashAlgorithm;
  private readonly worker: HashWorkerClient | null;
  private readonly fileHasher: IncrementalHasher | null;
  private readonly fileHashPromise: Promise<string> | null;
  private readonly nativeChunkHashing: boolean;
  private fileHashCompleted = false;
  readonly maxParallelPreparations: number;

  private constructor(
    blob: Blob,
    algorithm: SupportedHashAlgorithm,
    worker: HashWorkerClient | null,
    fileHasher: IncrementalHasher | null,
    fileHashPromise: Promise<string> | null,
    nativeChunkHashing: boolean,
    maxParallelPreparations: number,
  ) {
    this.blob = blob;
    this.algorithm = algorithm;
    this.worker = worker;
    this.fileHasher = fileHasher;
    this.fileHashPromise = fileHashPromise;
    this.nativeChunkHashing = nativeChunkHashing;
    this.maxParallelPreparations = maxParallelPreparations;
    if (fileHashPromise) {
      void fileHashPromise.then(
        () => {
          this.fileHashCompleted = true;
        },
        () => undefined,
      );
    }
  }

  static async create(
    blob: Blob,
    algorithm: SupportedHashAlgorithm,
    chunkHashConcurrency: number,
    wholeFileHashReadSizeBytes: number,
  ): Promise<ChunkHashSession> {
    if (canUseHashWorker()) {
      const worker = await globalHashWorkerPool.acquire(algorithm);
      if (globalThis.crypto?.subtle) {
        const fileHashPromise = worker.hashBlob(
          blob,
          wholeFileHashReadSizeBytes,
        );
        return new ChunkHashSession(
          blob,
          algorithm,
          worker,
          null,
          fileHashPromise,
          true,
          Math.max(1, chunkHashConcurrency),
        );
      }

      return new ChunkHashSession(
        blob,
        algorithm,
        worker,
        null,
        null,
        false,
        1,
      );
    }

    const fileHasher = await createIncrementalHasher(algorithm);
    return new ChunkHashSession(
      blob,
      algorithm,
      null,
      fileHasher,
      null,
      false,
      1,
    );
  }

  async prepare(
    segment: ChunkSegment,
    updateFileHash: boolean,
  ): Promise<PreparedChunk> {
    const chunk = this.blob.slice(segment.start, segment.end);
    const buffer = await chunk.arrayBuffer();
    let hash: string;

    if (this.nativeChunkHashing) {
      hash = await hashBuffer(buffer, this.algorithm);
    } else if (this.worker) {
      const result = await this.worker.hashChunk(buffer, { updateFileHash });
      hash = result.chunkHash;
    } else {
      if (updateFileHash) {
        this.fileHasher?.update(new Uint8Array(buffer));
      }
      hash = await hashBuffer(buffer, this.algorithm);
    }

    return {
      segment,
      blob: chunk,
      hash,
    };
  }

  async digestFile(): Promise<string> {
    if (this.fileHashPromise) {
      return this.fileHashPromise;
    }

    if (this.worker) {
      return this.worker.digestFile();
    }

    if (!this.fileHasher) {
      throw new Error("File hasher is unavailable.");
    }

    return this.fileHasher.digestHex();
  }

  release(): void {
    if (this.released) {
      return;
    }

    this.released = true;
    const worker = this.worker;
    if (worker) {
      if (this.fileHashPromise && !this.fileHashCompleted) {
        globalHashWorkerPool.replace(worker);
        return;
      }

      globalHashWorkerPool.release(worker);
    }
  }
}
