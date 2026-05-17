import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  renameDeleteOptions: undefined as
    | {
        renameFile: (fileId: string, newName: string) => Promise<boolean | void>;
      }
    | undefined,
  renameFile: vi.fn(),
  updateFileMetadata: vi.fn(),
  refreshNodeContent: vi.fn(() => Promise.resolve()),
  encryptDisplayMeta: vi.fn(async () => "encrypted-display-meta"),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock("../../../shared/api/filesApi", () => ({
  filesApi: {
    renameFile: mocks.renameFile,
    updateFileMetadata: mocks.updateFileMetadata,
    deleteFile: vi.fn(),
  },
}));

vi.mock("../../../shared/crypto", () => ({
  DISPLAY_META_KEY: "en",
  ENCRYPTED_CONTENT_TYPE: "application/octet-stream",
  encryptDisplayMeta: mocks.encryptDisplayMeta,
  getOriginalContentType: (metadata: Record<string, string> | undefined) =>
    metadata?.originalContentType,
  isFileEncrypted: (metadata: Record<string, string> | undefined) =>
    metadata?.isClientEncrypted === "true",
}));

vi.mock("../../../shared/hooks/useFileRenameDeleteOperations", () => ({
  useFileRenameDeleteOperations: (options: unknown) => {
    mocks.renameDeleteOptions = options as typeof mocks.renameDeleteOptions;
    return {};
  },
}));

vi.mock("../../../shared/store/nodesActions", () => ({
  refreshNodeContent: mocks.refreshNodeContent,
}));

const { useNodesStore } = await import("../../../shared/store/nodesStore");
const { useFileOperations } = await import("./useFileOperations");

const currentNode = {
  id: "node-1",
  layoutId: "layout-1",
  parentId: null,
  name: "Vault",
  metadata: {},
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
};

const makeContent = (fileMetadata: Record<string, string>) => ({
  id: "node-1",
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  nodes: [],
  files: [
    {
      id: "file-1",
      nodeId: "node-1",
      ownerId: "owner-1",
      name: "old-name.txt",
      contentType: "application/octet-stream",
      sizeBytes: 12,
      metadata: fileMetadata,
      requiresVideoTranscoding: false,
      previewHashEncryptedHex: null,
      createdAt: "2026-05-17T00:00:00Z",
      updatedAt: "2026-05-17T00:00:00Z",
    },
  ],
});

describe("useFileOperations", () => {
  afterEach(() => {
    useNodesStore.getState().reset();
    mocks.renameDeleteOptions = undefined;
    vi.clearAllMocks();
  });

  it("renames encrypted files by updating encrypted display metadata", async () => {
    useNodesStore.setState({
      currentNode,
      contentByNodeId: {
        "node-1": makeContent({
          isClientEncrypted: "true",
          originalContentType: "image/png",
          en: "old-display-meta",
        }),
      },
    });
    renderHook(() => useFileOperations());

    await act(async () => {
      await mocks.renameDeleteOptions?.renameFile("file-1", "new-name.png");
    });

    expect(mocks.encryptDisplayMeta).toHaveBeenCalledWith({
      name: "new-name.png",
      contentType: "image/png",
    });
    expect(mocks.updateFileMetadata).toHaveBeenCalledWith("file-1", {
      en: "encrypted-display-meta",
    });
    expect(mocks.renameFile).not.toHaveBeenCalled();
    expect(mocks.refreshNodeContent).toHaveBeenCalledWith("node-1");
  });

  it("keeps plaintext files on the normal rename endpoint", async () => {
    useNodesStore.setState({
      currentNode,
      contentByNodeId: {
        "node-1": makeContent({}),
      },
    });
    renderHook(() => useFileOperations());

    await act(async () => {
      await mocks.renameDeleteOptions?.renameFile("file-1", "new-name.txt");
    });

    expect(mocks.renameFile).toHaveBeenCalledWith("file-1", {
      name: "new-name.txt",
    });
    expect(mocks.updateFileMetadata).not.toHaveBeenCalled();
  });
});
