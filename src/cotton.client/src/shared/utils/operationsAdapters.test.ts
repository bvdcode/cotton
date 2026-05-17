import { describe, expect, it, vi } from "vitest";
import {
  buildFileOperations,
  buildFolderOperations,
} from "./operationsAdapters";

const makeFolderHook = () => ({
  renamingFolderId: null as string | null,
  renamingFolderName: "",
  setRenamingFolderName: vi.fn(),
  handleRenameFolder: vi.fn(),
  handleConfirmRename: vi.fn().mockResolvedValue(undefined),
  handleCancelRename: vi.fn(),
  handleDeleteFolder: vi.fn().mockResolvedValue(undefined),
});

const makeFileHook = () => ({
  renamingFileId: null as string | null,
  renamingFileName: "",
  setRenamingFileName: vi.fn(),
  handleRenameFile: vi.fn(),
  handleConfirmRename: vi.fn().mockResolvedValue(undefined),
  handleCancelRename: vi.fn(),
  handleDeleteFile: vi.fn().mockResolvedValue(undefined),
});

describe("buildFolderOperations", () => {
  it("wires rename state to the underlying hook", () => {
    const hook = makeFolderHook();
    hook.renamingFolderId = "folder-1";
    hook.renamingFolderName = "Drafts";

    const ops = buildFolderOperations(hook, vi.fn());

    expect(ops.isRenaming("folder-1")).toBe(true);
    expect(ops.isRenaming("other")).toBe(false);
    expect(ops.getRenamingName()).toBe("Drafts");

    ops.onRenamingNameChange("Photos");
    expect(hook.setRenamingFolderName).toHaveBeenCalledWith("Photos");
  });

  it("forwards folder actions to the right callbacks", () => {
    const hook = makeFolderHook();
    const onClick = vi.fn();
    const onShare = vi.fn().mockResolvedValue(undefined);
    const onCut = vi.fn();
    const ops = buildFolderOperations(hook, onClick, onShare, onCut);

    ops.onClick("folder-2");
    ops.onStartRename?.("folder-2", "Photos");
    ops.onConfirmRename?.();
    ops.onCancelRename?.();
    ops.onDelete?.("folder-2", "Photos");
    ops.onShare?.("folder-2", "Photos");
    ops.onCut?.("folder-2");

    expect(onClick).toHaveBeenCalledWith("folder-2");
    expect(hook.handleRenameFolder).toHaveBeenCalledWith("folder-2", "Photos");
    expect(hook.handleConfirmRename).toHaveBeenCalled();
    expect(hook.handleCancelRename).toHaveBeenCalled();
    expect(hook.handleDeleteFolder).toHaveBeenCalledWith("folder-2", "Photos");
    expect(onShare).toHaveBeenCalledWith("folder-2", "Photos");
    expect(onCut).toHaveBeenCalledWith("folder-2");
  });

  it("omits optional folder actions when handlers are not supplied", () => {
    const ops = buildFolderOperations(makeFolderHook(), vi.fn());

    expect(ops.onShare).toBeUndefined();
    expect(ops.onCut).toBeUndefined();
  });
});

describe("buildFileOperations", () => {
  it("wires rename state to the underlying hook", () => {
    const hook = makeFileHook();
    hook.renamingFileId = "file-1";
    hook.renamingFileName = "report.pdf";

    const ops = buildFileOperations(hook, { onClick: vi.fn() });

    expect(ops.isRenaming("file-1")).toBe(true);
    expect(ops.isRenaming("other")).toBe(false);
    expect(ops.getRenamingName()).toBe("report.pdf");

    ops.onRenamingNameChange("next.pdf");
    expect(hook.setRenamingFileName).toHaveBeenCalledWith("next.pdf");
  });

  it("forwards file actions to the right callbacks", () => {
    const hook = makeFileHook();
    const onClick = vi.fn();
    const onDownload = vi.fn().mockResolvedValue(undefined);
    const onShare = vi.fn().mockResolvedValue(undefined);
    const onCut = vi.fn();
    const onMediaClick = vi.fn();
    const ops = buildFileOperations(hook, {
      onClick,
      onDownload,
      onShare,
      onCut,
      onMediaClick,
    });

    ops.onClick("file-1", "report.pdf", 1024);
    ops.onStartRename?.("file-1", "report.pdf");
    void ops.onConfirmRename?.();
    ops.onCancelRename?.();
    ops.onDelete?.("file-1", "report.pdf");
    ops.onDownload?.("file-1", "report.pdf");
    ops.onShare?.("file-1", "report.pdf");
    ops.onCut?.("file-1");
    ops.onMediaClick?.("file-1");

    expect(onClick).toHaveBeenCalledWith("file-1", "report.pdf", 1024);
    expect(hook.handleRenameFile).toHaveBeenCalledWith("file-1", "report.pdf");
    expect(hook.handleConfirmRename).toHaveBeenCalled();
    expect(hook.handleCancelRename).toHaveBeenCalled();
    expect(hook.handleDeleteFile).toHaveBeenCalledWith("file-1", "report.pdf");
    expect(onDownload).toHaveBeenCalledWith("file-1", "report.pdf");
    expect(onShare).toHaveBeenCalledWith("file-1", "report.pdf");
    expect(onCut).toHaveBeenCalledWith("file-1");
    expect(onMediaClick).toHaveBeenCalledWith("file-1");
  });

  it("omits optional file actions when handlers are not supplied", () => {
    const ops = buildFileOperations(makeFileHook(), { onClick: vi.fn() });

    expect(ops.onDownload).toBeUndefined();
    expect(ops.onShare).toBeUndefined();
    expect(ops.onCut).toBeUndefined();
    expect(ops.onMediaClick).toBeUndefined();
  });
});
