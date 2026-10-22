import { beforeEach, expect, it, vi } from "vitest";
import type { MoveFileRequest } from "../api/filesApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import { DISPLAY_META_KEY, encryptDisplayMeta } from "../crypto/displayMeta";
import { ENCRYPTED_FLAG_KEY, encryptFileToBlob } from "../crypto/fileCipher";
import { generateMasterKey } from "../crypto/keys";
import { requireMasterKey, useVault } from "../crypto/vault";
import { ConflictAction } from "../types/nameConflict";
import { decryptExistingFileInPlace } from "../upload/decryptExistingFileInPlace";
import { moveCandidatesToTarget } from "./fileTransferExecution";

const mocks = vi.hoisted(() => ({
  move: vi.fn(),
  children: vi.fn(),
  download: vi.fn(),
  update: vi.fn(),
  upload: vi.fn(),
}));
vi.mock("../api/filesApi", () => ({
  filesApi: {
    moveFile: mocks.move,
    getDownloadLink: mocks.download,
    updateFileContent: mocks.update,
  },
}));
vi.mock("../api/nodesApi", () => ({
  nodesApi: { getChildren: mocks.children },
}));
vi.mock("../upload/uploadBlobToChunks", () => ({
  uploadBlobToChunks: mocks.upload,
}));

const file: NodeFileManifestDto = {
  id: "source-file",
  name: "opaque-source",
  nodeId: "source",
  ownerId: "user",
  contentType: "application/octet-stream",
  sizeBytes: 0,
  metadata: {},
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
};

beforeEach(async () => {
  vi.resetAllMocks();
  useVault.setState({ isUnlocked: true, masterKey: await generateMasterKey() });
});

it("decrypts a moved encrypted file under the name chosen during conflict resolution", async () => {
  const plaintext = "The original file content";
  const ciphertext = await encryptFileToBlob(
    new Blob([plaintext]),
    requireMasterKey(),
  );
  const source: NodeFileManifestDto = {
    ...file,
    sizeBytes: ciphertext.size,
    metadata: {
      [ENCRYPTED_FLAG_KEY]: "true",
      [DISPLAY_META_KEY]: await encryptDisplayMeta({
        name: "report.txt",
        contentType: "text/plain",
      }),
    },
  };
  mocks.children.mockResolvedValue({
    content: {
      id: "target",
      createdAt: "2026-01-01",
      updatedAt: "2026-01-01",
      nodes: [],
      files: [{ ...source, id: "existing", name: "opaque-existing" }],
    },
    totalCount: 1,
  });
  mocks.move.mockImplementation(
    async (_id: string, request: MoveFileRequest) => ({
      ...source,
      nodeId: request.parentId,
      metadata: { ...source.metadata, ...request.metadata },
    }),
  );
  const result = await moveCandidatesToTarget({
    operation: "move",
    candidates: [
      {
        id: source.id,
        kind: "file",
        sourceParentId: source.nodeId,
        file: source,
      },
    ],
    targetParentId: "target",
    targetEncryptsNewFiles: false,
    confirmConflict: vi.fn(async () => ConflictAction.Rename),
  });
  expect(result.movedFilesToOfferDecrypt).toHaveLength(1);
  expect(result.sourceParents).toEqual(new Set(["source"]));
  const transferred = result.movedFilesToOfferDecrypt[0];
  expect(transferred.file).toBeDefined();
  if (!transferred.file) {
    throw new Error("Transferred file is missing.");
  }
  mocks.download.mockResolvedValue("https://download.test/file");
  mocks.upload.mockResolvedValue({ chunkHashes: ["chunk"], fileHash: "hash" });
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => ({ ok: true, blob: async () => ciphertext })),
  );
  try {
    await decryptExistingFileInPlace({
      file: { ...transferred.file, id: transferred.id },
      targetNodeId: "target",
      server: { maxChunkSizeBytes: 1024, supportedHashAlgorithm: "SHA-256" },
    });
  } finally {
    vi.unstubAllGlobals();
  }

  expect(mocks.update).toHaveBeenCalledWith(
    source.id,
    expect.objectContaining({
      name: "report (1).txt",
      contentType: "text/plain",
      nodeId: "target",
      metadata: {},
    }),
  );
  expect(mocks.upload).toHaveBeenCalledWith(
    expect.objectContaining({ fileName: "report (1).txt" }),
  );
  const uploaded: { blob: Blob } = mocks.upload.mock.calls[0][0];
  expect(await uploaded.blob.text()).toBe(plaintext);
});
