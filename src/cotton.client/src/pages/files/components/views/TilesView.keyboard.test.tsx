import { cleanup, fireEvent, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type {
  FileOperations,
  FileSystemTile,
  FolderOperations,
  IFileListView,
} from "@shared/types/FileListViewTypes";
import { TilesView } from "./TilesView";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

class TestResizeObserver {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

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
  name = `${id}.txt`,
  contentType = "text/plain",
): FileSystemTile => ({
  kind: "file",
  file: {
    id,
    name,
    nodeId: "node-1",
    ownerId: "owner-1",
    contentType,
    sizeBytes: 10,
    metadata: {},
    previewHashEncryptedHex: null,
    createdAt: "2026-05-17T00:00:00Z",
    updatedAt: "2026-05-17T00:00:01Z",
  },
});

const makeFolderOperations = (): FolderOperations => ({
  isRenaming: () => false,
  getRenamingName: () => "",
  onRenamingNameChange: vi.fn(),
  onClick: vi.fn(),
  onStartRename: vi.fn(),
  onDelete: vi.fn(),
});

const makeFileOperations = (): FileOperations => ({
  isRenaming: () => false,
  getRenamingName: () => "",
  onRenamingNameChange: vi.fn(),
  onClick: vi.fn(),
  onMediaClick: vi.fn(),
  onStartRename: vi.fn(),
  onDelete: vi.fn(),
});

const renderTilesView = (
  overrides: Partial<IFileListView> = {},
): {
  folderOperations: FolderOperations;
  fileOperations: FileOperations;
} => {
  const folderOperations = overrides.folderOperations ?? makeFolderOperations();
  const fileOperations = overrides.fileOperations ?? makeFileOperations();

  render(
    <TilesView
      tiles={overrides.tiles ?? []}
      folderOperations={folderOperations}
      fileOperations={fileOperations}
      isCreatingFolder={false}
      newFolderName=""
      onNewFolderNameChange={vi.fn()}
      onConfirmNewFolder={async () => undefined}
      onCancelNewFolder={vi.fn()}
      folderNamePlaceholder="Folder name"
      fileNamePlaceholder="File name"
      {...overrides}
    />,
  );

  return { folderOperations, fileOperations };
};

beforeEach(() => {
  Object.defineProperty(window, "ResizeObserver", {
    configurable: true,
    writable: true,
    value: TestResizeObserver,
  });
  Object.defineProperty(globalThis, "ResizeObserver", {
    configurable: true,
    writable: true,
    value: TestResizeObserver,
  });
  vi.spyOn(window, "requestAnimationFrame").mockImplementation((callback) => {
    callback(0);
    return 0;
  });
  vi.spyOn(window, "cancelAnimationFrame").mockImplementation(() => undefined);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("TilesView keyboard behavior", () => {
  it("opens the first folder on Enter from document context", () => {
    const { folderOperations } = renderTilesView({
      tiles: [makeFolderTile("folder-1")],
    });

    fireEvent.keyDown(window, { key: "Enter" });

    expect(folderOperations.onClick).toHaveBeenCalledWith("folder-1");
  });

  it("opens regular files and media files through the correct handlers", () => {
    const { fileOperations } = renderTilesView({
      tiles: [
        makeFileTile("text-file", "notes.txt", "text/plain"),
        makeFileTile("image-file", "photo.jpg", "image/jpeg"),
      ],
      selectedIds: new Set(["image-file"]),
    });

    fireEvent.keyDown(window, { key: "Enter" });

    expect(fileOperations.onMediaClick).toHaveBeenCalledWith("image-file");
    expect(fileOperations.onClick).not.toHaveBeenCalled();
  });

  it("starts rename and delete for the focused folder", () => {
    const { folderOperations } = renderTilesView({
      tiles: [makeFolderTile("folder-1")],
    });

    fireEvent.keyDown(window, { key: "F2" });
    fireEvent.keyDown(window, { key: "Delete" });

    expect(folderOperations.onStartRename).toHaveBeenCalledWith(
      "folder-1",
      "Folder folder-1",
    );
    expect(folderOperations.onDelete).toHaveBeenCalledWith(
      "folder-1",
      "Folder folder-1",
    );
  });

  it("does not rename or delete in read-only mode", () => {
    const { folderOperations } = renderTilesView({
      tiles: [makeFolderTile("folder-1")],
      readOnly: true,
    });

    fireEvent.keyDown(window, { key: "F2" });
    fireEvent.keyDown(window, { key: "Delete" });

    expect(folderOperations.onStartRename).not.toHaveBeenCalled();
    expect(folderOperations.onDelete).not.toHaveBeenCalled();
  });

  it("navigates back on Backspace when a handler is supplied", () => {
    const onNavigateBack = vi.fn();
    renderTilesView({
      tiles: [makeFolderTile("folder-1")],
      onNavigateBack,
    });

    fireEvent.keyDown(window, { key: "Backspace" });

    expect(onNavigateBack).toHaveBeenCalledTimes(1);
  });

  it("ignores shortcuts while focus is in an external input", () => {
    const { folderOperations } = renderTilesView({
      tiles: [makeFolderTile("folder-1")],
    });
    const input = document.createElement("input");
    document.body.appendChild(input);
    input.focus();

    fireEvent.keyDown(window, { key: "Enter" });

    expect(folderOperations.onClick).not.toHaveBeenCalled();
    input.remove();
  });

  it("moves active keyboard target with arrow keys", () => {
    const { folderOperations } = renderTilesView({
      tiles: [makeFolderTile("first"), makeFolderTile("second")],
    });

    fireEvent.keyDown(window, { key: "ArrowRight" });
    fireEvent.keyDown(window, { key: "Enter" });

    expect(folderOperations.onClick).toHaveBeenCalledWith("second");
  });
});
