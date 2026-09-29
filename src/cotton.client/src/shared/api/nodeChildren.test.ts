import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../test/fileFixtures";
import { loadNode } from "../store/nodesActions";
import { useNodesStore } from "../store/nodesStore";
import { httpClient } from "./httpClient";
import { fetchAllNodeChildren } from "./nodeChildren";

const folder = createFolder();
const files = Array.from({ length: 10_000 }, (_, index) =>
  createFile({ id: `file-${index}`, name: `${index}.txt` }),
);

const response = (
  pageFiles = files.slice(0, 1000),
  totalCount = files.length,
) => ({
  data: { ...folder, nodes: [], files: pageFiles },
  headers: { "x-total-count": String(totalCount) },
});

beforeEach(() => {
  useNodesStore.getState().reset();
});

afterEach(() => {
  vi.restoreAllMocks();
  useNodesStore.getState().reset();
});

describe("folder listing pagination", () => {
  it("opens a folder with 10,000 files through bounded requests without losing entries", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce({ data: folder })
      .mockResolvedValueOnce({ data: [] });
    for (let offset = 0; offset < files.length; offset += 1000) {
      get.mockResolvedValueOnce(response(files.slice(offset, offset + 1000)));
    }

    await loadNode(folder.id);

    const state = useNodesStore.getState();
    expect(state.error).toBeNull();
    expect(state.currentNode?.id).toBe(folder.id);
    expect(
      state.contentByNodeId[folder.id]?.files.map((file) => file.id),
    ).toEqual(files.map((file) => file.id));
    expect(get).toHaveBeenCalledTimes(12);
    for (let page = 1; page <= 10; page += 1) {
      expect(get).toHaveBeenNthCalledWith(
        page + 2,
        `/layouts/nodes/${folder.id}/children`,
        {
          params: {
            page,
            pageSize: 1000,
            nodeType: undefined,
            depth: undefined,
            includeStats: undefined,
          },
          paramsSerializer: { indexes: null },
        },
      );
    }
  });

  it("preserves folders and files when a page crosses their boundary", async () => {
    const nodes = Array.from({ length: 999 }, (_, index) =>
      createFolder({ id: `node-${index}` }),
    );
    const first = {
      ...response(files.slice(0, 1), 1001),
      data: { ...folder, nodes, files: files.slice(0, 1) },
    };
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce(first)
      .mockResolvedValueOnce(response(files.slice(1, 2), 1001));

    const result = await fetchAllNodeChildren(folder.id);

    expect(result.content.nodes).toEqual(nodes);
    expect(result.content.files.map((file) => file.id)).toEqual([
      "file-0",
      "file-1",
    ]);
    expect(first.data.files).toHaveLength(1);
    expect(result.totalCount).toBe(1001);
    expect(get).toHaveBeenCalledTimes(2);
  });

  it("keeps trash type and depth on every page", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce(response(files.slice(0, 1000), 1001))
      .mockResolvedValueOnce(response(files.slice(1000, 1001), 1001));

    const result = await fetchAllNodeChildren(folder.id, {
      nodeType: "trash",
      depth: 1,
    });

    expect(result.content.files).toHaveLength(1001);
    for (let page = 1; page <= 2; page += 1) {
      expect(get).toHaveBeenNthCalledWith(
        page,
        `/layouts/nodes/${folder.id}/children`,
        {
          params: {
            page,
            pageSize: 1000,
            nodeType: "trash",
            depth: 1,
            includeStats: undefined,
          },
          paramsSerializer: { indexes: null },
        },
      );
    }
  });

  it("stops immediately for an empty folder", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue(response([], 0));

    const result = await fetchAllNodeChildren(folder.id);

    expect(result.content.files).toEqual([]);
    expect(get).toHaveBeenCalledTimes(1);
  });

  it("stops if the next page becomes empty after deletions", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce(response(files.slice(0, 1000), 1001))
      .mockResolvedValueOnce(response([], 1000));

    const result = await fetchAllNodeChildren(folder.id);

    expect(result.content.files).toHaveLength(1000);
    expect(get).toHaveBeenCalledTimes(2);
  });

  it("propagates a later page failure instead of returning a truncated folder", async () => {
    vi.spyOn(httpClient, "get")
      .mockResolvedValueOnce(response(files.slice(0, 1000), 1001))
      .mockRejectedValueOnce(new Error("Page request failed"));

    await expect(fetchAllNodeChildren(folder.id)).rejects.toThrow(
      "Page request failed",
    );
  });
});
