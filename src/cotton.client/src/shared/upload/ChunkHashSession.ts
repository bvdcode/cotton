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

  private constructor(
    blob: Blob,
    algorithm: SupportedHashAlgorithm,
    worker: HashWorkerClient | null,
    fileHasher: IncrementalHasher | null,
  ) {
    this.blob = blob;
    this.algorithm = algorithm;
    this.worker = worker;
    this.fileHasher = fileHasher;
  }

  static async create(
    blob: Blob,
    algorithm: SupportedHashAlgorithm,
  ): Promise<ChunkHashSession> {
    if (canUseHashWorker()) {
      const worker = await globalHashWorkerPool.acquire(algorithm);
      return new ChunkHashSession(blob, algorithm, worker, null);
    }

    const fileHasher = await createIncrementalHasher(algorithm);
    return new ChunkHashSession(blob, algorithm, null, fileHasher);
  }

  async prepare(
    segment: ChunkSegment,
    updateFileHash: boolean,
  ): Promise<PreparedChunk> {
    const chunk = this.blob.slice(segment.start, segment.end);
    let buffer = await chunk.arrayBuffer();
    let hash: string;

    if (this.worker) {
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

  async digestFile(): Promise<string> {
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
    if (this.worker) {
      globalHashWorkerPool.release(this.worker);
    }
  }
}
