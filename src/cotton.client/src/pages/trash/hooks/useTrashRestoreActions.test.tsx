import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";

const mocks = vi.hoisted(() => ({
  restoreNode: vi.fn(),
  restoreFile: vi.fn(),
  refreshContent: vi.fn(() => Promise.resolve()),
  deselectAll: vi.fn(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock("../../../shared/api/nodesApi", () => ({
  nodesApi: {
    restoreNode: mocks.restoreNode,
  },
}));

vi.mock("../../../shared/api/filesApi", () => ({
  filesApi: {
    restoreFile: mocks.restoreFile,
  },
}));

const { useTrashRestoreActions } = await import("./useTrashRestoreActions");

const makeSelection = (selectedIds: string[] = []): FileSelectionState => ({
  selectionMode: selectedIds.length > 0,
  selectedIds: new Set(selectedIds),
  selectedCount: selectedIds.length,
  toggleSelectionMode: vi.fn(),
  toggleItem: vi.fn(),
  selectAll: vi.fn(),
  deselectAll: mocks.deselectAll,
  isSelected: (id: string) => selectedIds.includes(id),
});

const folderTile: FileSystemTile = {
  kind: "folder",
  node: {
    id: "folder-1",
    layoutId: "layout-1",
    parentId: "trash-wrapper-1",
    name: "Reports",
    metadata: { originalParentPath: "Docs" },
    createdAt: "2026-05-18T00:00:00Z",
    updatedAt: "2026-05-18T00:00:00Z",
  },
};

const fileTile: FileSystemTile = {
  kind: "file",
  file: {
    id: "file-1",
    nodeId: "trash-wrapper-2",
    ownerId: "user-1",
    name: "photo.mov",
    contentType: "video/quicktime",
    sizeBytes: 42,
    metadata: { originalParentPath: "Media" },
    createdAt: "2026-05-18T00:00:00Z",
    updatedAt: "2026-05-18T00:00:00Z",
  },
};

describe("useTrashRestoreActions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.restoreNode.mockResolvedValue({ status: "Restored" });
    mocks.restoreFile.mockResolvedValue({ status: "Restored" });
  });

  it("asks for confirmation with restore destination before calling the API", async () => {
    const { result } = renderHook(() =>
      useTrashRestoreActions({
        fileSelection: makeSelection(),
        tiles: [folderTile],
        refreshContent: mocks.refreshContent,
      }),
    );

    let restorePromise: Promise<void> | undefined;
    act(() => {
      restorePromise = result.current.restoreItem({
        id: "folder-1",
        kind: "folder",
        name: "Reports",
      });
    });

    await waitFor(() =>
      expect(result.current.activePrompt).toMatchObject({
        item: { id: "folder-1" },
        prompt: { kind: "confirm", restorePath: "Docs" },
      }),
    );
    expect(mocks.restoreNode).not.toHaveBeenCalled();

    await act(async () => {
      result.current.handlePromptAnswer({ action: "apply" });
      await restorePromise;
    });

    expect(mocks.restoreNode).toHaveBeenCalledWith("folder-1", {
      createMissingParents: false,
      overwrite: false,
    });
  });

  it("skips restore when the confirmation is rejected", async () => {
    const { result } = renderHook(() =>
      useTrashRestoreActions({
        fileSelection: makeSelection(),
        tiles: [fileTile],
        refreshContent: mocks.refreshContent,
      }),
    );

    let restorePromise: Promise<void> | undefined;
    act(() => {
      restorePromise = result.current.restoreItem({
        id: "file-1",
        kind: "file",
        name: "photo.mov",
      });
    });

    await waitFor(() =>
      expect(result.current.activePrompt).toMatchObject({
        item: { id: "file-1" },
        prompt: { kind: "confirm", restorePath: "Media" },
      }),
    );

    await act(async () => {
      result.current.handlePromptAnswer({ action: "skip" });
      await restorePromise;
    });

    expect(mocks.restoreFile).not.toHaveBeenCalled();
  });
});
