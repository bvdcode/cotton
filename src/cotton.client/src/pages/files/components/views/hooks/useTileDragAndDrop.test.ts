import { act, renderHook } from "@testing-library/react";
import type { DragEvent } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  MOVE_DRAG_DATA_MIME,
  MOVE_DRAG_DATA_TYPE,
  writeMoveDragPayload,
} from "@shared/hooks/useMoveOperations";
import type {
  FileSystemTile,
  IFileListView,
} from "@shared/types/FileListViewTypes";
import { useTileDragAndDrop } from "./useTileDragAndDrop";

class FakeDataTransfer {
  private store = new Map<string, string>();
  effectAllowed: DataTransfer["effectAllowed"] = "uninitialized";
  dropEffect: DataTransfer["dropEffect"] = "none";

  setData(format: string, value: string): void {
    this.store.set(format, value);
  }

  getData(format: string): string {
    return this.store.get(format) ?? "";
  }

  clearData(): void {
    this.store.clear();
  }

  get types(): readonly string[] {
    return Array.from(this.store.keys());
  }
}

const makeDragEvent = (
  dataTransfer: FakeDataTransfer,
  currentTarget = document.createElement("div"),
  relatedTarget: EventTarget | null = null,
): DragEvent<HTMLDivElement> =>
  ({
    dataTransfer: dataTransfer as unknown as DataTransfer,
    currentTarget,
    relatedTarget,
    preventDefault: vi.fn(),
    stopPropagation: vi.fn(),
  }) as unknown as DragEvent<HTMLDivElement>;

const makeFolderTile = (
  id: string,
  parentId: string | null = "parent-1",
): FileSystemTile => ({
  kind: "folder",
  node: {
    id,
    name: `Folder ${id}`,
    layoutId: "layout-1",
    parentId,
    createdAt: "2026-05-17T00:00:00Z",
    updatedAt: "2026-05-17T00:00:01Z",
    metadata: {},
  },
});

const makeFileTile = (id: string, nodeId: string): FileSystemTile => ({
  kind: "file",
  file: {
    id,
    name: `${id}.txt`,
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

const makeMoveSupport = (
  currentParentId: string | null = "parent-1",
): NonNullable<IFileListView["moveSupport"]> => ({
  currentParentId,
  cutItemIds: new Set(["cut-file"]),
  onMove: vi.fn(),
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("useTileDragAndDrop drag start", () => {
  it("writes a folder move payload with the tile source parent", () => {
    const moveSupport = makeMoveSupport("fallback-parent");
    const { result } = renderHook(() =>
      useTileDragAndDrop({
        tiles: [makeFolderTile("folder-1", "folder-parent")],
        moveSupport,
      }),
    );
    const dataTransfer = new FakeDataTransfer();
    const event = makeDragEvent(dataTransfer);

    act(() => result.current.handleMoveDragStart("folder-1", event));

    expect(dataTransfer.effectAllowed).toBe("move");
    expect(dataTransfer.getData(MOVE_DRAG_DATA_TYPE)).toBe("1");
    expect(dataTransfer.types).toContain(
      `${MOVE_DRAG_DATA_TYPE}/folder-parent`,
    );
    expect(JSON.parse(dataTransfer.getData(MOVE_DRAG_DATA_MIME))).toEqual({
      items: [
        {
          id: "folder-1",
          kind: "folder",
          sourceParentId: "folder-parent",
        },
      ],
    });
  });

  it("writes a file move payload with the containing folder as source parent", () => {
    const moveSupport = makeMoveSupport("current-parent");
    const { result } = renderHook(() =>
      useTileDragAndDrop({
        tiles: [makeFileTile("file-1", "file-parent")],
        moveSupport,
      }),
    );
    const dataTransfer = new FakeDataTransfer();

    act(() =>
      result.current.handleMoveDragStart("file-1", makeDragEvent(dataTransfer)),
    );

    expect(JSON.parse(dataTransfer.getData(MOVE_DRAG_DATA_MIME))).toEqual({
      items: [
        {
          id: "file-1",
          kind: "file",
          sourceParentId: "file-parent",
          file: {
            name: "file-1.txt",
            contentType: "text/plain",
            sizeBytes: 1,
            metadata: {},
          },
        },
      ],
    });
  });

  it("prevents dragging unknown tiles or tiles without parent context", () => {
    const { result } = renderHook(() =>
      useTileDragAndDrop({
        tiles: [makeFolderTile("folder-1")],
        moveSupport: makeMoveSupport(null),
      }),
    );
    const event = makeDragEvent(new FakeDataTransfer());

    act(() => result.current.handleMoveDragStart("missing", event));

    expect(event.preventDefault).toHaveBeenCalled();
  });
});

describe("useTileDragAndDrop drag over and leave", () => {
  it("marks a valid folder target and sets move drop effect", () => {
    const { result } = renderHook(() =>
      useTileDragAndDrop({
        tiles: [makeFolderTile("target")],
        moveSupport: makeMoveSupport(),
      }),
    );
    const dataTransfer = new FakeDataTransfer();
    writeMoveDragPayload(dataTransfer as unknown as DataTransfer, {
      items: [{ id: "source", kind: "folder", sourceParentId: "parent-1" }],
    });
    const event = makeDragEvent(dataTransfer);

    act(() => result.current.handleMoveDragOver("target", event));

    expect(event.preventDefault).toHaveBeenCalled();
    expect(event.stopPropagation).toHaveBeenCalled();
    expect(dataTransfer.dropEffect).toBe("move");
    expect(result.current.dropTargetId).toBe("target");
  });

  it("rejects source parents and dragged items as drop targets", () => {
    const { result } = renderHook(() =>
      useTileDragAndDrop({
        tiles: [makeFolderTile("target")],
        moveSupport: makeMoveSupport(),
      }),
    );
    const dataTransfer = new FakeDataTransfer();
    writeMoveDragPayload(dataTransfer as unknown as DataTransfer, {
      items: [
        { id: "target", kind: "folder", sourceParentId: "parent-1" },
      ],
    });
    const itemEvent = makeDragEvent(dataTransfer);

    act(() => result.current.handleMoveDragOver("target", itemEvent));
    expect(itemEvent.preventDefault).not.toHaveBeenCalled();
    expect(result.current.dropTargetId).toBeNull();

    const sourceEvent = makeDragEvent(dataTransfer);
    act(() => result.current.handleMoveDragOver("parent-1", sourceEvent));
    expect(sourceEvent.preventDefault).not.toHaveBeenCalled();
    expect(result.current.dropTargetId).toBeNull();
  });

  it("keeps the drop target while moving inside the same tile and clears on leave", () => {
    const { result } = renderHook(() =>
      useTileDragAndDrop({
        tiles: [makeFolderTile("target")],
        moveSupport: makeMoveSupport(),
      }),
    );
    const dataTransfer = new FakeDataTransfer();
    writeMoveDragPayload(dataTransfer as unknown as DataTransfer, {
      items: [{ id: "source", kind: "file", sourceParentId: "parent-1" }],
    });
    const target = document.createElement("div");
    const child = document.createElement("span");
    target.appendChild(child);

    act(() =>
      result.current.handleMoveDragOver(
        "target",
        makeDragEvent(dataTransfer, target),
      ),
    );
    act(() =>
      result.current.handleMoveDragLeave(
        "target",
        makeDragEvent(dataTransfer, target, child),
      ),
    );
    expect(result.current.dropTargetId).toBe("target");

    act(() =>
      result.current.handleMoveDragLeave(
        "target",
        makeDragEvent(dataTransfer, target, document.createElement("div")),
      ),
    );
    expect(result.current.dropTargetId).toBeNull();
  });
});

describe("useTileDragAndDrop drop", () => {
  it("reads the move payload, filters no-op items, and invokes onMove", () => {
    const moveSupport = makeMoveSupport();
    const { result } = renderHook(() =>
      useTileDragAndDrop({
        tiles: [makeFolderTile("target")],
        moveSupport,
      }),
    );
    const dataTransfer = new FakeDataTransfer();
    writeMoveDragPayload(dataTransfer as unknown as DataTransfer, {
      items: [
        { id: "move-me", kind: "folder", sourceParentId: "parent-1" },
        { id: "already-here", kind: "file", sourceParentId: "target" },
        { id: "target", kind: "folder", sourceParentId: "parent-1" },
      ],
    });
    const event = makeDragEvent(dataTransfer);

    act(() => result.current.handleMoveDrop("target", event));

    expect(event.preventDefault).toHaveBeenCalled();
    expect(event.stopPropagation).toHaveBeenCalled();
    expect(moveSupport.onMove).toHaveBeenCalledWith(
      [{ id: "move-me", kind: "folder", sourceParentId: "parent-1" }],
      "target",
    );
    expect(result.current.dropTargetId).toBeNull();
  });

  it("ignores malformed or empty move payloads", () => {
    const moveSupport = makeMoveSupport();
    const { result } = renderHook(() =>
      useTileDragAndDrop({
        tiles: [makeFolderTile("target")],
        moveSupport,
      }),
    );
    const dataTransfer = new FakeDataTransfer();
    dataTransfer.setData(MOVE_DRAG_DATA_TYPE, "1");
    dataTransfer.setData(MOVE_DRAG_DATA_MIME, "{not-json");

    act(() =>
      result.current.handleMoveDrop(
        "target",
        makeDragEvent(dataTransfer),
      ),
    );

    expect(moveSupport.onMove).not.toHaveBeenCalled();
  });
});
