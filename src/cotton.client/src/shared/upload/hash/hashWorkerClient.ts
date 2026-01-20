import type { SupportedHashAlgorithm } from "./hashing";

type InitResult = { type: "initResult"; requestId: string };
type HashChunkResult = { type: "hashChunkResult"; requestId: string; chunkHash: string };
type DigestFileResult = { type: "digestFileResult"; requestId: string; fileHash: string };
type ErrorResult = { type: "error"; requestId?: string; message: string };

type OutMessage = InitResult | HashChunkResult | DigestFileResult | ErrorResult;

type PendingRequest<T> = {
  resolve: (value: T) => void;
  reject: (err: Error) => void;
};

const makeRequestId = () => `${Date.now()}-${Math.random().toString(16).slice(2)}`;

export class HashWorkerClient {
  private readonly worker: Worker;
  private readonly pending = new Map<string, PendingRequest<string>>();
  private readonly pendingVoid = new Map<string, PendingRequest<void>>();
  private initBarrier: Promise<void> | null = null;

  constructor() {
    this.worker = new Worker(new URL("./hash.worker.ts", import.meta.url), { type: "module" });
    this.worker.onmessage = (ev: MessageEvent<OutMessage>) => {
      const msg = ev.data;
      if (msg.type === "error") {
        if (msg.requestId) {
          const p = this.pending.get(msg.requestId);
          if (p) {
            this.pending.delete(msg.requestId);
            p.reject(new Error(msg.message));
          }
          const pVoid = this.pendingVoid.get(msg.requestId);
          if (pVoid) {
            this.pendingVoid.delete(msg.requestId);
            pVoid.reject(new Error(msg.message));
          }
        }
        return;
      }

      if (msg.type === "initResult") {
        const pVoid = this.pendingVoid.get(msg.requestId);
        if (pVoid) {
          this.pendingVoid.delete(msg.requestId);
          pVoid.resolve();
        }
        return;
      }

      const p = this.pending.get(msg.requestId);
      if (!p) return;
      this.pending.delete(msg.requestId);

      if (msg.type === "hashChunkResult") {
        p.resolve(msg.chunkHash);
      } else if (msg.type === "digestFileResult") {
        p.resolve(msg.fileHash);
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

  async hashChunk(buffer: ArrayBuffer): Promise<string> {
    await this.ensureInitialized();
    const requestId = makeRequestId();
    const promise = new Promise<string>((resolve, reject) => {
      this.pending.set(requestId, { resolve, reject });
    });

    // Transfer ownership of the buffer to avoid copying.
    this.worker.postMessage({ type: "hashChunk", requestId, buffer }, [buffer]);
    return promise;
  }

  async digestFile(): Promise<string> {
    await this.ensureInitialized();
    const requestId = makeRequestId();
    const promise = new Promise<string>((resolve, reject) => {
      this.pending.set(requestId, { resolve, reject });
    });

    this.worker.postMessage({ type: "digestFile", requestId });
    return promise;
  }

  terminate() {
    this.worker.terminate();
    this.pending.clear();
    this.pendingVoid.clear();
    this.initBarrier = null;
  }
}

export function canUseHashWorker(): boolean {
  // Vite builds for modern browsers; still guard for environments without Worker.
  return typeof Worker !== "undefined";
}
