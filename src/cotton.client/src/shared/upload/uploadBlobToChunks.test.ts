import {
  AxiosError,
  AxiosHeaders,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { UploadProgressSnapshot } from "./types";
import { uploadBlobToChunks } from "./uploadBlobToChunks";

interface Deferred<T> {
  promise: Promise<T>;
  resolve: (value: T) => void;
}

const createDeferred = <T>(): Deferred<T> => {
  let resolvePromise: (value: T) => void = () => {
    throw new Error("Deferred promise is not initialized.");
  };
  const promise = new Promise<T>((resolve) => {
    resolvePromise = resolve;
  });
  return { promise, resolve: resolvePromise };
};

const mocks = vi.hoisted(() => ({
  acquire: vi.fn(),
  release: vi.fn(),
  chunkExists: vi.fn(),
  uploadChunk: vi.fn(),
  hashBuffer: vi.fn(),
  toWebCryptoAlgorithm: vi.fn(() => "SHA-256"),
}));

vi.mock("../api/chunksApi", () => ({
  chunksApi: {
    exists: mocks.chunkExists,
    uploadChunk: mocks.uploadChunk,
  },
}));

vi.mock("./hash/hashing", () => ({
  createIncrementalHasher: vi.fn(),
  hashBuffer: mocks.hashBuffer,
  toWebCryptoAlgorithm: mocks.toWebCryptoAlgorithm,
}));

vi.mock("./hash/hashWorkerClient", () => ({
  canUseHashWorker: vi.fn(() => true),
  HashWorkerClient: class {},
}));

vi.mock("./hash/HashWorkerPool", () => ({
  globalHashWorkerPool: {
    acquire: mocks.acquire,
    release: mocks.release,
  },
}));

const createWorker = () => {
  let hashIndex = 0;
  return {
    hashChunk: vi.fn(async (buffer: ArrayBuffer) => {
      const chunkHash = `chunk-${hashIndex}`;
      hashIndex += 1;
      return { buffer, chunkHash };
    }),
    updateFileHash: vi.fn<(buffers: ArrayBuffer[]) => Promise<void>>(
      async () => undefined,
    ),
    digestFile: vi.fn(async () => "file-hash"),
  };
};

const createResponseError = (status: number): AxiosError => {
  const config: InternalAxiosRequestConfig = {
    headers: new AxiosHeaders(),
  };
  const response: AxiosResponse = {
    data: null,
    status,
    statusText: "Transient gateway failure",
    headers: new AxiosHeaders(),
    config,
  };
  return new AxiosError(
    response.statusText,
    AxiosError.ERR_BAD_RESPONSE,
    config,
    undefined,
    response,
  );
};

describe("uploadBlobToChunks", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    let hashIndex = 0;
    mocks.hashBuffer.mockImplementation(async () => {
      const hash = `chunk-${hashIndex}`;
      hashIndex += 1;
      return hash;
    });
    mocks.chunkExists.mockResolvedValue(true);
    mocks.uploadChunk.mockResolvedValue(undefined);
  });

  it("hashes chunks ahead of blocked network consumers", async () => {
    const worker = createWorker();
    const probeGate = createDeferred<void>();
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists.mockImplementation(async () => {
      await probeGate.promise;
      return true;
    });

    const upload = uploadBlobToChunks({
      blob: new Blob(["abcd"]),
      fileName: "bytes.bin",
      server: {
        maxChunkSizeBytes: 1,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 2 },
    });

    await vi.waitFor(() => {
      expect(mocks.hashBuffer).toHaveBeenCalledTimes(4);
      expect(mocks.chunkExists).toHaveBeenCalledTimes(2);
    });

    probeGate.resolve();
    await expect(upload).resolves.toEqual({
      chunkHashes: ["chunk-0", "chunk-1", "chunk-2", "chunk-3"],
      fileHash: "file-hash",
    });
    expect(worker.updateFileHash).toHaveBeenCalledOnce();
    await vi.waitFor(() => {
      expect(mocks.release).toHaveBeenCalledWith(worker);
    });
  });

  it("updates the whole-file hash in source order before enqueueing", async () => {
    const worker = createWorker();
    const hashedBytes: number[][] = [];
    mocks.acquire.mockResolvedValue(worker);
    mocks.hashBuffer.mockImplementation(async (buffer: ArrayBuffer) => {
      const byte = new Uint8Array(buffer)[0];
      await new Promise((resolve) => {
        globalThis.setTimeout(resolve, 5 - byte);
      });
      return `chunk-${byte}`;
    });
    worker.updateFileHash.mockImplementation(async (buffers: ArrayBuffer[]) => {
      hashedBytes.push(buffers.map((buffer) => new Uint8Array(buffer)[0]));
    });

    await expect(
      uploadBlobToChunks({
        blob: new Blob([new Uint8Array([1, 4, 2, 3])]),
        fileName: "ordered.bin",
        server: {
          maxChunkSizeBytes: 1,
          supportedHashAlgorithm: "sha256",
        },
        client: { concurrency: 2 },
      }),
    ).resolves.toEqual({
      chunkHashes: ["chunk-1", "chunk-4", "chunk-2", "chunk-3"],
      fileHash: "file-hash",
    });
    expect(hashedBytes).toEqual([[1, 4, 2, 3]]);
  });

  it("keeps only one whole-file hash batch in flight", async () => {
    const worker = createWorker();
    const firstFileHashGate = createDeferred<void>();
    let fileHashUpdateCount = 0;
    mocks.acquire.mockResolvedValue(worker);
    worker.updateFileHash.mockImplementation(async () => {
      fileHashUpdateCount += 1;
      if (fileHashUpdateCount === 1) {
        await firstFileHashGate.promise;
      }
    });

    const upload = uploadBlobToChunks({
      blob: new Blob([new Uint8Array(8)]),
      fileName: "bounded-hash.bin",
      server: {
        maxChunkSizeBytes: 1,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 2 },
    });

    await vi.waitFor(() => {
      expect(mocks.hashBuffer).toHaveBeenCalledTimes(8);
    });
    expect(worker.updateFileHash).toHaveBeenCalledOnce();

    firstFileHashGate.resolve();
    await expect(upload).resolves.toMatchObject({ fileHash: "file-hash" });
    expect(worker.updateFileHash).toHaveBeenCalledTimes(2);
  });

  it("keeps a fixed number of network consumers active", async () => {
    const worker = createWorker();
    const uploadGate = createDeferred<void>();
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists.mockResolvedValue(false);
    mocks.uploadChunk.mockImplementation(async () => uploadGate.promise);

    const upload = uploadBlobToChunks({
      blob: new Blob(["abcdefghijkl"]),
      fileName: "bytes.bin",
      server: {
        maxChunkSizeBytes: 1,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 8 },
    });

    await vi.waitFor(() => {
      expect(mocks.uploadChunk).toHaveBeenCalledTimes(8);
    });
    await new Promise((resolve) => {
      globalThis.setTimeout(resolve, 0);
    });
    expect(mocks.uploadChunk).toHaveBeenCalledTimes(8);

    uploadGate.resolve();
    await expect(upload).resolves.toMatchObject({
      chunkHashes: expect.arrayContaining(["chunk-0", "chunk-11"]),
      fileHash: "file-hash",
    });
    expect(mocks.uploadChunk).toHaveBeenCalledTimes(12);
  });

  it("applies backpressure when the prepared queue is full", async () => {
    const worker = createWorker();
    const probeGate = createDeferred<void>();
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists.mockImplementation(async () => {
      await probeGate.promise;
      return true;
    });

    const upload = uploadBlobToChunks({
      blob: new Blob(["abcdefghijklmnopqrstuvwxy"]),
      fileName: "bytes.bin",
      server: {
        maxChunkSizeBytes: 1,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 1 },
    });

    await vi.waitFor(() => {
      expect(mocks.hashBuffer).toHaveBeenCalledTimes(12);
    });
    await new Promise((resolve) => {
      globalThis.setTimeout(resolve, 0);
    });
    expect(mocks.hashBuffer).toHaveBeenCalledTimes(12);

    probeGate.resolve();
    await expect(upload).resolves.toMatchObject({ fileHash: "file-hash" });
    expect(mocks.hashBuffer).toHaveBeenCalledTimes(25);
  });

  it("reports parallel transport progress and confirms 100 percent last", async () => {
    const worker = createWorker();
    const uploadGates = [createDeferred<void>(), createDeferred<void>()];
    const snapshots: UploadProgressSnapshot[] = [];
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists.mockResolvedValue(false);
    mocks.uploadChunk.mockImplementation(async (options) => {
      const index = mocks.uploadChunk.mock.calls.length - 1;
      options.onProgress?.(2);
      await uploadGates[index].promise;
    });

    const upload = uploadBlobToChunks({
      blob: new Blob(["abcdefgh"]),
      fileName: "bytes.bin",
      server: {
        maxChunkSizeBytes: 4,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 2 },
      onProgress: (_bytesUploaded, snapshot) => {
        if (snapshot) {
          snapshots.push(snapshot);
        }
      },
    });

    await vi.waitFor(() => {
      expect(snapshots).toContainEqual({
        bytesUploaded: 4,
        bytesConfirmed: 0,
        bytesInFlight: 4,
        bytesTransmitted: 4,
      });
    });

    uploadGates[0].resolve();
    await vi.waitFor(() => {
      expect(snapshots).toContainEqual({
        bytesUploaded: 6,
        bytesConfirmed: 4,
        bytesInFlight: 2,
        bytesTransmitted: 6,
      });
    });

    uploadGates[1].resolve();
    await expect(upload).resolves.toEqual({
      chunkHashes: ["chunk-0", "chunk-1"],
      fileHash: "file-hash",
    });
    expect(snapshots.at(-1)).toEqual({
      bytesUploaded: 8,
      bytesConfirmed: 8,
      bytesInFlight: 0,
      bytesTransmitted: 8,
    });
    expect(snapshots.every((snapshot) => snapshot.bytesUploaded <= 8)).toBe(
      true,
    );
  });

  it("rehashes retry segments without restarting the whole-file hash", async () => {
    const worker = createWorker();
    const chunkBytes = 256 * 1024;
    let uploadCalls = 0;
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists.mockResolvedValue(false);
    mocks.uploadChunk.mockImplementation(async () => {
      uploadCalls += 1;
      if (uploadCalls === 1) {
        throw new AxiosError("interrupted", "ERR_NETWORK");
      }
    });

    const upload = uploadBlobToChunks({
      blob: new Blob([new Uint8Array(chunkBytes)]),
      fileName: "retry.bin",
      server: {
        maxChunkSizeBytes: chunkBytes,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 1 },
    });

    await expect(upload).resolves.toEqual({
      chunkHashes: ["chunk-1", "chunk-2"],
      fileHash: "file-hash",
    });
    expect(mocks.hashBuffer).toHaveBeenCalledTimes(3);
    expect(mocks.hashBuffer).toHaveBeenNthCalledWith(
      1,
      expect.any(ArrayBuffer),
      "SHA-256",
    );
    expect(mocks.hashBuffer).toHaveBeenNthCalledWith(
      2,
      expect.any(ArrayBuffer),
      "SHA-256",
    );
    expect(mocks.hashBuffer).toHaveBeenNthCalledWith(
      3,
      expect.any(ArrayBuffer),
      "SHA-256",
    );
    expect(worker.updateFileHash).toHaveBeenCalledOnce();
  });

  it("reuses the prepared minimum-size chunk across transport retries", async () => {
    const worker = createWorker();
    const chunkBytes = 128 * 1024;
    let uploadCalls = 0;
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists.mockResolvedValue(false);
    mocks.uploadChunk.mockImplementation(async () => {
      uploadCalls += 1;
      if (uploadCalls < 3) {
        throw new AxiosError("interrupted", "ERR_NETWORK");
      }
    });

    const upload = uploadBlobToChunks({
      blob: new Blob([new Uint8Array(chunkBytes)]),
      fileName: "minimum-retry.bin",
      server: {
        maxChunkSizeBytes: chunkBytes,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 1 },
    });

    await expect(upload).resolves.toEqual({
      chunkHashes: ["chunk-0"],
      fileHash: "file-hash",
    });
    expect(mocks.uploadChunk).toHaveBeenCalledTimes(3);
    expect(mocks.chunkExists).toHaveBeenCalledTimes(3);
    expect(mocks.hashBuffer).toHaveBeenCalledOnce();
    expect(worker.updateFileHash).toHaveBeenCalledOnce();
  });

  it("overlaps whole-file hashing with network work and awaits it before digest", async () => {
    const worker = createWorker();
    const fileHashGate = createDeferred<void>();
    worker.updateFileHash.mockImplementation(async () => {
      await fileHashGate.promise;
    });
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists.mockResolvedValue(false);

    const upload = uploadBlobToChunks({
      blob: new Blob([new Uint8Array(256 * 1024)]),
      fileName: "hash-gated.bin",
      server: {
        maxChunkSizeBytes: 256 * 1024,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 1 },
    });

    await vi.waitFor(() => {
      expect(worker.updateFileHash).toHaveBeenCalledOnce();
      expect(mocks.uploadChunk).toHaveBeenCalledOnce();
    });
    expect(worker.digestFile).not.toHaveBeenCalled();

    fileHashGate.resolve();
    await expect(upload).resolves.toEqual({
      chunkHashes: ["chunk-0"],
      fileHash: "file-hash",
    });
  });

  it("releases the worker after an upload failure", async () => {
    const worker = createWorker();
    const fileHashGate = createDeferred<void>();
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists.mockResolvedValue(false);
    mocks.uploadChunk.mockRejectedValue(new Error("upload failed"));
    worker.updateFileHash.mockImplementation(async () => {
      await fileHashGate.promise;
    });

    const upload = uploadBlobToChunks({
      blob: new Blob([new Uint8Array(256 * 1024)]),
      fileName: "failed-after-hash.bin",
      server: {
        maxChunkSizeBytes: 256 * 1024,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 1 },
    });

    await vi.waitFor(() => {
      expect(mocks.uploadChunk).toHaveBeenCalledOnce();
    });
    expect(mocks.release).not.toHaveBeenCalled();

    fileHashGate.resolve();
    await expect(upload).rejects.toThrow("upload failed");
    expect(mocks.release).toHaveBeenCalledWith(worker);
  });

  it("retries a transient existence gateway failure without rehashing", async () => {
    const worker = createWorker();
    mocks.acquire.mockResolvedValue(worker);
    mocks.chunkExists
      .mockRejectedValueOnce(createResponseError(502))
      .mockResolvedValueOnce(false);

    const upload = uploadBlobToChunks({
      blob: new Blob([new Uint8Array(256 * 1024)]),
      fileName: "gateway-retry.bin",
      server: {
        maxChunkSizeBytes: 256 * 1024,
        supportedHashAlgorithm: "sha256",
      },
      client: { concurrency: 1 },
    });

    await expect(upload).resolves.toEqual({
      chunkHashes: ["chunk-0"],
      fileHash: "file-hash",
    });
    expect(mocks.chunkExists).toHaveBeenCalledTimes(2);
    expect(mocks.uploadChunk).toHaveBeenCalledOnce();
    expect(worker.hashChunk).not.toHaveBeenCalled();
    expect(mocks.hashBuffer).toHaveBeenCalledOnce();
    expect(worker.updateFileHash).toHaveBeenCalledOnce();
  });
});
