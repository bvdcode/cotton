import type { SupportedHashAlgorithm } from "./hashing";

type InitResult = { type: "initResult"; requestId: string };
type HashChunkResult = {
  type: "hashChunkResult";
  requestId: string;
  chunkHash: string;
  buffer: ArrayBuffer;
};
type UpdateFileHashResult = { type: "updateFileHashResult"; requestId: string };
type DigestFileResult = {
  type: "digestFileResult";
  requestId: string;
  fileHash: string;
};
type ErrorResult = { type: "error"; requestId?: string; message: string };

type OutMessage =
  | InitResult
  | HashChunkResult
  | UpdateFileHashResult
  | DigestFileResult
  | ErrorResult;

type PendingRequest<T> = {
  resolve: (value: T) => void;
  reject: (err: Error) => void;
};

export interface HashedChunkBuffer {
  chunkHash: string;
  buffer: ArrayBuffer;
}

const makeRequestId = () =>
  `${Date.now()}-${Math.random().toString(16).slice(2)}`;

export class HashWorkerClient {
  private readonly worker: Worker;
  private readonly pendingStrings = new Map<string, PendingRequest<string>>();
  private readonly pendingChunks = new Map<
    string,
    PendingRequest<HashedChunkBuffer>
  >();
  private readonly pendingVoid = new Map<string, PendingRequest<void>>();
  private initBarrier: Promise<void> | null = null;

  constructor() {
    this.worker = new Worker(new URL("./hash.worker.ts", import.meta.url), {
      type: "module",
    });
    this.worker.onmessage = (ev: MessageEvent<OutMessage>) => {
      const msg = ev.data;
      switch (msg.type) {
        case "error":
          this.rejectRequest(msg);
          return;
        case "initResult":
        case "updateFileHashResult":
          this.resolveVoidRequest(msg.requestId);
          return;
        case "hashChunkResult":
          this.resolveChunkRequest(msg);
          return;
        case "digestFileResult":
          this.resolveStringRequest(msg.requestId, msg.fileHash);
          return;
      }
    };
  }

  async init(algorithm: SupportedHashAlgorithm): Promise<void> {
    const requestId = makeRequestId();
    const promise = new Promise<void>((resolve, reject) => {
      this.pendingVoid.set(requestId, { resolve, reject });
    });

    // Expose init completion as a barrier so consumers can't hash before the worker is ready.
    this.initBarrier = promise;

    this.worker.postMessage({ type: "init", requestId, algorithm });
    return promise;
  }

  private async ensureInitialized(): Promise<void> {
    if (!this.initBarrier) {
      throw new Error("Hash worker is not initialized");
    }
    await this.initBarrier;
  }

  async hashChunk(
    buffer: ArrayBuffer,
    options?: { updateFileHash?: boolean },
  ): Promise<HashedChunkBuffer> {
    await this.ensureInitialized();
    const requestId = makeRequestId();
    const promise = new Promise<HashedChunkBuffer>((resolve, reject) => {
      this.pendingChunks.set(requestId, { resolve, reject });
    });

    this.worker.postMessage(
      {
        type: "hashChunk",
        requestId,
        buffer,
        updateFileHash: options?.updateFileHash,
      },
      [buffer],
    );
    return promise;
  }

  async updateFileHash(buffers: ArrayBuffer[]): Promise<void> {
    await this.ensureInitialized();
    const requestId = makeRequestId();
    const promise = new Promise<void>((resolve, reject) => {
      this.pendingVoid.set(requestId, { resolve, reject });
    });

    this.worker.postMessage(
      {
        type: "updateFileHash",
        requestId,
        buffers,
      },
      buffers,
    );
    return promise;
  }

  async digestFile(): Promise<string> {
    await this.ensureInitialized();
    const requestId = makeRequestId();
    const promise = new Promise<string>((resolve, reject) => {
      this.pendingStrings.set(requestId, { resolve, reject });
    });

    this.worker.postMessage({ type: "digestFile", requestId });
    return promise;
  }

  terminate() {
    this.worker.terminate();
    this.pendingStrings.clear();
    this.pendingChunks.clear();
    this.pendingVoid.clear();
    this.initBarrier = null;
  }

  private rejectRequest(message: ErrorResult): void {
    if (!message.requestId) {
      return;
    }

    const error = new Error(message.message);
    const stringRequest = this.pendingStrings.get(message.requestId);
    if (stringRequest) {
      this.pendingStrings.delete(message.requestId);
      stringRequest.reject(error);
    }

    const chunkRequest = this.pendingChunks.get(message.requestId);
    if (chunkRequest) {
      this.pendingChunks.delete(message.requestId);
      chunkRequest.reject(error);
    }

    const voidRequest = this.pendingVoid.get(message.requestId);
    if (voidRequest) {
      this.pendingVoid.delete(message.requestId);
      voidRequest.reject(error);
    }
  }

  private resolveVoidRequest(requestId: string): void {
    const request = this.pendingVoid.get(requestId);
    if (!request) {
      return;
    }

    this.pendingVoid.delete(requestId);
    request.resolve();
  }

  private resolveChunkRequest(message: HashChunkResult): void {
    const request = this.pendingChunks.get(message.requestId);
    if (!request) {
      return;
    }

    this.pendingChunks.delete(message.requestId);
    request.resolve({
      chunkHash: message.chunkHash,
      buffer: message.buffer,
    });
  }

  private resolveStringRequest(requestId: string, value: string): void {
    const request = this.pendingStrings.get(requestId);
    if (!request) {
      return;
    }

    this.pendingStrings.delete(requestId);
    request.resolve(value);
  }
}

export function canUseHashWorker(): boolean {
  // Vite builds for modern browsers; still guard for environments without Worker.
  return typeof Worker !== "undefined";
}
