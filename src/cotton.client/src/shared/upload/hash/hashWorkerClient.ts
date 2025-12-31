import type { SupportedHashAlgorithm } from "./hashing";

type HashChunkResult = { type: "hashChunkResult"; requestId: string; chunkHash: string };
type DigestFileResult = { type: "digestFileResult"; requestId: string; fileHash: string };
type ErrorResult = { type: "error"; requestId?: string; message: string };

type OutMessage = HashChunkResult | DigestFileResult | ErrorResult;

type PendingRequest<T> = {
  resolve: (value: T) => void;
  reject: (err: Error) => void;
};

const makeRequestId = () => `${Date.now()}-${Math.random().toString(16).slice(2)}`;

export class HashWorkerClient {
  private readonly worker: Worker;
  private readonly pending = new Map<string, PendingRequest<string>>();

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
    this.worker.postMessage({ type: "init", algorithm });
  }

  hashChunk(buffer: ArrayBuffer): Promise<string> {
    const requestId = makeRequestId();
    const promise = new Promise<string>((resolve, reject) => {
      this.pending.set(requestId, { resolve, reject });
    });

    // Transfer ownership of the buffer to avoid copying.
    this.worker.postMessage({ type: "hashChunk", requestId, buffer }, [buffer]);
    return promise;
  }

  digestFile(): Promise<string> {
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
  }
}

export function canUseHashWorker(): boolean {
  // Vite builds for modern browsers; still guard for environments without Worker.
  return typeof Worker !== "undefined";
}
