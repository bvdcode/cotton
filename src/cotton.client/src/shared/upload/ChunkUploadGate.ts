import { uploadConfig } from "./config";

export class ChunkUploadGate {
  private active = 0;
  private readonly waiters = new Set<() => void>();
  private readonly capacity: number;

  constructor(capacity: number) {
    if (!Number.isInteger(capacity) || capacity < 1) {
      throw new Error("Upload capacity must be a positive integer.");
    }
    this.capacity = capacity;
  }

  async run<T>(operation: () => Promise<T>, signal: AbortSignal): Promise<T> {
    await this.acquire(signal);
    try {
      signal.throwIfAborted();
      return await operation();
    } finally {
      this.release();
    }
  }

  private async acquire(signal: AbortSignal): Promise<void> {
    signal.throwIfAborted();
    if (this.active < this.capacity) {
      this.active += 1;
      return;
    }

    await new Promise<void>((resolve, reject) => {
      const grant = () => {
        signal.removeEventListener("abort", cancel);
        resolve();
      };
      const cancel = () => {
        this.waiters.delete(grant);
        reject(new DOMException("Upload was cancelled.", "AbortError"));
      };
      this.waiters.add(grant);
      signal.addEventListener("abort", cancel, { once: true });
    });
  }

  private release(): void {
    const waiter = this.waiters.values().next().value;
    if (waiter) {
      this.waiters.delete(waiter);
      waiter();
      return;
    }
    this.active -= 1;
  }
}

export const globalChunkUploadGate = new ChunkUploadGate(
  uploadConfig.maxChunkUploadConcurrency,
);
