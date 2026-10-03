import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ENCRYPTED_FLAG_KEY, generateMasterKey, useVault } from "../crypto";
import { FOLDER_ENCRYPTION_POLICY_KEY } from "../crypto/metadataFlags";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "../store/moveClipboardStore";
import { useNodesStore } from "../store/nodesStore";
import { ConflictAction } from "../types/nameConflict";
import { useMoveOperations } from "./useMoveOperations";

const mocks = vi.hoisted(() => ({
  copyFile: vi.fn(),
  copyNode: vi.fn(),
  moveFile: vi.fn(),
  getNode: vi.fn(),
  getChildren: vi.fn(),
  encrypt: vi.fn(),
  refresh: vi.fn(),
  confirm: vi.fn(),
  success: vi.fn(),
  error: vi.fn(),
}));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("../api/filesApi", () => ({
  filesApi: { copyFile: mocks.copyFile, moveFile: mocks.moveFile },
}));
vi.mock("../api/nodesApi", () => ({
  nodesApi: {
    copyNode: mocks.copyNode,
    getNode: mocks.getNode,
    getChildren: mocks.getChildren,
    getAncestors: vi.fn(async () => []),
  },
}));
vi.mock("../api/queries/serverSettings", () => ({
  fetchServerSettings: vi.fn(async () => ({
    maxChunkSizeBytes: 100,
    supportedHashAlgorithm: "SHA256",
  })),
}));
vi.mock("../tasks", () => ({
  encryptExistingFileWithTask: mocks.encrypt,
  decryptExistingFileWithTask: vi.fn(),
}));
vi.mock("../store/nodesActions", () => ({ refreshNodeContent: mocks.refresh }));
vi.mock("@shared/ui/notifications", () => ({
  toast: { success: mocks.success, error: mocks.error },
}));
vi.mock("../ui/ActionToast", () => ({ showActionToast: vi.fn() }));

const target = {
  id: "target",
  parentId: null,
  name: "Target",
  layoutId: "layout",
  metadata: {},
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
};
const original = {
  id: "original",
  nodeId: "source",
  ownerId: "user",
  name: "original.txt",
  contentType: "text/plain",
  sizeBytes: 12,
  metadata: {},
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
};
const copied = { ...original, id: "copy", nodeId: target.id };
const item: MoveClipboardItem = {
  id: original.id,
  kind: "file",
  sourceParentId: original.nodeId,
  file: original,
};
const emptyContent = {
  id: "empty",
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
  nodes: [],
  files: [],
};
const renderOperations = () =>
  renderHook(() => useMoveOperations({ confirmConflict: mocks.confirm }));

beforeEach(() => {
  vi.resetAllMocks();
  useMoveClipboardStore.getState().clear();
  useNodesStore.setState({
    contentByNodeId: {
      source: { ...emptyContent, files: [original] },
      target: emptyContent,
    },
  });
  useVault.setState({ isUnlocked: false, masterKey: null });
  mocks.copyFile.mockResolvedValue(copied);
  mocks.getNode.mockResolvedValue(target);
  mocks.getChildren.mockResolvedValue({ content: emptyContent, totalCount: 0 });
});

describe("copy and paste", () => {
  it("keeps the original, updates the destination and retains the clipboard for repeated pastes", async () => {
    const { result } = renderOperations();
    act(() => result.current.copyItems([item]));
    await act(() => result.current.pasteInto(target.id));
    expect(mocks.copyFile).toHaveBeenCalledWith(original.id, {
      parentId: target.id,
    });
    expect(mocks.moveFile).not.toHaveBeenCalled();
    expect(useNodesStore.getState().contentByNodeId.source?.files).toEqual([
      original,
    ]);
    expect(useNodesStore.getState().contentByNodeId.target?.files).toEqual([
      copied,
    ]);
    expect(useMoveClipboardStore.getState().items).toEqual([item]);
    expect(useMoveClipboardStore.getState().operation).toBe("copy");
    await act(() => result.current.pasteInto("another-target"));
    expect(mocks.copyFile).toHaveBeenLastCalledWith(original.id, {
      parentId: "another-target",
    });
    expect(mocks.success).toHaveBeenCalledWith(
      "copy.toasts.copied",
      expect.objectContaining({ toastId: expect.any(String) }),
    );
  });

  it("encrypts the newly copied file rather than the original in an encrypted destination", async () => {
    useVault.setState({
      isUnlocked: true,
      masterKey: await generateMasterKey(),
    });
    mocks.getNode.mockResolvedValue({
      ...target,
      metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: "true" },
    });
    const { result } = renderOperations();
    act(() => result.current.copyItems([item]));
    await act(() => result.current.pasteInto(target.id));
    expect(mocks.encrypt).toHaveBeenCalledWith(
      expect.objectContaining({
        file: expect.objectContaining({ id: copied.id }),
        targetNodeId: target.id,
      }),
    );
    expect(useNodesStore.getState().contentByNodeId.source?.files).toEqual([
      original,
    ]);
  });

  it("does not copy plaintext into an encrypted destination while the vault is locked", async () => {
    mocks.getNode.mockResolvedValue({
      ...target,
      metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: "true" },
    });
    const { result } = renderOperations();
    act(() => result.current.copyItems([item]));
    await act(() => result.current.pasteInto(target.id));
    expect(mocks.copyFile).not.toHaveBeenCalled();
    expect(mocks.error).toHaveBeenCalledWith(
      "clientEncryption.toasts.unlockRequired",
    );
  });

  it("retains encryption metadata and does not encrypt ciphertext again", async () => {
    mocks.getNode.mockResolvedValue({
      ...target,
      metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: "true" },
    });
    const encrypted = {
      ...original,
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    };
    mocks.copyFile.mockResolvedValue({
      ...encrypted,
      id: copied.id,
      nodeId: target.id,
    });
    const { result } = renderOperations();
    act(() => result.current.copyItems([{ ...item, file: encrypted }]));
    await act(() => result.current.pasteInto(target.id));
    expect(mocks.encrypt).not.toHaveBeenCalled();
    expect(mocks.copyFile).toHaveBeenCalledWith(original.id, {
      parentId: target.id,
      name: expect.stringMatching(/^[a-f0-9-]{36}$/),
    });
    expect(
      useNodesStore.getState().contentByNodeId.target?.files[0].metadata,
    ).toEqual(encrypted.metadata);
  });

  it("resolves same-folder conflicts with a new name without offering to replace the original", async () => {
    const error = Object.assign(new Error("Conflict"), {
      isAxiosError: true,
      response: { status: 409, data: { conflictKind: "File" } },
    });
    mocks.copyFile.mockRejectedValueOnce(error).mockResolvedValueOnce(copied);
    mocks.confirm.mockResolvedValue(ConflictAction.Rename);
    mocks.getChildren.mockResolvedValue({
      content: { ...emptyContent, files: [original] },
      totalCount: 1,
    });
    const { result } = renderOperations();
    act(() => result.current.copyItems([item]));
    await act(() => result.current.pasteInto(original.nodeId));
    expect(mocks.confirm).toHaveBeenCalledWith({
      newName: "original (1).txt",
      canOverwrite: false,
    });
    expect(mocks.copyFile).toHaveBeenLastCalledWith(original.id, {
      parentId: original.nodeId,
      name: "original (1).txt",
    });
  });

  it("switches from copying to cutting when a new cut operation starts", () => {
    const { result } = renderOperations();
    act(() => result.current.copyItems([item]));
    act(() => result.current.cutItems([item]));
    expect(useMoveClipboardStore.getState().operation).toBe("move");
  });
});
