import { HashWorkerClient } from './hashWorkerClient';
import type { SupportedHashAlgorithm } from './hashing';

/**
 * Pool of reusable HashWorkerClient instances to avoid WASM memory exhaustion.
 * 
 * Problem: Creating a new Worker + WASM instance for each file upload causes
 * "Out of memory: Cannot allocate Wasm memory" when uploading thousands of files.
 * 
 * Solution: Maintain a pool of workers that are reused across uploads.
 * Workers are initialized on-demand and kept alive for the session.
 */
class HashWorkerPool {
  private readonly maxWorkers: number;
  private readonly workers: HashWorkerClient[] = [];
  private readonly available: HashWorkerClient[] = [];
  private readonly inUse = new Set<HashWorkerClient>();

  constructor(maxWorkers = 4) {
    this.maxWorkers = maxWorkers;
  }

  /**
   * Acquire a worker from the pool. If all workers are busy and pool is not full,
   * creates a new worker. Otherwise, waits for a worker to become available.
   */
  async acquire(algorithm: SupportedHashAlgorithm): Promise<HashWorkerClient> {
    // Reuse available worker if possible
    if (this.available.length > 0) {
      const worker = this.available.pop()!;
      // CRITICAL: Initialize BEFORE adding to inUse to prevent race condition
      // where hashChunk() is called before init() completes
      await worker.init(algorithm);
      this.inUse.add(worker);
      return worker;
    }

    // Create new worker if pool is not full
    if (this.workers.length < this.maxWorkers) {
      const worker = new HashWorkerClient();
      this.workers.push(worker);
      // CRITICAL: Initialize BEFORE adding to inUse
      await worker.init(algorithm);
      this.inUse.add(worker);
      return worker;
    }

    // Wait for a worker to become available
    return new Promise<HashWorkerClient>((resolve) => {
      const checkInterval = setInterval(async () => {
        if (this.available.length > 0) {
          clearInterval(checkInterval);
          const worker = this.available.pop()!;
          // CRITICAL: Initialize BEFORE adding to inUse
          await worker.init(algorithm);
          this.inUse.add(worker);
          resolve(worker);
        }
      }, 50);
    });
  }

  /**
   * Release a worker back to the pool for reuse.
   * IMPORTANT: Don't call worker.terminate() - the pool manages worker lifecycle.
   */
  release(worker: HashWorkerClient): void {
    if (!this.inUse.has(worker)) {
      console.warn('Attempting to release a worker that is not in use');
      return;
    }

    this.inUse.delete(worker);
    this.available.push(worker);
  }

  /**
   * Terminate all workers and clear the pool.
   * Call this when unmounting the upload manager or on session end.
   */
  destroy(): void {
    for (const worker of this.workers) {
      worker.terminate();
    }
    this.workers.length = 0;
    this.available.length = 0;
    this.inUse.clear();
  }

  /**
   * Get pool statistics for debugging/monitoring
   */
  getStats() {
    return {
      total: this.workers.length,
      available: this.available.length,
      inUse: this.inUse.size,
      maxWorkers: this.maxWorkers,
    };
  }
}

// Global singleton pool for the entire application
const globalHashWorkerPool = new HashWorkerPool(4);

export { HashWorkerPool, globalHashWorkerPool };
