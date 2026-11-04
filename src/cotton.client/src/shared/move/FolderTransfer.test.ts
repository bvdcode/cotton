import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { generateMasterKey } from "../crypto/keys";
import { useVault } from "../crypto/vault";
import { DISPLAY_META_KEY, encryptDisplayMeta } from "../crypto/displayMeta";
import { encryptFolderName } from "../crypto/folderDisplayMeta";
import { ENCRYPTED_FLAG_KEY } from "../crypto/fileCipher";
import { FOLDER_ENCRYPTION_POLICY_KEY } from "../crypto/metadataFlags";
import { useNodesStore } from "../store/nodesStore";
import type { MoveClipboardItem } from "../store/moveClipboardStore";
import { moveCandidatesToTarget } from "../hooks/fileTransferExecution";
import { ConflictAction } from "../types/nameConflict";
import { TransferFixture, conflict } from "./folderMergeFixtures";
import type { MoveConflictResolver } from "./moveConflictResolution";

const mocks = vi.hoisted(() => ({
  getNode: vi.fn(),
  getChildren: vi.fn(),
  getAncestors: vi.fn(),
  moveNode: vi.fn(),
  copyNode: vi.fn(),
  moveFile: vi.fn(),
  copyFile: vi.fn(),
  deleteNode: vi.fn(),
}));
vi.mock("../api/nodesApi", () => ({ nodesApi: mocks }));
vi.mock("../api/filesApi", () => ({ filesApi: mocks }));

let fixture: TransferFixture;
const item: MoveClipboardItem = {
  id: "source",
  kind: "folder",
  sourceParentId: "from",
};
const options = (
  confirmConflict: MoveConflictResolver = vi.fn(
    async () => ConflictAction.Merge,
  ),
) => ({
  candidates: [item],
  operation: "move" as const,
  targetParentId: "to",
  targetEncryptsNewFiles: false,
  confirmConflict,
});

beforeEach(() => {
  vi.resetAllMocks();
  useVault.getState().lock();
  useNodesStore.setState({ contentByNodeId: {}, ancestorsByNodeId: {} });
  fixture = new TransferFixture();
  fixture.folder("root", null);
  fixture.folder("from", "root");
  fixture.folder("to", "root");
  fixture.folder("source", "from", "Books");
  fixture.folder("destination", "to", "Books");
  mocks.getNode.mockImplementation(fixture.getNode);
  mocks.getChildren.mockImplementation(fixture.getChildren);
  mocks.getAncestors.mockImplementation(fixture.getAncestors);
  mocks.moveNode.mockImplementation(fixture.moveNode);
  mocks.copyNode.mockImplementation(fixture.copyNode);
  mocks.moveFile.mockImplementation(fixture.moveFile);
  mocks.copyFile.mockImplementation(fixture.copyFile);
  mocks.deleteNode.mockImplementation(fixture.deleteNode);
});
afterEach(() => useVault.getState().lock());

describe("folder merging", () => {
  it("merges matching nested folders and preserves unrelated destination files", async () => {
    fixture.folder("source-child", "source", "Nested");
    fixture.folder("dest-child", "destination", "Nested");
    fixture.file("first", "source-child");
    fixture.file("second", "source");
    fixture.file("untouched", "destination");
    const confirm = vi.fn(async () => ConflictAction.Merge);
    const result = await moveCandidatesToTarget(options(confirm));
    expect(result.succeeded).toEqual([item]);
    expect(result.notMoved).toEqual([]);
    expect(fixture.files.get("first")?.nodeId).toBe("dest-child");
    expect(fixture.files.get("second")?.nodeId).toBe("destination");
    expect(fixture.files.get("untouched")?.nodeId).toBe("destination");
    expect(mocks.deleteNode.mock.calls).toEqual([
      ["source-child", false, true],
      ["source", false, true],
    ]);
    expect(confirm.mock.calls).toEqual([
      [
        {
          newName: "Books (1)",
          canMerge: true,
          canOverwrite: false,
          canApplyToAll: false,
        },
      ],
    ]);
  });

  it("copies into matching nested folders without removing the source", async () => {
    fixture.folder("source-child", "source", "Nested");
    fixture.folder("dest-child", "destination", "Nested");
    fixture.file("first", "source-child");
    const result = await moveCandidatesToTarget({
      ...options(),
      operation: "copy",
    });
    expect(result.failed).toEqual([]);
    expect(result.succeeded).toHaveLength(1);
    expect(fixture.files.get("first")?.nodeId).toBe("source-child");
    expect(fixture.files.get("copy-first")?.nodeId).toBe("dest-child");
    expect(fixture.folders.has("source")).toBe(true);
    expect(mocks.deleteNode).not.toHaveBeenCalled();
  });

  it("leaves skipped files in the source while moving other files", async () => {
    fixture.file("conflicting", "source", "same.txt");
    fixture.file("existing", "destination", "same.txt");
    fixture.file("other", "source");
    const confirm = vi
      .fn()
      .mockResolvedValueOnce(ConflictAction.Merge)
      .mockResolvedValueOnce(ConflictAction.Skip);
    const result = await moveCandidatesToTarget(options(confirm));
    expect(result.notMoved).toEqual([item]);
    expect(result.failed).toEqual([]);
    expect(fixture.files.get("conflicting")?.nodeId).toBe("source");
    expect(fixture.files.get("other")?.nodeId).toBe("destination");
    expect(mocks.deleteNode).not.toHaveBeenCalled();
    expect(confirm.mock.calls[1][0]).toEqual({
      newName: "same (1).txt",
      canMerge: false,
      canOverwrite: true,
      canApplyToAll: true,
    });
  });

  it("cancels nested processing and keeps remaining clipboard roots", async () => {
    fixture.file("conflicting", "source", "same.txt");
    fixture.file("existing", "destination", "same.txt");
    fixture.file("later", "source");
    fixture.folder("another", "from");
    const another: MoveClipboardItem = { ...item, id: "another" };
    const confirm = vi
      .fn()
      .mockResolvedValueOnce(ConflictAction.Merge)
      .mockResolvedValueOnce(ConflictAction.Cancel);
    const result = await moveCandidatesToTarget({
      ...options(confirm),
      candidates: [item, another],
    });
    expect(result.notMoved).toEqual([item, another]);
    expect(fixture.files.get("later")?.nodeId).toBe("source");
    expect(fixture.folders.get("another")?.parentId).toBe("from");
    expect(mocks.deleteNode).not.toHaveBeenCalled();
  });

  it("applies overwrite-all to file conflicts across nested folders", async () => {
    fixture.folder("source-child", "source", "Nested");
    fixture.folder("dest-child", "destination", "Nested");
    fixture.file("first", "source-child", "same.txt");
    fixture.file("existing-first", "dest-child", "same.txt");
    fixture.file("second", "source", "same.txt");
    fixture.file("existing-second", "destination", "same.txt");
    const confirm = vi
      .fn()
      .mockResolvedValueOnce(ConflictAction.Merge)
      .mockResolvedValueOnce(ConflictAction.OverwriteAll);
    const result = await moveCandidatesToTarget(options(confirm));
    expect(result.failed).toEqual([]);
    expect(result.notMoved).toEqual([]);
    expect(confirm).toHaveBeenCalledTimes(2);
    expect(mocks.moveFile).toHaveBeenCalledWith("second", {
      parentId: "destination",
      name: "same.txt",
      overwrite: true,
    });
  });

  it("renames a conflicting file without replacing the existing one", async () => {
    fixture.file("first", "source", "same.txt");
    fixture.file("existing", "destination", "same.txt");
    const confirm = vi
      .fn()
      .mockResolvedValueOnce(ConflictAction.Merge)
      .mockResolvedValueOnce(ConflictAction.Rename);
    const result = await moveCandidatesToTarget(options(confirm));
    expect(result.notMoved).toEqual([]);
    expect(fixture.files.get("first")?.name).toBe("same (1).txt");
    expect(fixture.files.get("existing")?.name).toBe("same.txt");
    expect(confirm.mock.calls[1][0].canApplyToAll).toBe(false);
  });

  it("keeps the source after a child transfer fails", async () => {
    fixture.file("first", "source");
    fixture.file("later", "source");
    mocks.moveFile.mockRejectedValueOnce(new Error("Transfer failed"));
    const result = await moveCandidatesToTarget(options());
    expect(result.failed).toEqual([item]);
    expect(result.notMoved).toEqual([item]);
    expect(fixture.files.get("first")?.nodeId).toBe("source");
    expect(fixture.files.get("later")?.nodeId).toBe("destination");
    expect(mocks.deleteNode).not.toHaveBeenCalled();
  });

  it("preserves a source file added after listing and before cleanup", async () => {
    fixture.file("first", "source");
    mocks.deleteNode.mockImplementation(
      async (id: string, skip: boolean, onlyIfEmpty: boolean) => {
        fixture.file("new-file", "source");
        await fixture.deleteNode(id, skip, onlyIfEmpty);
      },
    );
    const result = await moveCandidatesToTarget(options());
    expect(result.failed).toEqual([item]);
    expect(fixture.files.get("new-file")?.nodeId).toBe("source");
    expect(fixture.folders.has("source")).toBe(true);
  });

  it("matches encrypted folder names in the browser and preserves encrypted metadata", async () => {
    useVault.setState({
      isUnlocked: true,
      masterKey: await generateMasterKey(),
    });
    const source = fixture.folder("source", "from", "opaque-source");
    source.metadata = { [DISPLAY_META_KEY]: await encryptFolderName("Books") };
    const destination = fixture.folder(
      "destination",
      "to",
      "opaque-destination",
    );
    destination.metadata = {
      [DISPLAY_META_KEY]: await encryptFolderName("Books"),
    };
    const file = fixture.file("encrypted", "source", "opaque-file");
    file.metadata = {
      [ENCRYPTED_FLAG_KEY]: "true",
      [DISPLAY_META_KEY]: await encryptDisplayMeta({
        name: "photo.jpg",
        contentType: "image/jpeg",
      }),
    };
    const metadata = { ...file.metadata };
    const result = await moveCandidatesToTarget(options());
    expect(result.failed).toEqual([]);
    expect(fixture.files.get(file.id)?.metadata).toEqual(metadata);
    expect(fixture.files.get(file.id)?.nodeId).toBe(destination.id);
    expect(mocks.moveNode).not.toHaveBeenCalled();
    expect(result.movedFilesToOfferDecrypt[0]?.sourceParentId).toBe(
      destination.id,
    );
  });

  it("uses the nested destination encryption policy and actual file parent", async () => {
    useVault.setState({
      isUnlocked: true,
      masterKey: await generateMasterKey(),
    });
    fixture.folders.get("destination")!.metadata = {
      [FOLDER_ENCRYPTION_POLICY_KEY]: "true",
    };
    fixture.file("first", "source");
    const result = await moveCandidatesToTarget(options());
    expect(result.failed).toEqual([]);
    expect(result.movedFilesToEncrypt).toEqual([
      expect.objectContaining({ id: "first", sourceParentId: "destination" }),
    ]);
  });

  it("waits for encryption preparation before copying into an encrypted merge target", async () => {
    useVault.setState({
      isUnlocked: true,
      masterKey: await generateMasterKey(),
    });
    fixture.folders.get("destination")!.metadata = {
      [FOLDER_ENCRYPTION_POLICY_KEY]: "true",
    };
    fixture.file("first", "source");
    const prepareEncryptedTarget = vi.fn(async () => {
      expect(mocks.copyFile).not.toHaveBeenCalled();
      throw new Error("Server settings unavailable");
    });
    const result = await moveCandidatesToTarget({
      ...options(),
      operation: "copy",
      prepareEncryptedTarget,
    });
    expect(result.failed).toEqual([item]);
    expect(prepareEncryptedTarget).toHaveBeenCalledOnce();
    expect(mocks.copyFile).not.toHaveBeenCalled();
    expect(mocks.deleteNode).not.toHaveBeenCalled();
  });

  it("rejects merging into a descendant before transferring contents", async () => {
    fixture.folder("to", "source");
    fixture.file("first", "source");
    const result = await moveCandidatesToTarget({
      ...options(),
      operation: "copy",
    });
    expect(result.failed).toEqual([item]);
    expect(mocks.copyFile).not.toHaveBeenCalled();
    expect(mocks.getChildren).not.toHaveBeenCalledWith(
      "source",
      expect.anything(),
    );
  });

  it("does not offer merging a folder with itself when copying in place", async () => {
    const confirm = vi.fn(async () => ConflictAction.Cancel);
    await moveCandidatesToTarget({
      ...options(confirm),
      operation: "copy",
      targetParentId: "from",
    });
    expect(confirm).toHaveBeenCalledWith(
      expect.objectContaining({ canMerge: false, canApplyToAll: false }),
    );
    expect(mocks.deleteNode).not.toHaveBeenCalled();
  });

  it("keeps all actions available only until the last root item", async () => {
    const first = fixture.file("first", "from", "same.txt");
    const second = fixture.file("second", "from", "same.txt");
    fixture.file("existing", "to", "same.txt");
    const candidates: MoveClipboardItem[] = [first, second].map((file) => ({
      id: file.id,
      kind: "file",
      sourceParentId: "from",
      file,
    }));
    const confirm = vi.fn<MoveConflictResolver>(
      async () => ConflictAction.Skip,
    );
    await moveCandidatesToTarget({ ...options(confirm), candidates });
    expect(confirm.mock.calls.map(([prompt]) => prompt.canApplyToAll)).toEqual([
      true,
      false,
    ]);
  });

  it("keeps the source if empty cleanup is rejected", async () => {
    mocks.deleteNode.mockRejectedValue(conflict("Folder"));
    const result = await moveCandidatesToTarget(options());
    expect(result.failed).toEqual([item]);
    expect(fixture.folders.has("source")).toBe(true);
  });
});
