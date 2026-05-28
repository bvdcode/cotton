import { renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { NodeFileManifestDto } from "../../api/nodesApi";
import { useSearchFileList } from "./useSearchFileList";

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
  nodes: [],
  files,
  nodePaths: {},
  filePaths: Object.fromEntries(
    files.map((file) => [file.id, `/Root/Search/${file.name}`]),
  ),
});

describe("useSearchFileList", () => {
  it("keeps previously loaded search results in place when another page is appended", () => {
    const firstPageFile = makeFile("file-b", "b.txt");
    const nextPageFile = makeFile("file-a", "a.txt");

    const { result, rerender } = renderHook(
      ({ files }: { files: NodeFileManifestDto[] }) =>
        useSearchFileList({
          results: buildResults(files),
          loading: false,
          error: null,
          totalCount: files.length,
          hasQuery: true,
          rootNodeName: "Root",
        }),
      { initialProps: { files: [firstPageFile] } },
    );

    expect(result.current.tiles.map((tile) => tile.kind)).toEqual(["file"]);
    expect(
      result.current.tiles.map((tile) =>
        tile.kind === "file" ? tile.file.name : tile.node.name,
      ),
    ).toEqual(["b.txt"]);

    rerender({ files: [firstPageFile, nextPageFile] });

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
