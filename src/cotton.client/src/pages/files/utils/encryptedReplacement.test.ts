import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { resolveUploadConflicts } from "./uploadConflicts";
import { createFile } from "../../../test/fileFixtures";
import { filesApi } from "../../../shared/api/filesApi";
import {
  decryptBlobToBlob,
  decryptDisplayMeta,
  encryptDisplayMeta,
  generateMasterKey,
  useVault,
} from "../../../shared/crypto";
import { ConflictAction } from "../../../shared/types/nameConflict";
import { uploadBlobToChunks } from "../../../shared/upload/uploadBlobToChunks";
import { uploadFileToNode } from "../../../shared/upload/uploadFileToNode";

vi.mock("../../../shared/api/filesApi", () => ({
  filesApi: { createFromChunks: vi.fn(), updateFileContent: vi.fn() },
}));
vi.mock("../../../shared/upload/uploadBlobToChunks", () => ({ uploadBlobToChunks: vi.fn() }));

const server = { maxChunkSizeBytes: 1024, supportedHashAlgorithm: "sha256" };

async function prepareReplacement(action: ConflictAction = ConflictAction.Overwrite) {
  const file = new File(["new private contents"], "private.txt", {
    type: "text/plain",
  });
  const metadata = {
    isClientEncrypted: "true",
    en: await encryptDisplayMeta({
      name: file.name,
      contentType: file.type,
    }),
  };
  return resolveUploadConflicts(
    [file],
    {
      nodes: [],
      files: [createFile({ id: "encrypted-file", name: file.name, metadata })],
      takenNameKeys: [file.name],
    },
    async () => action,
  );
}

describe("encrypted file replacement in a plain folder", () => {
  beforeEach(() => {
    vi.mocked(uploadBlobToChunks).mockResolvedValue({
      chunkHashes: ["encrypted-chunk"],
      fileHash: "encrypted-hash",
    });
  });

  afterEach(() => {
    vi.resetAllMocks();
    useVault.getState().lock();
  });

  it("uploads decryptable encrypted content and matching display metadata", async () => {
    const key = await generateMasterKey();
    useVault.getState().unlock(key, { persistToSession: false });
    const result = await prepareReplacement();
    const replacement = result.files[0];
    expect(replacement.encrypt).toBe(true);

    await uploadFileToNode({ ...replacement, nodeId: "plain-folder", server });

    const uploaded = vi.mocked(uploadBlobToChunks).mock.calls[0][0].blob;
    const [id, request] = vi.mocked(filesApi.updateFileContent).mock.calls[0];
    expect(id).toBe("encrypted-file");
    expect(request.metadata?.isClientEncrypted).toBe("true");
    expect(request.name).not.toBe(replacement.file.name);
    expect(request.contentType).toBe("application/octet-stream");
    expect(await decryptDisplayMeta(request.metadata?.en ?? "")).toEqual({
      name: replacement.file.name,
      contentType: replacement.file.type,
    });
    const decrypted = await decryptBlobToBlob(uploaded, key);
    expect(await decrypted.text()).toBe(await replacement.file.text());
    expect(filesApi.createFromChunks).not.toHaveBeenCalled();
  });

  it("sends no content if the key is locked after resolving the conflict", async () => {
    useVault.getState().unlock(await generateMasterKey(), {
      persistToSession: false,
    });
    const result = await prepareReplacement();
    useVault.getState().lock();

    await expect(
      uploadFileToNode({ ...result.files[0], nodeId: "plain-folder", server }),
    ).rejects.toThrow();

    expect(uploadBlobToChunks).not.toHaveBeenCalled();
    expect(filesApi.updateFileContent).not.toHaveBeenCalled();
  });

  it("keeps a renamed upload on the ordinary folder policy", async () => {
    useVault.getState().unlock(await generateMasterKey(), {
      persistToSession: false,
    });
    const result = await prepareReplacement(ConflictAction.Rename);

    expect(result.files[0].replaceNodeFileId).toBeUndefined();
    expect(result.files[0].encrypt).toBeUndefined();
    expect(result.files[0].file.name).toBe("private (1).txt");
  });
});
