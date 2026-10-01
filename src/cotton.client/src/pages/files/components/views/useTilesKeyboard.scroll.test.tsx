import { cleanup, fireEvent, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import type { FileSystemTile } from "../../../../shared/types/FileListViewTypes";
import { createFile } from "../../../../test/fileFixtures";
import { useTilesKeyboard } from "./useTilesKeyboard";

const tiles: FileSystemTile[] = Array.from({ length: 300 }, (_, index) => ({
  kind: "file",
  file: createFile({ id: `file-${index}` }),
}));

beforeEach(() => {
  vi.spyOn(window, "requestAnimationFrame").mockReturnValue(1);
  vi.spyOn(window, "cancelAnimationFrame").mockImplementation(() => undefined);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

it("only scrolls for keyboard navigation, not appended pages or layout changes", () => {
  const containerRef = { current: document.createElement("div") };
  const operations = {
    isRenaming: () => false,
    getRenamingName: () => "",
    onRenamingNameChange: vi.fn(),
    onClick: vi.fn(),
  };
  const { result, rerender } = renderHook(
    ({ count, columns }) =>
      useTilesKeyboard({
        tiles: tiles.slice(0, count),
        selectedIds: undefined,
        columns,
        containerRef,
        shouldVirtualize: true,
        folderOperations: operations,
        fileOperations: operations,
        readOnly: false,
        onNavigateBack: undefined,
      }),
    { initialProps: { count: 100, columns: 4 } },
  );
  const scrollToIndex = vi.fn();
  result.current.virtuosoRef.current = {
    autoscrollToBottom: vi.fn(),
    getState: vi.fn(),
    scrollBy: vi.fn(),
    scrollIntoView: vi.fn(),
    scrollTo: vi.fn(),
    scrollToIndex,
  };

  rerender({ count: 200, columns: 4 });
  expect(scrollToIndex).not.toHaveBeenCalled();
  rerender({ count: 300, columns: 4 });
  expect(scrollToIndex).not.toHaveBeenCalled();
  rerender({ count: 300, columns: 3 });
  expect(scrollToIndex).not.toHaveBeenCalled();

  fireEvent.keyDown(window, { key: "ArrowDown" });
  expect(scrollToIndex).toHaveBeenCalledExactlyOnceWith({
    index: 1,
    align: "center",
    behavior: "auto",
  });
});
