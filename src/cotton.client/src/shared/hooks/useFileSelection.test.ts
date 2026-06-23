import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import { useFileSelection } from "./useFileSelection";

const makeFolderTile = (id: string): FileSystemTile => ({
  kind: "folder",
  node: {
    id,
    layoutId: "layout-id",
    parentId: null,
    name: `Folder ${id}`,
    createdAt: "2026-05-17T00:00:00Z",
    updatedAt: "2026-05-17T00:00:00Z",
    metadata: {},
  },
});

describe("useFileSelection", () => {
  it("starts inactive with no selected items", () => {
    const { result } = renderHook(() => useFileSelection());

    expect(result.current.selectionMode).toBe(false);
    expect(result.current.selectedCount).toBe(0);
    expect(result.current.isSelected("a")).toBe(false);
  });

  it("toggles selection mode and clears selection on exit", () => {
    const { result } = renderHook(() => useFileSelection());

    act(() => result.current.toggleSelectionMode());
    act(() => result.current.toggleItem("a"));
    expect(result.current.selectionMode).toBe(true);
    expect(result.current.selectedCount).toBe(1);

    act(() => result.current.toggleSelectionMode());
    expect(result.current.selectionMode).toBe(false);
    expect(result.current.selectedCount).toBe(0);
  });

  it("adds and removes selected ids", () => {
    const { result } = renderHook(() => useFileSelection());
    act(() => result.current.toggleSelectionMode());

    act(() => result.current.toggleItem("a"));
    act(() => result.current.toggleItem("b"));
    expect(result.current.selectedCount).toBe(2);
    expect(result.current.isSelected("a")).toBe(true);
    expect(result.current.isSelected("b")).toBe(true);

    act(() => result.current.toggleItem("a"));
    expect(result.current.selectedCount).toBe(1);
    expect(result.current.isSelected("a")).toBe(false);
  });

  it("exits selection mode when the last selected id is removed", () => {
    const { result } = renderHook(() => useFileSelection());
    act(() => result.current.toggleSelectionMode());
    act(() => result.current.toggleItem("a"));

    act(() => result.current.toggleItem("a"));

    expect(result.current.selectionMode).toBe(false);
    expect(result.current.selectedCount).toBe(0);
  });

  it("shift-click selects a contiguous range from the last clicked id", () => {
    const { result } = renderHook(() => useFileSelection());
    const orderedIds = ["a", "b", "c", "d", "e"];
    act(() => result.current.toggleSelectionMode());

    act(() => result.current.toggleItem("b"));
    act(() => result.current.toggleItem("d", { shiftKey: true, orderedIds }));

    expect(result.current.selectedCount).toBe(3);
    expect(result.current.isSelected("b")).toBe(true);
    expect(result.current.isSelected("c")).toBe(true);
    expect(result.current.isSelected("d")).toBe(true);
  });

  it("selectAll uses ids from folder and file tiles", () => {
    const { result } = renderHook(() => useFileSelection());
    act(() => result.current.toggleSelectionMode());

    act(() =>
      result.current.selectAll([
        makeFolderTile("a"),
        makeFolderTile("b"),
        makeFolderTile("c"),
      ]),
    );

    expect(result.current.selectedCount).toBe(3);
    expect(result.current.isSelected("a")).toBe(true);
    expect(result.current.isSelected("b")).toBe(true);
    expect(result.current.isSelected("c")).toBe(true);
  });

  it("deselectAll clears selection and exits selection mode", () => {
    const { result } = renderHook(() => useFileSelection());
    act(() => result.current.toggleSelectionMode());
    act(() => result.current.toggleItem("a"));
    act(() => result.current.toggleItem("b"));

    act(() => result.current.deselectAll());

    expect(result.current.selectionMode).toBe(false);
    expect(result.current.selectedCount).toBe(0);
  });
});
