import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import type { FileListSource } from "@shared/types/fileListSource";

const interactionMock = { focusedFileId: "file-2" };
const useFileInteractionHandlersMock = vi.fn<
  (args: unknown) => typeof interactionMock
>(() => interactionMock);

vi.mock("@shared/hooks/useFileInteractionHandlers", () => ({
  useFileInteractionHandlers: (args: unknown) =>
    useFileInteractionHandlersMock(args),
}));

const { useFileListPageLogic, useFileListSourceLogic } =
  await import("./useFileListPageLogic");

const makeFolderTile = (id: string): FileSystemTile => ({
  kind: "folder",
  node: {
    id,
    name: `Folder ${id}`,
    layoutId: "layout-1",
    parentId: null,
    createdAt: "2026-05-17T00:00:00Z",
    updatedAt: "2026-05-17T00:00:01Z",
    metadata: {},
  },
});

const makeFileTile = (
  id: string,
  name: string,
  nodeId = "node-1",
): FileSystemTile => ({
  kind: "file",
  file: {
    id,
    name,
    nodeId,
    ownerId: "owner-1",
    contentType: "text/plain",
    sizeBytes: 1,
    metadata: {},
    previewHashEncryptedHex: null,
    createdAt: "2026-05-17T00:00:00Z",
    updatedAt: "2026-05-17T00:00:01Z",
  },
});

const makeSource = (
  overrides: Partial<FileListSource> = {},
): FileListSource => ({
  loading: false,
  error: null,
  tiles: [],
  totalCount: 0,
  isContentTransitioning: false,
  hasContent: false,
  ...overrides,
});

describe("useFileListSourceLogic", () => {
  it("passes through source state and derives source capabilities", () => {
    const source = makeSource({
      loading: true,
      error: "failed",
      totalCount: 42,
      isContentTransitioning: true,
      hasContent: true,
      tiles: [makeFolderTile("folder-1")],
    });

    const { result } = renderHook(() =>
      useFileListSourceLogic({ source, sourceKind: "trash" }),
    );

    expect(result.current).toMatchObject({
      tiles: source.tiles,
      loading: true,
      error: "failed",
      totalCount: 42,
      isContentTransitioning: true,
      hasContent: true,
      capabilities: {
        canUpload: false,
        canDelete: true,
        canRestore: true,
        canRename: false,
        canCutPaste: false,
        isReadOnly: false,
      },
    });
  });

  it("extracts file manifests and sorts them by natural filename", () => {
    const source = makeSource({
      tiles: [
        makeFolderTile("folder-1"),
        makeFileTile("file-10", "file10.txt"),
        makeFileTile("file-2", "file2.txt"),
        makeFileTile("file-1", "file1.txt"),
      ],
    });

    const { result } = renderHook(() =>
      useFileListSourceLogic({ source, sourceKind: "nodes" }),
    );

    expect(result.current.sortedFiles.map((file) => file.name)).toEqual([
      "file1.txt",
      "file2.txt",
      "file10.txt",
    ]);
  });

  it.each([
    ["nodes", { canUpload: true, canDelete: true, isReadOnly: false }],
    ["trash", { canRestore: true, canUpload: false }],
    ["search", { canDelete: false, canRename: false }],
    ["share", { isReadOnly: true, canDelete: false }],
  ] as const)("derives %s capabilities", (sourceKind, expected) => {
    const { result } = renderHook(() =>
      useFileListSourceLogic({
        source: makeSource(),
        sourceKind,
      }),
    );

    expect(result.current.capabilities).toMatchObject(expected);
  });
});

describe("useFileListPageLogic", () => {
  it("builds interaction handlers from sorted files", () => {
    const source = makeSource({
      tiles: [makeFileTile("file-b", "b.txt"), makeFileTile("file-a", "a.txt")],
    });

    const { result } = renderHook(() =>
      useFileListPageLogic({ source, sourceKind: "nodes" }),
    );

    expect(result.current.interaction).toBe(interactionMock);
    expect(useFileInteractionHandlersMock).toHaveBeenCalledWith({
      sortedFiles: [
        expect.objectContaining({ id: "file-a", name: "a.txt" }),
        expect.objectContaining({ id: "file-b", name: "b.txt" }),
      ],
    });
  });
});
