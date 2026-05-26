import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import type {
  NodeContentDto,
  NodeFileManifestDto,
} from "../../../shared/api/nodesApi";
import { useVault } from "../../../shared/crypto";

vi.mock("../../../shared/api/queries/serverSettings", () => ({
  fetchServerSettings: vi.fn(),
}));

vi.mock("../../../shared/store/nodesActions", () => ({
  refreshNodeContent: vi.fn(),
}));

vi.mock("../../../shared/utils/clientEncryptionFolderScan", () => ({
  collectPlainFilesInFoldersForClientEncryption: vi.fn(async () => ({
    files: [],
    scannedFolders: 0,
    truncated: false,
  })),
}));

vi.mock("../../../shared/tasks", () => ({
  decryptExistingFileWithTask: vi.fn(),
  encryptExistingFileWithTask: vi.fn(),
}));

import { fetchServerSettings } from "../../../shared/api/queries/serverSettings";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import { collectPlainFilesInFoldersForClientEncryption } from "../../../shared/utils/clientEncryptionFolderScan";
import {
  decryptExistingFileWithTask,
  encryptExistingFileWithTask,
} from "../../../shared/tasks";
import { useFolderClientEncryptionActions } from "./useFolderClientEncryptionActions";

const makeNode = (metadata: Record<string, string> = {}): NodeDto => ({
  id: "node-1",
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  layoutId: "layout-1",
  parentId: null,
  name: "Vault",
  metadata,
});

const makeFile = (
  id: string,
  metadata: Record<string, string> = {},
): NodeFileManifestDto => ({
  id,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  nodeId: "node-1",
  ownerId: "user-1",
  name: `${id}.txt`,
  contentType: "text/plain",
  sizeBytes: 100,
  metadata,
});

const makeContent = (files: NodeFileManifestDto[]): NodeContentDto => ({
  id: "node-1",
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  nodes: [],
  files,
});

describe("useFolderClientEncryptionActions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useVault.getState().lock();
    vi.mocked(collectPlainFilesInFoldersForClientEncryption).mockResolvedValue({
      files: [],
      scannedFolders: 0,
      truncated: false,
    });
    vi.mocked(fetchServerSettings).mockResolvedValue({
      version: "1.0.0",
      maxChunkSizeBytes: 1024,
      supportedHashAlgorithm: "SHA-256",
    });
  });

  afterEach(() => {
    useVault.getState().lock();
  });

  it("reports plain files only when the current folder encrypts new uploads", () => {
    const content = makeContent([
      makeFile("plain"),
      makeFile("encrypted", { isClientEncrypted: "true" }),
    ]);

    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-1",
        currentNode: makeNode({ isClientEncryptionEnabled: "true" }),
        content,
        onToast: vi.fn(),
      }),
    );

    expect(result.current.folderPolicyEnabled).toBe(true);
    expect(result.current.plainFiles.map((file) => file.id)).toEqual([
      "plain",
    ]);
    expect(result.current.encryptedFiles.map((file) => file.id)).toEqual([
      "encrypted",
    ]);
  });

  it("does not expose actions from stale folder content", () => {
    const staleContent = makeContent([
      makeFile("plain"),
      makeFile("encrypted", { isClientEncrypted: "true" }),
    ]);

    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-2",
        currentNode: {
          ...makeNode({ isClientEncryptionEnabled: "true" }),
          id: "node-2",
        },
        content: staleContent,
        onToast: vi.fn(),
      }),
    );

    expect(result.current.folderPolicyEnabled).toBe(true);
    expect(result.current.plainFiles).toEqual([]);
    expect(result.current.encryptedFiles).toEqual([]);
  });

  it("uses the effective folder policy supplied by the page", () => {
    const content = makeContent([makeFile("plain")]);

    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-1",
        currentNode: makeNode({}),
        content,
        folderPolicyEnabled: true,
        onToast: vi.fn(),
      }),
    );

    expect(result.current.folderPolicyEnabled).toBe(true);
    expect(result.current.plainFiles.map((file) => file.id)).toEqual([
      "plain",
    ]);
  });

  it("does not use an inherited policy while current folder data is stale", () => {
    const content = makeContent([makeFile("plain")]);

    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-2",
        currentNode: makeNode({}),
        content,
        folderPolicyEnabled: true,
        onToast: vi.fn(),
      }),
    );

    expect(result.current.folderPolicyEnabled).toBe(true);
    expect(result.current.plainFiles).toEqual([]);
  });

  it("encrypts existing plain files through task-backed encryption", async () => {
    useVault.setState({ isUnlocked: true, masterKey: {} as CryptoKey });
    const onToast = vi.fn();
    const content = makeContent([makeFile("a"), makeFile("b")]);

    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-1",
        currentNode: makeNode({ isClientEncryptionEnabled: "true" }),
        content,
        onToast,
      }),
    );

    await act(async () => {
      await result.current.encryptPlainFiles();
    });

    expect(fetchServerSettings).toHaveBeenCalled();
    expect(encryptExistingFileWithTask).toHaveBeenCalledTimes(2);
    expect(encryptExistingFileWithTask).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({
        file: expect.objectContaining({ id: "a" }),
        targetNodeId: "node-1",
        scopeLabel: "Vault",
      }),
    );
    expect(refreshNodeContent).toHaveBeenCalledWith("node-1");
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.encryptExistingComplete",
    );
  });

  it("requires the vault to be unlocked before encrypting existing files", async () => {
    const onToast = vi.fn();
    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-1",
        currentNode: makeNode({ isClientEncryptionEnabled: "true" }),
        content: makeContent([makeFile("a")]),
        onToast,
      }),
    );

    await act(async () => {
      await result.current.encryptPlainFiles();
    });

    expect(fetchServerSettings).not.toHaveBeenCalled();
    expect(encryptExistingFileWithTask).not.toHaveBeenCalled();
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.unlockRequired",
      "error",
    );
  });

  it("decrypts existing encrypted files through task-backed decryption", async () => {
    useVault.setState({ isUnlocked: true, masterKey: {} as CryptoKey });
    const onToast = vi.fn();
    const content = makeContent([
      makeFile("encrypted-a", { isClientEncrypted: "true" }),
      makeFile("encrypted-b", { isClientEncrypted: "true" }),
    ]);

    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-1",
        currentNode: makeNode({}),
        content,
        onToast,
      }),
    );

    await act(async () => {
      await result.current.decryptEncryptedFiles();
    });

    expect(fetchServerSettings).toHaveBeenCalled();
    expect(decryptExistingFileWithTask).toHaveBeenCalledTimes(2);
    expect(decryptExistingFileWithTask).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({
        file: expect.objectContaining({
          id: "encrypted-a",
          metadata: { isClientEncrypted: "true" },
        }),
        targetNodeId: "node-1",
        scopeLabel: "Vault",
      }),
    );
    expect(refreshNodeContent).toHaveBeenCalledWith("node-1");
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.decryptExistingComplete",
    );
  });


  it("encrypts recursive plain files when the current encrypted folder action runs", async () => {
    useVault.setState({ isUnlocked: true, masterKey: {} as CryptoKey });
    const onToast = vi.fn();
    const nestedFile = { ...makeFile("nested"), nodeId: "nested-node" };
    vi.mocked(collectPlainFilesInFoldersForClientEncryption).mockResolvedValue({
      files: [nestedFile],
      scannedFolders: 2,
      truncated: false,
    });

    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-1",
        currentNode: makeNode({ isClientEncryptionEnabled: "true" }),
        content: makeContent([makeFile("direct")]),
        onToast,
      }),
    );

    await act(async () => {
      await result.current.encryptPlainFiles();
    });

    expect(collectPlainFilesInFoldersForClientEncryption).toHaveBeenCalledWith([
      "node-1",
    ]);
    expect(encryptExistingFileWithTask).toHaveBeenCalledTimes(1);
    expect(encryptExistingFileWithTask).toHaveBeenCalledWith(
      expect.objectContaining({
        file: expect.objectContaining({ id: "nested" }),
        targetNodeId: "nested-node",
      }),
    );
    expect(refreshNodeContent).toHaveBeenCalledWith("node-1");
    expect(refreshNodeContent).toHaveBeenCalledWith("nested-node");
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.encryptExistingComplete",
    );
  });

  it("warns when recursive plain file encryption scan is incomplete", async () => {
    useVault.setState({ isUnlocked: true, masterKey: {} as CryptoKey });
    const onToast = vi.fn();
    vi.mocked(collectPlainFilesInFoldersForClientEncryption).mockResolvedValue({
      files: [makeFile("scanned")],
      scannedFolders: 250,
      truncated: true,
    });

    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-1",
        currentNode: makeNode({ isClientEncryptionEnabled: "true" }),
        content: makeContent([makeFile("direct")]),
        onToast,
      }),
    );

    await act(async () => {
      await result.current.encryptPlainFiles();
    });

    expect(encryptExistingFileWithTask).toHaveBeenCalledOnce();
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.encryptExistingScanIncomplete",
      "error",
    );
  });

  it("requires the vault to be unlocked before decrypting existing files", async () => {
    const onToast = vi.fn();
    const { result } = renderHook(() =>
      useFolderClientEncryptionActions({
        nodeId: "node-1",
        currentNode: makeNode({}),
        content: makeContent([
          makeFile("encrypted", { isClientEncrypted: "true" }),
        ]),
        onToast,
      }),
    );

    await act(async () => {
      await result.current.decryptEncryptedFiles();
    });

    expect(fetchServerSettings).not.toHaveBeenCalled();
    expect(decryptExistingFileWithTask).not.toHaveBeenCalled();
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.unlockRequired",
      "error",
    );
  });
});
