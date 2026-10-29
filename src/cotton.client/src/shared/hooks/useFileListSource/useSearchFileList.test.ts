import { renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { NodeFileManifestDto } from "../../api/nodesApi";
import type { NodeDto } from "../../api/layoutsApi";
import { useSearchFileList } from "./useSearchFileList";
import { mergeSearchResults } from "../../../features/search/utils/normalizeSearch";

const makeFile = (id: string, name: string): NodeFileManifestDto => ({
  id,
  name,
  nodeId: "folder-1",
  ownerId: "owner-1",
  contentType: "text/plain",
  sizeBytes: 1,
  metadata: {},
  previewHashEncryptedHex: null,
  createdAt: "2026-05-27T00:00:00Z",
  updatedAt: "2026-05-27T00:00:01Z",
});

const buildResults = (files: NodeFileManifestDto[]) => ({
  orderedIds: files.map((file) => file.id),
  nodes: [],
  files,
  nodePaths: {},
  filePaths: Object.fromEntries(
    files.map((file) => [file.id, `/Root/Search/${file.name}`]),
  ),
});

describe("useSearchFileList", () => {
  it("orders the returned models when a hit was deleted before models were loaded", () => {
    const files = [makeFile("remaining", "report.txt")];
    const { result } = renderHook(() =>
      useSearchFileList({
        results: {
          ...buildResults(files),
          orderedIds: ["deleted", "remaining"],
        },
        loading: false,
        error: null,
        totalCount: 2,
        hasQuery: true,
      }),
    );
    expect(result.current.tiles).toMatchObject([
      { kind: "file", file: { id: "remaining" } },
    ]);
  });

  it("preserves server ordering across nested files, direct children and global folders", () => {
    const folders: NodeDto[] = [
      { id: "local-folder", parentId: "folder-1", name: "Local" },
      { id: "global-folder", parentId: "elsewhere", name: "Global" },
    ].map((node) => ({
      ...node,
      layoutId: "layout-1",
      metadata: {},
      createdAt: "",
      updatedAt: "",
    }));
    const files = [
      { ...makeFile("nested-z", "z.txt"), nodeId: "deep-folder" },
      makeFile("local-a", "a.txt"),
      { ...makeFile("global", "global.txt"), nodeId: "elsewhere" },
    ];
    const { result } = renderHook(() =>
      useSearchFileList({
        results: {
          ...buildResults(files),
          nodes: folders,
          orderedIds: [
            "nested-z",
            "local-folder",
            "local-a",
            "global-folder",
            "global",
          ],
        },
        loading: false,
        error: null,
        totalCount: 5,
        hasQuery: true,
      }),
    );
    expect(
      result.current.tiles.map((tile) =>
        tile.kind === "folder" ? tile.node.id : tile.file.id,
      ),
    ).toEqual([
      "nested-z",
      "local-folder",
      "local-a",
      "global-folder",
      "global",
    ]);
  });

  it("keeps previously loaded search results in place when another page is appended", () => {
    const firstPageFile = makeFile("file-b", "b.txt");
    const nextPageFile = makeFile("file-a", "a.txt");

    const { result, rerender } = renderHook(
      ({ append }: { append: boolean }) =>
        useSearchFileList({
          results: append
            ? mergeSearchResults(
                buildResults([firstPageFile]),
                buildResults([nextPageFile]),
              )
            : buildResults([firstPageFile]),
          loading: false,
          error: null,
          totalCount: 2,
          hasQuery: true,
          rootNodeName: "Root",
        }),
      { initialProps: { append: false } },
    );

    expect(result.current.tiles.map((tile) => tile.kind)).toEqual(["file"]);
    expect(
      result.current.tiles.map((tile) =>
        tile.kind === "file" ? tile.file.name : tile.node.name,
      ),
    ).toEqual(["b.txt"]);

    rerender({ append: true });

    expect(
      result.current.tiles.map((tile) =>
        tile.kind === "file" ? tile.file.name : tile.node.name,
      ),
    ).toEqual(["b.txt", "a.txt"]);
    expect(result.current.tiles.map((tile) => tile.path)).toEqual([
      "/Search/b.txt",
      "/Search/a.txt",
    ]);
  });
});
