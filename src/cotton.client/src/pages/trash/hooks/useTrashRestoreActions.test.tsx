import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import type { BatchItem, BatchItemResult } from "@shared/api/batchItemsApi";

const mocks = vi.hoisted(() => ({
  restore: vi.fn<(items: BatchItem[]) => Promise<BatchItemResult[]>>(),
  refreshContent: vi.fn(() => Promise.resolve()),
  deselectAll: vi.fn(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock("../../../shared/api/batchItemsApi", async (importOriginal) => {
  const actual =
    await importOriginal<typeof import("../../../shared/api/batchItemsApi")>();
  return {
    ...actual,
    batchItemsApi: { ...actual.batchItemsApi, restore: mocks.restore },
  };
});

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
    mocks.restore.mockResolvedValue([
      {
        id: "folder-1",
        kind: "Folder",
        failed: false,
        deleted: false,
        restoreOutcome: { status: "Restored" },
      },
    ]);
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
    expect(mocks.restore).not.toHaveBeenCalled();

    await act(async () => {
      result.current.handlePromptAnswer({ action: "apply" });
      await restorePromise;
    });

    expect(mocks.restore).toHaveBeenCalledWith([
      {
        id: "folder-1",
        kind: "Folder",
        createMissingParents: false,
        overwrite: false,
      },
    ]);
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

    expect(mocks.restore).not.toHaveBeenCalled();
  });

  it("restores 65 items in bounded batches and retries only the conflicting item", async () => {
    const tiles: FileSystemTile[] = Array.from({ length: 65 }, (_, index) => ({
      kind: "file",
      file: { ...fileTile.file, id: `file-${index}` },
    }));
    mocks.restore.mockImplementation(async (items: BatchItem[]) =>
      items.map((item) => ({
        ...item,
        deleted: false,
        failed: false,
        restoreOutcome:
          item.id === "file-26" && !item.overwrite
            ? {
                status: "Conflict",
                conflictKind: "File",
                conflictName: "photo.mov",
              }
            : { status: "Restored" },
      })),
    );
    const { result } = renderHook(() =>
      useTrashRestoreActions({
        fileSelection: makeSelection(
          tiles.map((tile) =>
            tile.kind === "file" ? tile.file.id : tile.node.id,
          ),
        ),
        tiles,
        refreshContent: mocks.refreshContent,
      }),
    );
    let restoring: Promise<void> | undefined;
    act(() => {
      restoring = result.current.restoreSelected();
    });
    await waitFor(() =>
      expect(result.current.activePrompt?.prompt.kind).toBe("confirm"),
    );
    act(() => {
      result.current.handlePromptAnswer({ action: "apply", applyToAll: true });
    });
    await waitFor(() =>
      expect(result.current.activePrompt?.prompt.kind).toBe("conflict"),
    );
    expect(result.current.activePrompt?.item.id).toBe("file-26");
    await act(async () => {
      result.current.handlePromptAnswer({ action: "apply" });
      await restoring;
    });

    expect(
      mocks.restore.mock.calls.map(([items]: [BatchItem[]]) => items.length),
    ).toEqual([25, 25, 15, 1]);
    expect(mocks.restore).toHaveBeenLastCalledWith([
      {
        id: "file-26",
        kind: "File",
        createMissingParents: false,
        overwrite: true,
      },
    ]);
    expect(result.current.errors).toEqual([]);
    expect(result.current.progress).toEqual({ current: 65, total: 65 });
    expect(mocks.refreshContent).toHaveBeenCalledOnce();
  });

  it("continues later batches and reports only the batch that failed", async () => {
    const tiles: FileSystemTile[] = Array.from({ length: 26 }, (_, index) => ({
      kind: "file",
      file: { ...fileTile.file, id: `file-${index}` },
    }));
    mocks.restore.mockRejectedValueOnce(new Error("Connection interrupted"));
    mocks.restore.mockImplementation(async (items: BatchItem[]) =>
      items.map((item) => ({
        ...item,
        deleted: false,
        failed: false,
        restoreOutcome: { status: "Restored" },
      })),
    );
    const { result } = renderHook(() =>
      useTrashRestoreActions({
        fileSelection: makeSelection(
          tiles.map((tile) =>
            tile.kind === "file" ? tile.file.id : tile.node.id,
          ),
        ),
        tiles,
        refreshContent: mocks.refreshContent,
      }),
    );
    let restoring: Promise<void> | undefined;
    act(() => {
      restoring = result.current.restoreSelected();
    });
    await waitFor(() =>
      expect(result.current.activePrompt?.prompt.kind).toBe("confirm"),
    );
    await act(async () => {
      result.current.handlePromptAnswer({ action: "apply", applyToAll: true });
      await restoring;
    });
    expect(
      mocks.restore.mock.calls.map(([items]: [BatchItem[]]) => items.length),
    ).toEqual([25, 1]);
    expect(result.current.errors).toHaveLength(25);
    expect(result.current.progress).toEqual({ current: 26, total: 26 });
  });
});
