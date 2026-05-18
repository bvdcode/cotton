import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { filesApi } from "../api/filesApi";
import {
  CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
  ClientEncryptionSizeLimitError,
  decryptDisplayMeta,
  DISPLAY_META_KEY,
  ENCRYPTED_CONTENT_TYPE,
  ENCRYPTED_FLAG_KEY,
  ORIGINAL_CONTENT_TYPE_KEY,
  generateMasterKey,
  useVault,
} from "../crypto";
import { uploadBlobToChunks } from "./uploadBlobToChunks";
import { uploadFileToNode } from "./uploadFileToNode";

const createFromChunksMock = vi.mocked(filesApi.createFromChunks);
const uploadBlobToChunksMock = vi.mocked(uploadBlobToChunks);
const originalRandomUUID = globalThis.crypto.randomUUID;

vi.mock("../api/filesApi", () => ({
  filesApi: {
    createFromChunks: vi.fn(),
  },
}));

vi.mock("./uploadBlobToChunks", () => ({
  uploadBlobToChunks: vi.fn(),
}));

describe("uploadFileToNode", () => {
  beforeEach(() => {
    useVault.getState().lock();
    createFromChunksMock.mockReset();
    uploadBlobToChunksMock.mockReset();
    uploadBlobToChunksMock.mockResolvedValue({
      chunkHashes: ["chunk-a"],
      fileHash: "file-hash",
    });
    Object.defineProperty(globalThis.crypto, "randomUUID", {
      configurable: true,
      value: vi.fn(() => "11111111-2222-4333-8444-555555555555"),
    });
  });

  afterEach(() => {
    Object.defineProperty(globalThis.crypto, "randomUUID", {
      configurable: true,
      value: originalRandomUUID,
    });
    vi.restoreAllMocks();
    useVault.getState().lock();
  });

  it("keeps plain uploads on the visible server name and content type", async () => {
    const file = new File(["hello"], "plain.txt", { type: "text/plain" });

    await uploadFileToNode({
      file,
      nodeId: "node-1",
      server: { maxChunkSizeBytes: 1024, supportedHashAlgorithm: "sha256" },
    });

    expect(uploadBlobToChunksMock).toHaveBeenCalledWith(
      expect.objectContaining({
        blob: file,
        fileName: "plain.txt",
      }),
    );
    expect(createFromChunksMock).toHaveBeenCalledWith({
      nodeId: "node-1",
      chunkHashes: ["chunk-a"],
      name: "plain.txt",
      contentType: "text/plain",
      hash: "file-hash",
      originalNodeFileId: null,
      metadata: undefined,
    });
  });

  it("stores encrypted uploads under an opaque server name with encrypted display metadata", async () => {
    const masterKey = await generateMasterKey();
    const file = new File(["secret"], "private.pdf", {
      type: "application/pdf",
    });
    const onEncryptProgress = vi.fn();
    const onEncryptComplete = vi.fn();

    useVault.getState().unlock(masterKey);

    await uploadFileToNode({
      file,
      nodeId: "node-1",
      server: { maxChunkSizeBytes: 1024, supportedHashAlgorithm: "sha256" },
      encrypt: true,
      onEncryptProgress,
      onEncryptComplete,
    });

    const request = createFromChunksMock.mock.calls[0]?.[0];
    expect(request).toMatchObject({
      nodeId: "node-1",
      chunkHashes: ["chunk-a"],
      name: "11111111-2222-4333-8444-555555555555",
      contentType: ENCRYPTED_CONTENT_TYPE,
      hash: "file-hash",
      originalNodeFileId: null,
    });
    expect(request?.metadata?.[ENCRYPTED_FLAG_KEY]).toBe("true");
    expect(request?.metadata?.[ORIGINAL_CONTENT_TYPE_KEY]).toBeUndefined();
    expect(request?.metadata?.[DISPLAY_META_KEY]).toEqual(expect.any(String));

    await expect(
      decryptDisplayMeta(request?.metadata?.[DISPLAY_META_KEY] ?? ""),
    ).resolves.toEqual({
      name: "private.pdf",
      contentType: "application/pdf",
    });

    expect(uploadBlobToChunksMock.mock.calls[0]?.[0].blob).not.toBe(file);
    expect(onEncryptProgress).toHaveBeenCalledWith(0, file.size);
    expect(onEncryptProgress).toHaveBeenLastCalledWith(file.size, file.size);
    expect(onEncryptComplete).toHaveBeenCalledOnce();
  });

  it("rejects oversized encrypted uploads before reading or uploading bytes", async () => {
    const file = new File(["x"], "huge.bin", {
      type: "application/octet-stream",
    });
    Object.defineProperty(file, "size", {
      configurable: true,
      value: CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES + 1,
    });

    await expect(
      uploadFileToNode({
        file,
        nodeId: "node-1",
        server: { maxChunkSizeBytes: 1024, supportedHashAlgorithm: "sha256" },
        encrypt: true,
      }),
    ).rejects.toBeInstanceOf(ClientEncryptionSizeLimitError);

    expect(uploadBlobToChunksMock).not.toHaveBeenCalled();
    expect(createFromChunksMock).not.toHaveBeenCalled();
  });
});
