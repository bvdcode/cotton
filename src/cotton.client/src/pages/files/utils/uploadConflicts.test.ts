import { createFile, createFolder } from "../../../test/fileFixtures";
import { describe, expect, it, vi } from "vitest";
import type { SiblingNameLookupDto } from "../../../shared/api/nodesApi";
import { getFileNameKey } from "../../../shared/utils/fileNameUtils";
import {
  ConflictAction,
  type NameConflictPrompt,
} from "../../../shared/types/nameConflict";
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
  it("offers actions for all only while more files remain", async () => {
    const files = [new File(["a"], "a.jpg"), new File(["b"], "b.jpg")];
    const confirm = vi.fn<
      (prompt: NameConflictPrompt) => Promise<ConflictAction>
    >(async () => ConflictAction.Skip);
    await resolveUploadConflicts(
      files,
      createContent([
        { id: "a", name: "a.jpg" },
        { id: "b", name: "b.jpg" },
      ]),
      confirm,
    );
    expect(confirm.mock.calls.map(([prompt]) => prompt.canApplyToAll)).toEqual([
      true,
      false,
    ]);
    expect(confirm.mock.calls.every(([prompt]) => !prompt.canMerge)).toBe(true);
  });

  it("keeps actions for all when another dropped-folder group remains", async () => {
    const confirm = vi.fn(async () => ConflictAction.Skip);
    await resolveUploadConflicts(
      [new File(["a"], "a.jpg")],
      createContent([{ id: "a", name: "a.jpg" }]),
      confirm,
      1,
    );
    expect(confirm).toHaveBeenCalledWith(
      expect.objectContaining({ canApplyToAll: true, canMerge: false }),
    );
  });
  it("replaces every existing file after one replace-all decision", async () => {
    const files = [new File(["a"], "a.jpg"), new File(["b"], "b.jpg")];
    const confirm = vi.fn(async () => ConflictAction.OverwriteAll);

    const result = await resolveUploadConflicts(
      files,
      createContent([
        { id: "a", name: "a.jpg" },
        { id: "b", name: "b.jpg" },
      ]),
      confirm,
    );

    expect(result.files).toEqual([
      { file: files[0], replaceNodeFileId: "a" },
      { file: files[1], replaceNodeFileId: "b" },
    ]);
    expect(confirm).toHaveBeenCalledOnce();
  });

  it("still prompts when a replacement target was already used in this upload", async () => {
    const first = new File(["a"], "a.jpg");
    const second = new File(["b"], "a.jpg");
    const confirm = vi
      .fn()
      .mockResolvedValueOnce(ConflictAction.OverwriteAll)
      .mockResolvedValueOnce(ConflictAction.Rename);

    const result = await resolveUploadConflicts(
      [first, second],
      createContent([{ id: "a", name: "a.jpg" }]),
      confirm,
    );

    expect(result.files[0]).toEqual({ file: first, replaceNodeFileId: "a" });
    expect(result.files[1]?.file.name).toBe("a (1).jpg");
    expect(result.files[1]?.replaceNodeFileId).toBeUndefined();
    expect(confirm).toHaveBeenLastCalledWith(
      expect.objectContaining({
        newName: "a (1).jpg",
        canOverwrite: false,
      }),
    );
  });

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
    expect(confirmConflict).toHaveBeenCalledWith(
      expect.objectContaining({
        newName: "image_028 (1).jpg",
        canOverwrite: true,
      }),
    );
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
    expect(confirmConflict).toHaveBeenCalledWith(
      expect.objectContaining({
        newName: "École (1).txt",
        canOverwrite: false,
      }),
    );
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
    expect(confirmConflict).toHaveBeenCalledWith(
      expect.objectContaining({
        newName: "οσ (1)",
        canOverwrite: true,
      }),
    );
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
    expect(confirmConflict).toHaveBeenCalledWith(
      expect.objectContaining({
        newName: "report (1).txt",
        canOverwrite: true,
      }),
    );
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
    expect(confirmConflict).toHaveBeenCalledWith(
      expect.objectContaining({
        newName: "report (1).txt",
        canOverwrite: false,
      }),
    );
  });
});
