import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeDto } from "../api/layoutsApi";
import type { MoveNodeRequest } from "../api/nodesApi";
import { DISPLAY_META_KEY } from "../crypto/displayMeta";
import { encryptFolderName, readFolderName } from "../crypto/folderDisplayMeta";
import { generateMasterKey } from "../crypto/keys";
import { useVault } from "../crypto/vault";
import { moveCandidatesToTarget } from "../hooks/fileTransferExecution";
import { ConflictAction } from "../types/nameConflict";
import { moveItemWithConflictResolution } from "./moveConflictResolution";

const mocks = vi.hoisted(() => ({
  getNode: vi.fn(),
  children: vi.fn(),
  move: vi.fn(),
  copy: vi.fn(),
}));
vi.mock("../api/nodesApi", () => ({
  nodesApi: {
    getNode: mocks.getNode,
    getChildren: mocks.children,
    moveNode: mocks.move,
    copyNode: mocks.copy,
  },
}));

const source: NodeDto = {
  id: "source-folder",
  name: "Documents",
  parentId: "source",
  layoutId: "layout",
  metadata: {},
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
};
const emptyContent = {
  id: "target",
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
  nodes: [],
  files: [],
};
const item = (node: NodeDto) => ({
  id: node.id,
  kind: "folder" as const,
  sourceParentId: "source",
});

beforeEach(async () => {
  vi.resetAllMocks();
  useVault.setState({ isUnlocked: true, masterKey: await generateMasterKey() });
  mocks.getNode.mockResolvedValue(source);
  mocks.children.mockResolvedValue({ content: emptyContent, totalCount: 0 });
  mocks.move.mockImplementation(
    async (id: string, request: MoveNodeRequest) => ({
      ...source,
      id,
      parentId: request.parentId,
      name: request.name ?? source.name,
    }),
  );
  mocks.copy.mockImplementation(
    async (id: string, request: MoveNodeRequest) => ({
      ...source,
      id: `copy-${id}`,
      parentId: request.parentId,
      name: request.name ?? source.name,
    }),
  );
});

afterEach(() => useVault.getState().lock());

describe("folder display-name conflicts", () => {
  it.each([false, true])(
    "preserves an existing folder name and metadata when moving into an encrypted destination (encrypted source: %s)",
    async (encrypted) => {
      const folder = encrypted
        ? {
            ...source,
            name: "opaque-source",
            metadata: {
              [DISPLAY_META_KEY]: await encryptFolderName("Documents"),
            },
          }
        : source;
      mocks.getNode.mockResolvedValue(folder);

      const outcome = await moveItemWithConflictResolution({
        item: item(folder),
        targetParentId: "target",
        encryptFolders: true,
        skipAllConflicts: false,
        confirmConflict: vi.fn(),
      });

      expect(outcome.kind).toBe("moved");
      expect(mocks.move).toHaveBeenCalledExactlyOnceWith(folder.id, {
        parentId: "target",
      });
      expect(mocks.children).toHaveBeenCalledExactlyOnceWith("target", {
        page: 1,
      });
    },
  );

  it("encrypts the new copy name without changing the source folder", async () => {
    const outcome = await moveItemWithConflictResolution({
      item: item(source),
      operation: "copy",
      targetParentId: "target",
      encryptFolders: true,
      skipAllConflicts: false,
      confirmConflict: vi.fn(),
    });

    expect(outcome.kind).toBe("moved");
    expect(mocks.copy).toHaveBeenCalledOnce();
    const request: MoveNodeRequest = mocks.copy.mock.calls[0][1];
    const name = request.metadata?.[DISPLAY_META_KEY];
    if (typeof name !== "string") {
      throw new Error("Encrypted folder name is missing.");
    }
    expect(request.name).not.toBe(source.name);
    expect(
      await readFolderName({
        name: request.name ?? "",
        metadata: { en: name },
      }),
    ).toBe(source.name);
    expect(mocks.move).not.toHaveBeenCalled();
    expect(source.name).toBe("Documents");
    expect(source.metadata).toEqual({});
  });

  it.each(["move", "copy"] as const)(
    "resolves a plaintext %s against an encrypted folder before submitting it",
    async (operation) => {
      mocks.children.mockResolvedValue({
        content: {
          ...emptyContent,
          nodes: [
            {
              ...source,
              id: "existing",
              name: "opaque-existing",
              metadata: {
                [DISPLAY_META_KEY]: await encryptFolderName("DOCUMENTS"),
              },
            },
          ],
        },
        totalCount: 1,
      });
      const confirm = vi.fn(async () => ConflictAction.Rename);
      const outcome = await moveItemWithConflictResolution({
        item: item(source),
        operation,
        targetParentId: "target",
        skipAllConflicts: false,
        confirmConflict: confirm,
      });

      expect(confirm).toHaveBeenCalledWith({
        newName: "Documents (1)",
        canOverwrite: false,
      });
      const send = operation === "move" ? mocks.move : mocks.copy;
      expect(send).toHaveBeenCalledOnce();
      expect(send).toHaveBeenCalledWith(source.id, {
        parentId: "target",
        name: "Documents (1)",
      });
      expect(outcome.kind).toBe("moved");
      expect(mocks.children).toHaveBeenCalledOnce();
    },
  );

  it("reserves a plaintext folder name for a later encrypted folder in the batch", async () => {
    const encrypted = {
      ...source,
      id: "encrypted",
      name: "opaque-source",
      metadata: { [DISPLAY_META_KEY]: await encryptFolderName("Documents") },
    };
    mocks.getNode.mockImplementation(async (id: string) =>
      id === source.id ? source : encrypted,
    );
    mocks.copy.mockImplementation(
      async (id: string, request: MoveNodeRequest) => ({
        ...source,
        id: `copy-${id}`,
        parentId: request.parentId,
        name: request.name ?? source.name,
        metadata: request.metadata ?? {},
      }),
    );
    const confirm = vi.fn(async () => ConflictAction.Rename);

    const result = await moveCandidatesToTarget({
      candidates: [item(source), item(encrypted)],
      operation: "copy",
      targetParentId: "target",
      targetEncryptsNewFiles: false,
      confirmConflict: confirm,
    });

    expect(result.succeeded).toHaveLength(2);
    expect(confirm).toHaveBeenCalledWith({
      newName: "Documents (1)",
      canOverwrite: false,
    });
    const request: MoveNodeRequest = mocks.copy.mock.calls[1][1];
    const encryptedName = request.metadata?.[DISPLAY_META_KEY];
    if (typeof encryptedName !== "string") {
      throw new Error("Encrypted folder name is missing.");
    }
    expect(
      await readFolderName({
        name: request.name ?? "",
        metadata: { [DISPLAY_META_KEY]: encryptedName },
      }),
    ).toBe("Documents (1)");
    expect(mocks.children).toHaveBeenCalledOnce();
  });

  it("skips a conflicting folder without submitting a mutation", async () => {
    mocks.children.mockResolvedValue({
      content: {
        ...emptyContent,
        nodes: [
          {
            ...source,
            id: "existing",
            name: "opaque-existing",
            metadata: {
              [DISPLAY_META_KEY]: await encryptFolderName("Documents"),
            },
          },
        ],
      },
      totalCount: 1,
    });

    const outcome = await moveItemWithConflictResolution({
      item: item(source),
      targetParentId: "target",
      skipAllConflicts: false,
      confirmConflict: vi.fn(async () => ConflictAction.Skip),
    });

    expect(outcome.kind).toBe("skipped");
    expect(mocks.move).not.toHaveBeenCalled();
  });

  it("moves a plaintext folder with a locked vault without a display-name lookup", async () => {
    useVault.setState({ isUnlocked: false, masterKey: null });
    const outcome = await moveItemWithConflictResolution({
      item: item(source),
      targetParentId: "target",
      skipAllConflicts: false,
      confirmConflict: vi.fn(),
    });

    expect(outcome.kind).toBe("moved");
    expect(mocks.move).toHaveBeenCalledWith(source.id, { parentId: "target" });
    expect(mocks.children).not.toHaveBeenCalled();
  });

  it.each(["move", "copy"] as const)(
    "does not %s an encrypted folder with a locked vault",
    async (operation) => {
      mocks.getNode.mockResolvedValue({
        ...source,
        name: "opaque-source",
        metadata: { [DISPLAY_META_KEY]: await encryptFolderName("Documents") },
      });
      useVault.setState({ isUnlocked: false, masterKey: null });

      const outcome = await moveItemWithConflictResolution({
        item: item(source),
        operation,
        targetParentId: "target",
        skipAllConflicts: false,
        confirmConflict: vi.fn(),
      });

      expect(outcome.kind).toBe("failed");
      expect(mocks.move).not.toHaveBeenCalled();
      expect(mocks.copy).not.toHaveBeenCalled();
    },
  );
});
