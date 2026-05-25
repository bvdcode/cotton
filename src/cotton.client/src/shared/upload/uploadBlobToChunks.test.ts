import { beforeEach, describe, expect, it, vi } from "vitest";
import { uploadBlobToChunks } from "./uploadBlobToChunks";

const mocks = vi.hoisted(() => ({
  acquire: vi.fn(),
  release: vi.fn(),
  chunkExists: vi.fn(),
  uploadChunk: vi.fn(),
  hashBuffer: vi.fn(),
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
  toWebCryptoAlgorithm: vi.fn(() => "SHA-256"),
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

describe("uploadBlobToChunks", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.chunkExists.mockResolvedValue(true);
    mocks.uploadChunk.mockResolvedValue(undefined);
    mocks.hashBuffer.mockResolvedValue("chunk-hash");
  });

  it("does not block chunk existence probes behind sequential file hashing", async () => {
    let releaseFileHashUpdate: () => void = () => {
      throw new Error("file hash update was not queued");
    };
    const worker = {
      updateFileHash: vi.fn(() => new Promise<void>((resolve) => {
        releaseFileHashUpdate = resolve;
      })),
      digestFile: vi.fn(async () => "file-hash"),
    };
    mocks.acquire.mockResolvedValue(worker);

    const upload = uploadBlobToChunks({
      blob: new Blob(["hello world"]),
      fileName: "hello.txt",
      server: {
        maxChunkSizeBytes: 1024,
        supportedHashAlgorithm: "sha256",
      },
    });

    await vi.waitFor(() => {
      expect(mocks.chunkExists).toHaveBeenCalledWith(
        "chunk-hash",
        expect.any(AbortSignal),
      );
    });

    expect(worker.updateFileHash).toHaveBeenCalledOnce();
    expect(worker.digestFile).not.toHaveBeenCalled();

    releaseFileHashUpdate();

    await expect(upload).resolves.toEqual({
      chunkHashes: ["chunk-hash"],
      fileHash: "file-hash",
    });
    expect(worker.digestFile).toHaveBeenCalledOnce();
    expect(mocks.release).toHaveBeenCalledWith(worker);
  });
});
