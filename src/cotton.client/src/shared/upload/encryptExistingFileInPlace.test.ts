import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { filesApi } from "../api/filesApi";
import {
  DISPLAY_META_KEY,
  ENCRYPTED_CONTENT_TYPE,
  ENCRYPTED_FLAG_KEY,
  encryptDisplayMeta,
  encryptFileToBlob,
} from "../crypto";
import { uploadBlobToChunks } from "./uploadBlobToChunks";
import { encryptExistingFileInPlace } from "./encryptExistingFileInPlace";

vi.mock("../api/filesApi", () => ({
  filesApi: {
    getDownloadLink: vi.fn(),
    updateFileContent: vi.fn(),
  },
}));

vi.mock("../crypto", () => ({
  DISPLAY_META_KEY: "ctn.displayMeta",
  ENCRYPTED_CONTENT_TYPE: "application/vnd.cotton.encrypted",
  ENCRYPTED_FLAG_KEY: "isClientEncrypted",
  assertClientEncryptionBlobPipelineSize: vi.fn(),
  requireMasterKey: vi.fn(() => ({})),
  encryptDisplayMeta: vi.fn(async () => "encrypted-display-meta"),
  encryptFileToBlob: vi.fn(async () => new Blob(["encrypted"])),
}));

vi.mock("./uploadBlobToChunks", () => ({
  uploadBlobToChunks: vi.fn(async () => ({
    chunkHashes: ["hash-1"],
    fileHash: "file-hash",
  })),
}));

const server = {
  maxChunkSizeBytes: 1024,
  supportedHashAlgorithm: "sha256",
};

describe("encryptExistingFileInPlace", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(filesApi.getDownloadLink).mockResolvedValue("/download/plain");
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({
        ok: true,
        blob: async () => new Blob(["plain"], { type: "text/plain" }),
      })),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("replaces a plaintext file with an encrypted container and display metadata", async () => {
    await encryptExistingFileInPlace({
      file: {
        id: "file-1",
        name: "report.txt",
        contentType: "text/plain",
        sizeBytes: 5,
      },
      targetNodeId: "folder-1",
      server,
    });

    expect(filesApi.getDownloadLink).toHaveBeenCalledWith("file-1");
    expect(fetch).toHaveBeenCalledWith("/download/plain");
    expect(encryptDisplayMeta).toHaveBeenCalledWith({
      name: "report.txt",
      contentType: "text/plain",
    });
    expect(encryptFileToBlob).toHaveBeenCalled();
    expect(uploadBlobToChunks).toHaveBeenCalledWith(
      expect.objectContaining({
        fileName: "report.txt",
        server,
      }),
    );
    expect(filesApi.updateFileContent).toHaveBeenCalledWith(
      "file-1",
      expect.objectContaining({
        nodeId: "folder-1",
        chunkHashes: ["hash-1"],
        contentType: ENCRYPTED_CONTENT_TYPE,
        hash: "file-hash",
        metadata: {
          [ENCRYPTED_FLAG_KEY]: "true",
          [DISPLAY_META_KEY]: "encrypted-display-meta",
        },
      }),
    );
    const request = vi.mocked(filesApi.updateFileContent).mock.calls[0]?.[1];
    expect(request?.name).not.toBe("report.txt");
  });
});
