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
  private readonly nativeChunkHashing: boolean;
  private fileHashUpdate: Promise<void> | null = null;
  readonly maxParallelPreparations: number;

  private constructor(
    blob: Blob,
    algorithm: SupportedHashAlgorithm,
    worker: HashWorkerClient | null,
    fileHasher: IncrementalHasher | null,
    nativeChunkHashing: boolean,
    maxParallelPreparations: number,
  ) {
    this.blob = blob;
    this.algorithm = algorithm;
    this.worker = worker;
    this.fileHasher = fileHasher;
    this.nativeChunkHashing = nativeChunkHashing;
    this.maxParallelPreparations = maxParallelPreparations;
  }

  static async create(
    blob: Blob,
    algorithm: SupportedHashAlgorithm,
    chunkHashConcurrency: number,
  ): Promise<ChunkHashSession> {
    if (canUseHashWorker()) {
      const worker = await globalHashWorkerPool.acquire(algorithm);
      if (globalThis.crypto?.subtle) {
        return new ChunkHashSession(
          blob,
          algorithm,
          worker,
          null,
          true,
          Math.max(1, chunkHashConcurrency),
        );
      }

      return new ChunkHashSession(blob, algorithm, worker, null, false, 1);
    }

    const fileHasher = await createIncrementalHasher(algorithm);
    return new ChunkHashSession(blob, algorithm, null, fileHasher, false, 1);
  }

  async prepare(
    segment: ChunkSegment,
    updateFileHash: boolean,
  ): Promise<PreparedChunk> {
    const chunk = this.blob.slice(segment.start, segment.end);
    let buffer = await chunk.arrayBuffer();
    let hash: string;

    if (this.nativeChunkHashing) {
      hash = await hashBuffer(buffer, this.algorithm);
    } else if (this.worker) {
      const result = await this.worker.hashChunk(buffer, { updateFileHash });
      buffer = result.buffer;
      hash = result.chunkHash;
    } else {
      if (updateFileHash) {
        this.fileHasher?.update(new Uint8Array(buffer));
      }
      hash = await hashBuffer(buffer, this.algorithm);
    }

    return {
      segment,
      buffer,
      hash,
      contentType: chunk.type,
    };
  }

  async prepareBatch(segments: ChunkSegment[]): Promise<PreparedChunk[]> {
    if (!this.nativeChunkHashing) {
      return Promise.all(
        segments.map((segment) => this.prepare(segment, true)),
      );
    }

    if (!this.worker) {
      throw new Error("File hash worker is unavailable.");
    }

    const pendingChunks = await Promise.all(
      segments.map(async (segment) => {
        const chunk = this.blob.slice(segment.start, segment.end);
        const buffer = await chunk.arrayBuffer();
        return {
          segment,
          buffer,
          fileHashBuffer: buffer.slice(0),
          hash: hashBuffer(buffer, this.algorithm),
          contentType: chunk.type,
        };
      }),
    );

    if (this.fileHashUpdate) {
      await this.fileHashUpdate;
    }

    const fileHashUpdate = this.worker.updateFileHash(
      pendingChunks.map((prepared) => prepared.fileHashBuffer),
    );
    this.fileHashUpdate = fileHashUpdate;
    void fileHashUpdate.catch(() => undefined);
    const hashes = await Promise.all(
      pendingChunks.map((prepared) => prepared.hash),
    );

    return pendingChunks.map((prepared, index) => ({
      segment: prepared.segment,
      buffer: prepared.buffer,
      hash: hashes[index],
      contentType: prepared.contentType,
    }));
  }

  async digestFile(): Promise<string> {
    if (this.fileHashUpdate) {
      await this.fileHashUpdate;
    }

    if (this.worker) {
      return this.worker.digestFile();
    }

    if (!this.fileHasher) {
      throw new Error("File hasher is unavailable.");
    }

    return this.fileHasher.digestHex();
  }

  async release(): Promise<void> {
    if (this.released) {
      return;
    }

    this.released = true;
    if (this.worker) {
      try {
        await this.fileHashUpdate;
      } catch {
        // The upload path already owns the hashing failure.
      }
      globalHashWorkerPool.release(this.worker);
    }
  }
}
