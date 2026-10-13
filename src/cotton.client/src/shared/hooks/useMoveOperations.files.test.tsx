import { generateMasterKey } from "@shared/crypto/keys";
import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useVault } from "../crypto";
import { FOLDER_ENCRYPTION_POLICY_KEY } from "../crypto/metadataFlags";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "../store/moveClipboardStore";
import { useNodesStore } from "../store/nodesStore";
import { ConflictAction } from "../types/nameConflict";
import { useMoveOperations } from "./useMoveOperations";

const mocks = vi.hoisted(() => ({
  moveFile: vi.fn(),
  moveNode: vi.fn(),
  confirmConflict: vi.fn(),
  getChildren: vi.fn(),
  getNode: vi.fn(),
  getAncestors: vi.fn(),
  fetchServerSettings: vi.fn(),
  encryptExistingFileWithTask: vi.fn(),
  decryptExistingFileWithTask: vi.fn(),
  refreshNodeContent: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
  showActionToast: vi.fn(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock("@shared/ui/notifications", () => ({
  toast: {
    success: mocks.toastSuccess,
    error: mocks.toastError,
  },
}));

vi.mock("../api/filesApi", () => ({
  filesApi: {
    moveFile: mocks.moveFile,
  },
}));

vi.mock("../api/nodesApi", () => ({
  nodesApi: {
    moveNode: mocks.moveNode,
    getChildren: mocks.getChildren,
    getNode: mocks.getNode,
    getAncestors: mocks.getAncestors,
  },
}));

vi.mock("../api/queries/serverSettings", () => ({
  fetchServerSettings: mocks.fetchServerSettings,
}));

vi.mock("../store/nodesActions", () => ({
  refreshNodeContent: mocks.refreshNodeContent,
}));

vi.mock("../tasks", () => ({
  encryptExistingFileWithTask: mocks.encryptExistingFileWithTask,
  decryptExistingFileWithTask: mocks.decryptExistingFileWithTask,
}));

vi.mock("../ui/ActionToast", () => ({
  showActionToast: mocks.showActionToast,
}));

const sourceParentId = "11111111-1111-4111-8111-111111111111";
const targetParentId = "22222222-2222-4222-8222-222222222222";

const plainFileItem: MoveClipboardItem = {
  id: "33333333-3333-4333-8333-333333333333",
  kind: "file",
  sourceParentId,
  file: {
    name: "plain.txt",
    contentType: "text/plain",
    sizeBytes: 100,
    metadata: {},
  },
};

const makeMovedFolderDto = (id: string) => ({
  id,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  layoutId: "layout-1",
  parentId: targetParentId,
  name: "Moved folder",
  metadata: {},
});

const makeMovedFileDto = (item: MoveClipboardItem) => ({
  id: item.id,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  nodeId: targetParentId,
  ownerId: "user-1",
  name: item.file?.name ?? "moved.txt",
  contentType: item.file?.contentType ?? "application/octet-stream",
  sizeBytes: item.file?.sizeBytes ?? 0,
  metadata: item.file?.metadata ?? {},
});

const makeEmptyChildrenResponse = (id = "empty") => ({
  content: {
    id,
    createdAt: "2026-05-17T00:00:00Z",
    updatedAt: "2026-05-17T00:00:00Z",
    nodes: [],
    files: [],
  },
  totalCount: 0,
});

const createNameConflictError = (
  conflictKind: "File" | "Folder" = "File",
): Error & { isAxiosError: boolean } =>
  Object.assign(new Error("Name conflict"), {
    isAxiosError: true,
    response: { status: 409, data: { conflictKind } },
  });

describe("useMoveOperations", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    useMoveClipboardStore.setState({ items: [] });
    useNodesStore.setState({
      cacheOwnerUserId: null,
      currentNode: null,
      ancestors: [],
      rootNodeId: null,
      loading: false,
      error: null,
      contentByNodeId: {
        [sourceParentId]: {
          id: sourceParentId,
          createdAt: "2026-05-17T00:00:00Z",
          updatedAt: "2026-05-17T00:00:00Z",
          nodes: [],
          files: [],
        },
        root: {
          id: "root",
          createdAt: "2026-05-17T00:00:00Z",
          updatedAt: "2026-05-17T00:00:00Z",
          nodes: [
            {
              id: targetParentId,
              createdAt: "2026-05-17T00:00:00Z",
              updatedAt: "2026-05-17T00:00:00Z",
              layoutId: "layout-1",
              parentId: null,
              name: "Vault",
              metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: "true" },
            },
          ],
          files: [],
        },
      },
      ancestorsByNodeId: {},
      lastUpdatedByNodeId: {},
    });
    useVault.setState({
      isUnlocked: true,
      masterKey: await generateMasterKey(),
    });
    mocks.moveFile.mockImplementation((id: string) =>
      Promise.resolve(makeMovedFileDto({ ...plainFileItem, id })),
    );
    mocks.moveNode.mockImplementation((id: string) =>
      Promise.resolve(makeMovedFolderDto(id)),
    );
    mocks.getChildren.mockResolvedValue(makeEmptyChildrenResponse());
    mocks.getNode.mockResolvedValue({
      ...makeMovedFolderDto(targetParentId),
      name: "Vault",
      parentId: null,
      metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: "true" },
    });
    mocks.getAncestors.mockResolvedValue([]);
    mocks.fetchServerSettings.mockResolvedValue({
      maxChunkSizeBytes: 1024,
      supportedHashAlgorithm: "SHA-256",
    });
    mocks.encryptExistingFileWithTask.mockResolvedValue(undefined);
    mocks.confirmConflict.mockResolvedValue(ConflictAction.Skip);
  });

  it("does not keep moved files in the clipboard when post-move encryption fails", async () => {
    useNodesStore.setState({ contentByNodeId: {} });
    mocks.encryptExistingFileWithTask.mockRejectedValueOnce(
      new Error("encryption failed"),
    );
    useMoveClipboardStore.getState().setItems([plainFileItem]);

    const { result } = renderHook(() =>
      useMoveOperations({ confirmConflict: mocks.confirmConflict }),
    );

    await act(async () => {
      await result.current.pasteInto(targetParentId);
    });

    expect(mocks.moveFile).toHaveBeenCalledWith(plainFileItem.id, {
      parentId: targetParentId,
    });
    expect(mocks.encryptExistingFileWithTask).toHaveBeenCalledOnce();
    expect(useMoveClipboardStore.getState().items).toEqual([]);
    expect(mocks.toastSuccess).toHaveBeenCalledWith(
      "move.toasts.moved",
      expect.any(Object),
    );
    expect(mocks.toastError).toHaveBeenCalledWith(
      "clientEncryption.toasts.encryptExistingFailed",
      expect.any(Object),
    );
  });

  it("moves a conflicting file with the suggested name after confirmation", async () => {
    useMoveClipboardStore.getState().setItems([plainFileItem]);
    mocks.moveFile.mockRejectedValueOnce(createNameConflictError());
    mocks.confirmConflict.mockResolvedValueOnce(ConflictAction.Rename);

    const { result } = renderHook(() =>
      useMoveOperations({ confirmConflict: mocks.confirmConflict }),
    );

    await act(async () => {
      await result.current.pasteInto(targetParentId);
    });

    expect(mocks.confirmConflict).toHaveBeenCalledWith({
      newName: "plain (1).txt",
      canOverwrite: true,
    });
    expect(mocks.moveFile).toHaveBeenNthCalledWith(1, plainFileItem.id, {
      parentId: targetParentId,
    });
    expect(mocks.moveFile).toHaveBeenNthCalledWith(2, plainFileItem.id, {
      parentId: targetParentId,
      name: "plain (1).txt",
    });
    expect(useMoveClipboardStore.getState().items).toEqual([]);
    expect(mocks.toastError).not.toHaveBeenCalled();
  });

  it("uses current target names when suggesting a name after a conflict", async () => {
    useMoveClipboardStore.getState().setItems([plainFileItem]);
    mocks.moveFile.mockRejectedValueOnce(createNameConflictError());
    mocks.getChildren.mockResolvedValue({
      ...makeEmptyChildrenResponse(targetParentId),
      content: {
        ...makeEmptyChildrenResponse(targetParentId).content,
        files: [
          makeMovedFileDto(plainFileItem),
          { ...makeMovedFileDto(plainFileItem), id: "another-file", name: "plain (1).txt" },
        ],
      },
      totalCount: 2,
    });
    mocks.confirmConflict.mockResolvedValueOnce(ConflictAction.Rename);

    const { result } = renderHook(() =>
      useMoveOperations({ confirmConflict: mocks.confirmConflict }),
    );

    await act(async () => {
      await result.current.pasteInto(targetParentId);
    });

    expect(mocks.getChildren).toHaveBeenCalledWith(targetParentId, { page: 1 });
    expect(mocks.confirmConflict).toHaveBeenCalledWith({
      newName: "plain (2).txt",
      canOverwrite: true,
    });
    expect(mocks.moveFile).toHaveBeenNthCalledWith(2, plainFileItem.id, {
      parentId: targetParentId,
      name: "plain (2).txt",
    });
  });

  it("replaces a conflicting file after confirmation", async () => {
    useMoveClipboardStore.getState().setItems([plainFileItem]);
    mocks.moveFile.mockRejectedValueOnce(createNameConflictError());
    mocks.confirmConflict.mockResolvedValueOnce(ConflictAction.Overwrite);

    const { result } = renderHook(() =>
      useMoveOperations({ confirmConflict: mocks.confirmConflict }),
    );

    await act(async () => {
      await result.current.pasteInto(targetParentId);
    });

    expect(mocks.moveFile).toHaveBeenNthCalledWith(2, plainFileItem.id, {
      parentId: targetParentId,
      name: "plain.txt",
      overwrite: true,
    });
    expect(useMoveClipboardStore.getState().items).toEqual([]);
    expect(mocks.toastError).not.toHaveBeenCalled();
  });

  it("keeps a skipped conflicting file in the cut clipboard", async () => {
    useMoveClipboardStore.getState().setItems([plainFileItem]);
    mocks.moveFile.mockRejectedValueOnce(createNameConflictError());
    mocks.confirmConflict.mockResolvedValueOnce(ConflictAction.Skip);

    const { result } = renderHook(() =>
      useMoveOperations({ confirmConflict: mocks.confirmConflict }),
    );

    await act(async () => {
      await result.current.pasteInto(targetParentId);
    });

    expect(mocks.moveFile).toHaveBeenCalledOnce();
    expect(useMoveClipboardStore.getState().items).toEqual([plainFileItem]);
    expect(mocks.toastError).not.toHaveBeenCalled();
  });

  it("does not offer replacement when the server reports a folder conflict", async () => {
    useMoveClipboardStore.getState().setItems([plainFileItem]);
    mocks.moveFile.mockRejectedValueOnce(createNameConflictError("Folder"));
    mocks.confirmConflict.mockResolvedValueOnce(ConflictAction.Skip);

    const { result } = renderHook(() =>
      useMoveOperations({ confirmConflict: mocks.confirmConflict }),
    );

    await act(async () => {
      await result.current.pasteInto(targetParentId);
    });

    expect(mocks.confirmConflict).toHaveBeenCalledWith({
      newName: "plain (1).txt",
      canOverwrite: false,
    });
  });
});
