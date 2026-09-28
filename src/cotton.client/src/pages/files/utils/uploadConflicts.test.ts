import { createFile, createFolder } from "../../../test/fileFixtures";
import { describe, expect, it, vi } from "vitest";
import type { SiblingNameLookupDto } from "../../../shared/api/nodesApi";
import { getFileNameKey } from "../../../shared/utils/fileNameUtils";
import { ConflictAction } from "../../../shared/types/nameConflict";
import { resolveUploadConflicts } from "./uploadConflicts";

const createContent = (
  fileNames: Pick<SiblingNameLookupDto["files"][number], "id" | "name">[],
  folderNames: string[] = [],
): SiblingNameLookupDto => ({
  nodes: folderNames.map((name, index) =>
    createFolder({ id: `folder-${index + 1}`, name }),
  ),
  files: fileNames.map(createFile),
  takenNameKeys: [
    ...fileNames.map((file) => getFileNameKey(file.name)),
    ...folderNames.map(getFileNameKey),
  ],
});

describe("resolveUploadConflicts", () => {
  it("normalizes names before resolving conflicts", async () => {
    const file = new File(["new"], " image_028.jpg", { type: "image/jpeg" });
    const confirmConflict = vi.fn(async () => ConflictAction.Overwrite);

    const result = await resolveUploadConflicts(
      [file],
      createContent([{ id: "file-1", name: "image_028.jpg" }]),
      confirmConflict,
    );

    expect(result.files).toHaveLength(1);
    expect(result.files[0]?.file.name).toBe("image_028.jpg");
    expect(result.files[0]?.replaceNodeFileId).toBe("file-1");
    expect(confirmConflict).toHaveBeenCalledWith({
      newName: "image_028 (1).jpg",
      canOverwrite: true,
    });
  });

  it("sends normalized names when no conflict exists", async () => {
    const file = new File(["new"], " report.txt. ", { type: "text/plain" });

    const result = await resolveUploadConflicts(
      [file],
      createContent([]),
      vi.fn(),
    );

    expect(result.files[0]?.file.name).toBe("report.txt");
  });

  it("matches the server name key for diacritics", async () => {
    const file = new File(["new"], "École.txt", { type: "text/plain" });
    const confirmConflict = vi.fn(async () => ConflictAction.Rename);

    const result = await resolveUploadConflicts(
      [file],
      createContent([], ["ecole.txt"]),
      confirmConflict,
    );

    expect(result.files[0]?.file.name).toBe("École (1).txt");
    expect(confirmConflict).toHaveBeenCalledWith({
      newName: "École (1).txt",
      canOverwrite: false,
    });
  });

  it("matches the server name key for contextual Unicode casing", async () => {
    const file = new File(["new"], "οσ", { type: "text/plain" });
    const confirmConflict = vi.fn(async () => ConflictAction.Overwrite);

    const result = await resolveUploadConflicts(
      [file],
      createContent([{ id: "file-1", name: "ΟΣ" }]),
      confirmConflict,
    );

    expect(result.files[0]?.replaceNodeFileId).toBe("file-1");
    expect(confirmConflict).toHaveBeenCalledWith({
      newName: "οσ (1)",
      canOverwrite: true,
    });
  });

  it("returns a replacement target when the user overwrites an existing file", async () => {
    const file = new File(["new"], "report.txt", { type: "text/plain" });
    const confirmConflict = vi.fn(async () => ConflictAction.Overwrite);

    const result = await resolveUploadConflicts(
      [file],
      createContent([{ id: "file-1", name: "report.txt" }]),
      confirmConflict,
    );

    expect(result.cancelled).toBe(false);
    expect(result.files).toEqual([{ file, replaceNodeFileId: "file-1" }]);
    expect(confirmConflict).toHaveBeenCalledWith({
      newName: "report (1).txt",
      canOverwrite: true,
    });
  });

  it("renames instead of overwriting folder conflicts", async () => {
    const file = new File(["new"], "report.txt", { type: "text/plain" });
    const confirmConflict = vi.fn(async () => ConflictAction.Rename);

    const result = await resolveUploadConflicts(
      [file],
      createContent([], ["report.txt"]),
      confirmConflict,
    );

    expect(result.cancelled).toBe(false);
    expect(result.files).toHaveLength(1);
    expect(result.files[0]?.replaceNodeFileId).toBeUndefined();
    expect(result.files[0]?.file.name).toBe("report (1).txt");
    expect(confirmConflict).toHaveBeenCalledWith({
      newName: "report (1).txt",
      canOverwrite: false,
    });
  });
});
