import { describe, expect, it, vi } from "vitest";
import type { NodeContentDto } from "../../../shared/api/nodesApi";
import {
  ConflictAction,
  resolveUploadConflicts,
} from "./uploadConflicts";

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
  it("returns a replacement target when the user overwrites an existing file", async () => {
    const file = new File(["new"], "report.txt", { type: "text/plain" });
    const confirmConflict = vi.fn(async () => ConflictAction.Overwrite);

    const result = await resolveUploadConflicts(
      [file],
      createContent([{ id: "file-1", name: "report.txt" }]),
      confirmConflict,
    );

    expect(result.cancelled).toBe(false);
    expect(result.files).toEqual([
      { file, replaceNodeFileId: "file-1" },
    ]);
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
