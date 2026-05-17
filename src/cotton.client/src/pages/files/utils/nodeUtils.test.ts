import { describe, expect, it } from "vitest";
import { buildBreadcrumbs, calculateFolderStats } from "./nodeUtils";

const makeNode = (id: string, name: string) => ({
  id,
  createdAt: "2026-05-13T00:00:00Z",
  layoutId: "layout-1",
  metadata: {},
  name,
  parentId: null,
  updatedAt: "2026-05-13T00:00:00Z",
});

describe("buildBreadcrumbs", () => {
  it("returns an empty list when there is no current node", () => {
    expect(buildBreadcrumbs([], null)).toEqual([]);
  });

  it("returns a single crumb for the current node", () => {
    expect(buildBreadcrumbs([], makeNode("root", "Root"))).toEqual([
      { id: "root", name: "Root" },
    ]);
  });

  it("chains ancestors before the current node", () => {
    expect(
      buildBreadcrumbs(
        [makeNode("a", "A"), makeNode("b", "B")],
        makeNode("c", "C"),
      ),
    ).toEqual([
      { id: "a", name: "A" },
      { id: "b", name: "B" },
      { id: "c", name: "C" },
    ]);
  });
});

describe("calculateFolderStats", () => {
  it("returns zero stats for undefined inputs", () => {
    expect(calculateFolderStats(undefined, undefined)).toEqual({
      files: 0,
      folders: 0,
      sizeBytes: 0,
    });
  });

  it("counts folders, files, and file sizes", () => {
    expect(
      calculateFolderStats(
        [{ id: "n1" }, { id: "n2" }],
        [{ sizeBytes: 100 }, { sizeBytes: 250 }],
      ),
    ).toEqual({ files: 2, folders: 2, sizeBytes: 350 });
  });

  it("treats missing sizeBytes as zero", () => {
    expect(
      calculateFolderStats(undefined, [
        { sizeBytes: 1024 },
        {},
        { sizeBytes: 2048 },
      ]),
    ).toEqual({ files: 3, folders: 0, sizeBytes: 3072 });
  });
});
