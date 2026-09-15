import { renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { InterfaceLayoutType, type NodeDto } from "@shared/api/layoutsApi";
import type { NodeContentDto } from "@shared/api/nodesApi";

interface QueryState<T> {
  data?: T;
  isPending: boolean;
  isError: boolean;
}

interface NodeMeta {
  node: NodeDto;
  ancestors: NodeDto[];
}

interface ChildrenData {
  content: NodeContentDto;
  totalCount: number;
}

const queries = vi.hoisted(() => ({
  root: vi.fn<() => QueryState<NodeDto>>(),
  meta: vi.fn<() => QueryState<NodeMeta>>(),
  children: vi.fn<() => QueryState<ChildrenData>>(),
}));

vi.mock("@shared/api/queries/trash", () => ({
  useTrashRootQuery: queries.root,
  useTrashNodeMetaQuery: queries.meta,
  useTrashChildrenQuery: queries.children,
}));

import { useTrashPageData } from "./useTrashPageData";

const root: NodeDto = {
  id: "trash-root",
  name: "Trash",
  layoutId: "layout",
  parentId: null,
  metadata: {},
  createdAt: "2026-09-15T00:00:00Z",
  updatedAt: "2026-09-15T00:00:00Z",
};
const folder: NodeDto = {
  ...root,
  id: "folder",
  name: "Reports",
  parentId: root.id,
};
const content: NodeContentDto = {
  id: root.id,
  createdAt: root.createdAt,
  updatedAt: root.updatedAt,
  nodes: [],
  files: [],
};
const options = {
  layoutType: InterfaceLayoutType.Tiles,
  loadErrorText: "Load failed",
};

describe("useTrashPageData", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    queries.root.mockReturnValue({
      data: root,
      isPending: false,
      isError: false,
    });
    queries.meta.mockReturnValue({ isPending: true, isError: false });
    queries.children.mockReturnValue({ isPending: true, isError: false });
  });

  it("shows the resolved root while its metadata and children are loading", () => {
    const { result } = renderHook(() => useTrashPageData(options));

    expect(result.current).toMatchObject({
      nodeId: root.id,
      currentNode: root,
      ancestors: [],
      loading: true,
      error: null,
    });
    expect(queries.root).toHaveBeenCalledWith(true);
    expect(queries.children).toHaveBeenCalledWith({
      nodeId: root.id,
      isRoot: true,
      enabled: true,
    });
  });

  it("waits for a root id before loading its children", () => {
    queries.root.mockReturnValue({ isPending: true, isError: false });
    const { result } = renderHook(() => useTrashPageData(options));

    expect(result.current.nodeId).toBeNull();
    expect(result.current.currentNode).toBeNull();
    expect(result.current.loading).toBe(true);
    expect(queries.children).toHaveBeenCalledWith({
      nodeId: null,
      isRoot: true,
      enabled: false,
    });
  });

  it("does not display the cached root as a nested folder", () => {
    const { result } = renderHook(() =>
      useTrashPageData({
        ...options,
        routeNodeId: folder.id,
      }),
    );

    expect(result.current.nodeId).toBe(folder.id);
    expect(result.current.currentNode).toBeNull();
    expect(queries.root).toHaveBeenCalledWith(false);
    expect(queries.meta).toHaveBeenCalledWith(folder.id, {
      isRoot: false,
      enabled: true,
    });
  });

  it("returns the nested folder, ancestry, and loaded contents", () => {
    queries.meta.mockReturnValue({
      data: { node: folder, ancestors: [root] },
      isPending: false,
      isError: false,
    });
    queries.children.mockReturnValue({
      data: { content, totalCount: 0 },
      isPending: false,
      isError: false,
    });
    const { result } = renderHook(() =>
      useTrashPageData({
        ...options,
        routeNodeId: folder.id,
      }),
    );

    expect(result.current).toEqual({
      nodeId: folder.id,
      currentNode: folder,
      ancestors: [root],
      content,
      loading: false,
      error: null,
    });
  });

  it("does not wait for the disabled tile query in list mode", () => {
    queries.meta.mockReturnValue({
      data: { node: root, ancestors: [] },
      isPending: false,
      isError: false,
    });
    const { result } = renderHook(() =>
      useTrashPageData({
        ...options,
        layoutType: InterfaceLayoutType.List,
      }),
    );

    expect(result.current.loading).toBe(false);
    expect(queries.children).toHaveBeenCalledWith({
      nodeId: root.id,
      isRoot: true,
      enabled: false,
    });
  });

  it.each(["root", "meta", "children"] as const)(
    "reports a failed %s query",
    (query) => {
      queries[query].mockReturnValue({ isPending: false, isError: true });
      const { result } = renderHook(() => useTrashPageData(options));
      expect(result.current.error).toBe("Load failed");
    },
  );
});
