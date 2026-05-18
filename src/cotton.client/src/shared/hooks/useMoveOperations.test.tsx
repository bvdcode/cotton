import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useVault } from "../crypto";
import { FOLDER_ENCRYPTION_POLICY_KEY } from "../crypto/metadataFlags";
import { useMoveClipboardStore, type MoveClipboardItem } from "../store/moveClipboardStore";
import { useNodesStore } from "../store/nodesStore";
import { useMoveOperations } from "./useMoveOperations";

const mocks = vi.hoisted(() => ({
  moveFile: vi.fn(),
  moveNode: vi.fn(),
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

vi.mock("react-toastify", () => ({
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
    mocks.moveFile.mockResolvedValue(undefined);
    mocks.moveNode.mockResolvedValue(undefined);
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
});
