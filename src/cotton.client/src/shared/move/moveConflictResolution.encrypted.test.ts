import { beforeEach, describe, expect, it, vi } from "vitest";
import type { MoveFileRequest } from "../api/filesApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  DISPLAY_META_KEY,
  decryptDisplayMeta,
  encryptDisplayMeta,
} from "../crypto/displayMeta";
import { ENCRYPTED_FLAG_KEY } from "../crypto/fileCipher";
import { encryptFolderName } from "../crypto/folderDisplayMeta";
import { generateMasterKey } from "../crypto/keys";
import { useVault } from "../crypto/vault";
import { moveCandidatesToTarget } from "../hooks/fileTransferExecution";
import { ConflictAction } from "../types/nameConflict";
import { moveItemWithConflictResolution } from "./moveConflictResolution";

const mocks = vi.hoisted(() => ({
  copy: vi.fn(),
  move: vi.fn(),
  children: vi.fn(),
}));
vi.mock("../api/filesApi", () => ({
  filesApi: { copyFile: mocks.copy, moveFile: mocks.move },
}));
vi.mock("../api/nodesApi", () => ({
  nodesApi: { getChildren: mocks.children },
}));

const source: NodeFileManifestDto = {
  id: "source-file",
  nodeId: "source",
  ownerId: "user",
  name: "opaque-source",
  contentType: "application/octet-stream",
  sizeBytes: 12,
  metadata: {},
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
};
const emptyContent = {
  id: "content",
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
  nodes: [],
  files: [],
};

async function encryptedFile(
  name = "report.txt",
  id = source.id,
): Promise<NodeFileManifestDto> {
  return {
    ...source,
    id,
    name: `opaque-${id}`,
    metadata: {
      [ENCRYPTED_FLAG_KEY]: "true",
      [DISPLAY_META_KEY]: await encryptDisplayMeta({
        name,
        contentType: "text/plain",
      }),
    },
  };
}

const transferItem = (file: NodeFileManifestDto) => ({
  id: file.id,
  kind: "file" as const,
  sourceParentId: file.nodeId,
  file,
});

function simulateTransfer(
  file: NodeFileManifestDto,
  request: MoveFileRequest,
  id: string,
): NodeFileManifestDto {
  return {
    ...file,
    id,
    nodeId: request.parentId,
    name: request.name ?? file.name,
    metadata: { ...file.metadata, ...request.metadata },
  };
}

beforeEach(async () => {
  vi.resetAllMocks();
  useVault.setState({ isUnlocked: true, masterKey: await generateMasterKey() });
  mocks.children.mockResolvedValue({ content: emptyContent, totalCount: 0 });
});

describe("encrypted file name conflicts", () => {
  it("renames a same-folder copy in encrypted metadata without changing its source", async () => {
    const file = await encryptedFile();
    const next = await encryptedFile("report (1).txt", "next");
    mocks.children.mockResolvedValue({
      content: { ...emptyContent, files: [file, next] },
      totalCount: 2,
    });
    mocks.copy.mockImplementation((_id: string, request: MoveFileRequest) =>
      Promise.resolve(simulateTransfer(file, request, "copy")),
    );
    const confirm = vi.fn(async () => ConflictAction.Rename);

    const result = await moveItemWithConflictResolution({
      item: transferItem(file),
      operation: "copy",
      targetParentId: file.nodeId,
      skipAllConflicts: false,
      confirmConflict: confirm,
    });

    expect(confirm).toHaveBeenCalledWith({
      newName: "report (2).txt",
      canOverwrite: false,
    });
    expect(mocks.copy).toHaveBeenCalledOnce();
    const request: MoveFileRequest = mocks.copy.mock.calls[0][1];
    expect(request.name).toMatch(/^[a-f0-9-]{36}$/);
    expect(
      await decryptDisplayMeta(request.metadata?.[DISPLAY_META_KEY] ?? ""),
    ).toEqual({
      name: "report (2).txt",
      contentType: "text/plain",
    });
    expect(await decryptDisplayMeta(file.metadata[DISPLAY_META_KEY])).toEqual({
      name: "report.txt",
      contentType: "text/plain",
    });
    expect(result.kind).toBe("moved");
    expect(mocks.children).toHaveBeenCalledOnce();
  });

  it("renames an encrypted move using metadata while preserving its opaque server name", async () => {
    const file = await encryptedFile();
    const existing = await encryptedFile("REPORT.txt", "existing");
    mocks.children.mockResolvedValue({
      content: { ...emptyContent, files: [existing] },
      totalCount: 1,
    });
    mocks.move.mockImplementation((_id: string, request: MoveFileRequest) =>
      Promise.resolve(simulateTransfer(file, request, file.id)),
    );
    const result = await moveItemWithConflictResolution({
      item: transferItem(file),
      targetParentId: "target",
      skipAllConflicts: false,
      confirmConflict: vi.fn(async () => ConflictAction.Rename),
    });
    const request: MoveFileRequest = mocks.move.mock.calls[0][1];
    expect(request.name).toBeUndefined();
    expect(
      await decryptDisplayMeta(request.metadata?.[DISPLAY_META_KEY] ?? ""),
    ).toEqual({
      name: "report (1).txt",
      contentType: "text/plain",
    });
    expect(result.kind).toBe("moved");
  });

  it.each(["copy", "move"] as const)(
    "replaces the selected encrypted conflict on %s using its server name",
    async (operation) => {
      const file = await encryptedFile();
      const existing = await encryptedFile("report.txt", "existing");
      mocks.children.mockResolvedValue({
        content: { ...emptyContent, files: [existing] },
        totalCount: 1,
      });
      const send = operation === "copy" ? mocks.copy : mocks.move;
      send.mockImplementation((_id: string, request: MoveFileRequest) =>
        Promise.resolve(simulateTransfer(file, request, "transferred")),
      );
      const result = await moveItemWithConflictResolution({
        item: transferItem(file),
        operation,
        targetParentId: "target",
        skipAllConflicts: false,
        confirmConflict: vi.fn(async () => ConflictAction.Overwrite),
      });
      expect(send).toHaveBeenCalledWith(
        file.id,
        expect.objectContaining({
          parentId: "target",
          name: existing.name,
          overwrite: true,
        }),
      );
      expect(result.kind).toBe("moved");
    },
  );

  it("reserves names between files in a batch without reloading the destination", async () => {
    const first = await encryptedFile();
    const second = await encryptedFile("report.txt", "second");
    mocks.copy.mockImplementation((id: string, request: MoveFileRequest) => {
      const file = id === first.id ? first : second;
      return Promise.resolve(simulateTransfer(file, request, `copy-${id}`));
    });
    const result = await moveCandidatesToTarget({
      operation: "copy",
      candidates: [transferItem(first), transferItem(second)],
      targetParentId: "target",
      targetEncryptsNewFiles: true,
      confirmConflict: vi.fn(async () => ConflictAction.Rename),
    });
    expect(result.succeeded).toHaveLength(2);
    expect(mocks.children).toHaveBeenCalledOnce();
    const request: MoveFileRequest = mocks.copy.mock.calls[1][1];
    expect(
      await decryptDisplayMeta(request.metadata?.[DISPLAY_META_KEY] ?? ""),
    ).toEqual({
      name: "report (1).txt",
      contentType: "text/plain",
    });
  });

  it.each([ConflictAction.Skip, ConflictAction.Cancel])(
    "does not submit a rejected file on %s",
    async (action) => {
      const file = await encryptedFile();
      mocks.children.mockResolvedValue({
        content: { ...emptyContent, files: [file] },
        totalCount: 1,
      });
      await moveItemWithConflictResolution({
        item: transferItem(file),
        operation: "copy",
        targetParentId: file.nodeId,
        skipAllConflicts: false,
        confirmConflict: vi.fn(async () => action),
      });
      expect(mocks.copy).not.toHaveBeenCalled();
    },
  );

  it("checks encrypted folders and plaintext files in the same display-name namespace", async () => {
    const file = await encryptedFile();
    mocks.children.mockResolvedValue({
      content: {
        ...emptyContent,
        files: [{ ...source, id: "plain", name: "report (1).txt" }],
        nodes: [
          {
            id: "folder",
            name: "opaque-folder",
            parentId: "target",
            layoutId: "layout",
            createdAt: "2026-01-01",
            updatedAt: "2026-01-01",
            metadata: {
              [DISPLAY_META_KEY]: await encryptFolderName("report.txt"),
            },
          },
        ],
      },
      totalCount: 2,
    });
    const confirm = vi.fn(async () => ConflictAction.Skip);
    await moveItemWithConflictResolution({
      item: transferItem(file),
      operation: "copy",
      targetParentId: "target",
      skipAllConflicts: false,
      confirmConflict: confirm,
    });
    expect(confirm).toHaveBeenCalledWith({
      newName: "report (2).txt",
      canOverwrite: false,
    });
  });

  it("does not transfer encrypted files while the key is locked", async () => {
    const file = await encryptedFile();
    useVault.setState({ isUnlocked: false, masterKey: null });
    const result = await moveItemWithConflictResolution({
      item: transferItem(file),
      operation: "copy",
      targetParentId: "target",
      skipAllConflicts: false,
      confirmConflict: vi.fn(),
    });
    expect(result.kind).toBe("failed");
    expect(mocks.copy).not.toHaveBeenCalled();
  });

  it("keeps an encrypted copy opaque when its display name conflicts with plaintext", async () => {
    const file = await encryptedFile();
    mocks.children.mockResolvedValue({
      content: {
        ...emptyContent,
        files: [{ ...source, id: "plain", name: "report.txt" }],
      },
      totalCount: 1,
    });
    mocks.copy.mockImplementation((_id: string, request: MoveFileRequest) =>
      Promise.resolve(simulateTransfer(file, request, "copy")),
    );
    const confirm = vi.fn(async () => ConflictAction.Rename);
    await moveItemWithConflictResolution({
      item: transferItem(file),
      operation: "copy",
      targetParentId: "target",
      skipAllConflicts: false,
      confirmConflict: confirm,
    });
    expect(confirm).toHaveBeenCalledWith({
      newName: "report (1).txt",
      canOverwrite: false,
    });
    expect(mocks.copy).toHaveBeenCalledWith(
      file.id,
      expect.objectContaining({
        name: expect.stringMatching(/^[a-f0-9-]{36}$/),
      }),
    );
  });

  it("keeps plaintext transfers free of display-name lookup requests", async () => {
    mocks.copy.mockResolvedValue(source);
    await moveItemWithConflictResolution({
      item: transferItem(source),
      operation: "copy",
      targetParentId: "target",
      skipAllConflicts: false,
      confirmConflict: vi.fn(),
    });
    expect(mocks.children).not.toHaveBeenCalled();
    expect(mocks.copy).toHaveBeenCalledWith(source.id, { parentId: "target" });
  });
});
