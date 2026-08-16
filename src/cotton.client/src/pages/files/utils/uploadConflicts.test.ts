import { describe, expect, it, vi } from "vitest";
import type { NodeContentDto } from "../../../shared/api/nodesApi";
import { ConflictAction, resolveUploadConflicts } from "./uploadConflicts";

const createContent = (
  fileNames: Array<{ id: string; name: string }>,
  folderNames: string[] = [],
): NodeContentDto =>
  ({
    id: "node-1",
    nodes: folderNames.map((name, index) => ({
      id: `folder-${index + 1}`,
      name,
    })),
    files: fileNames.map((file) => ({
      id: file.id,
      name: file.name,
    })),
  }) as NodeContentDto;

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
