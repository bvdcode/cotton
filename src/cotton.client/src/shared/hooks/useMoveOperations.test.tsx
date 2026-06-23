import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useVault } from "../crypto";
import { FOLDER_ENCRYPTION_POLICY_KEY } from "../crypto/metadataFlags";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "../store/moveClipboardStore";
import { useNodesStore } from "../store/nodesStore";
import { useMoveOperations } from "./useMoveOperations";

const mocks = vi.hoisted(() => ({
  moveFile: vi.fn(),
  moveNode: vi.fn(),
  getChildren: vi.fn(),
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

describe("useMoveOperations", () => {
  beforeEach(() => {
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
    useVault.setState({ isUnlocked: true, masterKey: {} as CryptoKey });
    mocks.moveFile.mockImplementation((id: string) =>
      Promise.resolve(makeMovedFileDto({ ...plainFileItem, id })),
    );
    mocks.moveNode.mockImplementation((id: string) =>
      Promise.resolve(makeMovedFolderDto(id)),
    );
    mocks.getChildren.mockResolvedValue(makeEmptyChildrenResponse());
    mocks.fetchServerSettings.mockResolvedValue({
      maxChunkSizeBytes: 1024,
      supportedHashAlgorithm: "SHA-256",
    });
    mocks.encryptExistingFileWithTask.mockResolvedValue(undefined);
  });

  it("does not keep moved files in the clipboard when post-move encryption fails", async () => {
    mocks.encryptExistingFileWithTask.mockRejectedValueOnce(
      new Error("encryption failed"),
    );
    useMoveClipboardStore.getState().setItems([plainFileItem]);

    const { result } = renderHook(() => useMoveOperations());

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

  it("encrypts plain files nested inside a moved folder when the target encrypts new files", async () => {
    const folderItem: MoveClipboardItem = {
      id: "44444444-4444-4444-8444-444444444444",
      kind: "folder",
      sourceParentId,
    };
    const nestedNodeId = "55555555-5555-4555-8555-555555555555";
    mocks.getChildren
      .mockResolvedValueOnce({
        content: {
          id: folderItem.id,
          createdAt: "2026-05-17T00:00:00Z",
          updatedAt: "2026-05-17T00:00:00Z",
          nodes: [
            {
              id: nestedNodeId,
              createdAt: "2026-05-17T00:00:00Z",
              updatedAt: "2026-05-17T00:00:00Z",
              layoutId: "layout-1",
              parentId: folderItem.id,
              name: "Nested",
              metadata: {},
            },
          ],
          files: [
            {
              id: "66666666-6666-4666-8666-666666666666",
              createdAt: "2026-05-17T00:00:00Z",
              updatedAt: "2026-05-17T00:00:00Z",
              nodeId: folderItem.id,
              ownerId: "user-1",
              name: "root-plain.txt",
              contentType: "text/plain",
              sizeBytes: 12,
              metadata: {},
            },
          ],
        },
        totalCount: 2,
      })
      .mockResolvedValueOnce({
        content: {
          id: nestedNodeId,
          createdAt: "2026-05-17T00:00:00Z",
          updatedAt: "2026-05-17T00:00:00Z",
          nodes: [],
          files: [
            {
              id: "77777777-7777-4777-8777-777777777777",
              createdAt: "2026-05-17T00:00:00Z",
              updatedAt: "2026-05-17T00:00:00Z",
              nodeId: nestedNodeId,
              ownerId: "user-1",
              name: "nested-plain.txt",
              contentType: "text/plain",
              sizeBytes: 34,
              metadata: {},
            },
          ],
        },
        totalCount: 1,
      });
    useMoveClipboardStore.getState().setItems([folderItem]);

    const { result } = renderHook(() => useMoveOperations());

    await act(async () => {
      await result.current.pasteInto(targetParentId);
    });

    expect(mocks.moveNode).toHaveBeenCalledWith(folderItem.id, {
      parentId: targetParentId,
    });
    expect(mocks.getChildren).toHaveBeenCalledWith(folderItem.id, {
      page: 1,
      pageSize: 250,
    });
    expect(mocks.getChildren).toHaveBeenCalledWith(nestedNodeId, {
      page: 1,
      pageSize: 250,
    });
    expect(mocks.encryptExistingFileWithTask).toHaveBeenCalledTimes(2);
    expect(mocks.encryptExistingFileWithTask).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({
        file: expect.objectContaining({
          id: "66666666-6666-4666-8666-666666666666",
        }),
        targetNodeId: folderItem.id,
      }),
    );
    expect(mocks.encryptExistingFileWithTask).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({
        file: expect.objectContaining({
          id: "77777777-7777-4777-8777-777777777777",
        }),
        targetNodeId: nestedNodeId,
      }),
    );
    expect(useMoveClipboardStore.getState().items).toEqual([]);
  });
  it("warns when moved folder encryption scan is truncated", async () => {
    const folderItem: MoveClipboardItem = {
      id: "44444444-4444-4444-8444-444444444444",
      kind: "folder",
      sourceParentId,
    };
    const files = Array.from({ length: 501 }, (_, index) => ({
      id: `plain-${index}`,
      createdAt: "2026-05-17T00:00:00Z",
      updatedAt: "2026-05-17T00:00:00Z",
      nodeId: folderItem.id,
      ownerId: "user-1",
      name: `plain-${index}.txt`,
      contentType: "text/plain",
      sizeBytes: 12,
      metadata: {},
    }));
    mocks.getChildren.mockResolvedValueOnce({
      content: {
        id: folderItem.id,
        createdAt: "2026-05-17T00:00:00Z",
        updatedAt: "2026-05-17T00:00:00Z",
        nodes: [],
        files,
      },
      totalCount: files.length,
    });
    useMoveClipboardStore.getState().setItems([folderItem]);

    const { result } = renderHook(() => useMoveOperations());

    await act(async () => {
      await result.current.pasteInto(targetParentId);
    });

    expect(mocks.encryptExistingFileWithTask).toHaveBeenCalledTimes(500);
    expect(mocks.toastError).toHaveBeenCalledWith(
      "clientEncryption.toasts.encryptExistingScanIncomplete",
      expect.any(Object),
    );
    expect(useMoveClipboardStore.getState().items).toEqual([]);
  });
});
