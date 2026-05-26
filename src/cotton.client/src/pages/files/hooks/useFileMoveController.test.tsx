import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { TFunction } from "i18next";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import { useFileMoveController } from "./useFileMoveController";

const mocks = vi.hoisted(() => ({
  cutItems: vi.fn(),
  pasteInto: vi.fn(),
  moveItems: vi.fn(),
}));

vi.mock("../../../shared/hooks/useMoveOperations", () => ({
  isMoveDrag: vi.fn(() => false),
  moveDragHasSourceParent: vi.fn(() => false),
  readMoveDragPayload: vi.fn(() => null),
  useMoveOperations: () => ({
    cutItems: mocks.cutItems,
    pasteInto: mocks.pasteInto,
    moveItems: mocks.moveItems,
    clearClipboard: vi.fn(),
  }),
}));

vi.mock("../../../shared/store/moveClipboardStore", () => ({
  useMoveClipboardStore: vi.fn(() => []),
}));

const t = ((key: string) => key) as TFunction;

const makeFileTile = (): FileSystemTile => ({
  kind: "file",
  file: {
    id: "file-1",
    createdAt: "2026-05-17T00:00:00Z",
    updatedAt: "2026-05-17T00:00:00Z",
    nodeId: "node-1",
    ownerId: "user-1",
    name: "plain.txt",
    contentType: "text/plain",
    sizeBytes: 10,
    metadata: {},
  },
});

describe("useFileMoveController", () => {
  it("leaves selection mode after cutting the current selection", () => {
    const onItemsCut = vi.fn();
    const showToast = vi.fn();
    const { result } = renderHook(() =>
      useFileMoveController({
        nodeId: "node-1",
        tiles: [makeFileTile()],
        selectedIds: new Set(["file-1"]),
        selectedCount: 1,
        goUpParentId: null,
        onItemsCut,
        showToast,
        t,
      }),
    );

    result.current.handleCutSelection();

    expect(mocks.cutItems).toHaveBeenCalledWith([
      expect.objectContaining({ id: "file-1", kind: "file" }),
    ]);
    expect(onItemsCut).toHaveBeenCalledOnce();
    expect(showToast).toHaveBeenCalledWith("move.toasts.cut");
  });
});
