import { describe, expect, it, vi } from "vitest";
import type { FileSystemTile } from "../../shared/types/FileListViewTypes";
import {
  buildFolderEncryptionPrompt,
  buildSelectionArchiveRequest,
  buildUniqueSiblingName,
  isHugeFolderCount,
  resolveFilesNodeId,
  shouldRenderFilesList,
} from "./filesPageModel";

describe("filesPageModel", () => {
  it("builds a case-insensitive unique sibling name before the extension", () => {
    expect(buildUniqueSiblingName("notes.md", ["Notes.md", "notes 1.md"]))
      .toBe("notes 2.md");
    expect(buildUniqueSiblingName("README", ["other"])).toBe("README");
  });

  it("uses the route node before the cached root node", () => {
    expect(resolveFilesNodeId("route", "root")).toBe("route");
    expect(resolveFilesNodeId(undefined, "root")).toBe("root");
    expect(resolveFilesNodeId(undefined, null)).toBeNull();
  });

  it("marks only folders above the huge-folder threshold", () => {
    expect(isHugeFolderCount(100_000)).toBe(false);
    expect(isHugeFolderCount(100_001)).toBe(true);
    expect(isHugeFolderCount(null)).toBe(false);
  });

  it("keeps stale content visible when refresh fails", () => {
    expect(shouldRenderFilesList("failed", { folders: [] })).toBe(true);
    expect(shouldRenderFilesList("failed", undefined)).toBe(false);
    expect(shouldRenderFilesList(null, undefined)).toBe(true);
  });

  it("builds an archive request from the selected files and folders", () => {
    const tiles = [
      {
        kind: "folder",
        node: { id: "folder-id", name: "Folder" },
      },
      {
        kind: "file",
        file: { id: "file-id", name: "File.txt" },
      },
    ] as FileSystemTile[];

    expect(
      buildSelectionArchiveRequest(
        tiles,
        new Set(["folder-id", "file-id"]),
        "Current",
      ),
    ).toEqual({
      fileIds: ["file-id"],
      nodeIds: ["folder-id"],
      archiveName: "Current",
    });
    expect(buildSelectionArchiveRequest(tiles, new Set(["file-id"]))).toEqual({
      fileIds: ["file-id"],
      nodeIds: [],
      archiveName: "File.txt",
    });
  });

  it("returns the matching folder encryption action", () => {
    const encrypt = vi.fn();
    const decrypt = vi.fn();
    const prompt = buildFolderEncryptionPrompt({
      decryptEncryptedFiles: decrypt,
      encryptedFilesCount: 0,
      encryptedFilesMessage: "decrypt message",
      encryptedFilesAction: "decrypt",
      encryptPlainFiles: encrypt,
      folderPolicyEnabled: true,
      isDecryptingEncryptedFiles: false,
      isEncryptingPlainFiles: false,
      plainFilesCount: 2,
      plainFilesMessage: "encrypt message",
      plainFilesAction: "encrypt",
    });

    expect(prompt).toMatchObject({
      severity: "warning",
      message: "encrypt message",
      action: "encrypt",
    });
    prompt?.onAction();
    expect(encrypt).toHaveBeenCalledOnce();
    expect(decrypt).not.toHaveBeenCalled();
  });
});
