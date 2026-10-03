import { act, fireEvent, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createInstance } from "i18next";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import { useFileMoveController } from "./useFileMoveController";
import { useMoveClipboardStore } from "@shared/store/moveClipboardStore";

const mocks = vi.hoisted(() => ({
  cutItems: vi.fn(),
  copyItems: vi.fn(),
  pasteInto: vi.fn(),
  moveItems: vi.fn(),
}));

vi.mock("../../../shared/hooks/useMoveOperations", () => ({
  isMoveDrag: vi.fn(() => false),
  moveDragHasSourceParent: vi.fn(() => false),
  readMoveDragPayload: vi.fn(() => null),
  useMoveOperations: () => ({
    cutItems: mocks.cutItems,
    copyItems: mocks.copyItems,
    pasteInto: mocks.pasteInto,
    moveItems: mocks.moveItems,
    clearClipboard: vi.fn(),
  }),
}));

beforeEach(() => {
  vi.clearAllMocks();
  useMoveClipboardStore.getState().clear();
});

const translations = createInstance();
await translations.init({ lng: "en", resources: {} });
const t = translations.t;

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
  it("copies with Ctrl+C, leaves the original undimmed and preserves text-field shortcuts", () => {
    const onClipboardSet = vi.fn();
    const { result } = renderHook(() =>
      useFileMoveController({
        nodeId: "node-1",
        tiles: [makeFileTile()],
        selectedIds: new Set(["file-1"]),
        selectedCount: 1,
        goUpParentId: null,
        onClipboardSet,
        showToast: vi.fn(),
        t,
      }),
    );
    fireEvent.keyDown(window, { key: "c", code: "KeyC", ctrlKey: true });
    expect(mocks.copyItems).toHaveBeenCalledWith([
      expect.objectContaining({ id: "file-1" }),
    ]);
    expect(onClipboardSet).toHaveBeenCalledOnce();
    act(() =>
      useMoveClipboardStore
        .getState()
        .setItems(
          [{ id: "file-1", kind: "file", sourceParentId: "node-1" }],
          "copy",
        ),
    );
    expect(result.current.moveSupport?.cutItemIds.size).toBe(0);
    const input = document.createElement("input");
    document.body.append(input);
    input.focus();
    fireEvent.keyDown(input, { key: "c", code: "KeyC", ctrlKey: true });
    expect(mocks.copyItems).toHaveBeenCalledOnce();
    input.remove();
  });
  it("leaves selection mode after cutting the current selection", () => {
    const onClipboardSet = vi.fn();
    const showToast = vi.fn();
    const { result } = renderHook(() =>
      useFileMoveController({
        nodeId: "node-1",
        tiles: [makeFileTile()],
        selectedIds: new Set(["file-1"]),
        selectedCount: 1,
        goUpParentId: null,
        onClipboardSet,
        showToast,
        t,
      }),
    );

    result.current.handleCutSelection();

    expect(mocks.cutItems).toHaveBeenCalledWith([
      expect.objectContaining({ id: "file-1", kind: "file" }),
    ]);
    expect(onClipboardSet).toHaveBeenCalledOnce();
    expect(showToast).toHaveBeenCalledWith("move.toasts.cut");
  });
});
