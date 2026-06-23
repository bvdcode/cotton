import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { queryKeys } from "./queryKeys";
import { clearTrashCaches, invalidateTrashChildren } from "./trash";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

const seedTrashChildren = (
  queryClient: QueryClient,
  nodeId: string,
  depth = 0,
) => {
  queryClient.setQueryData(
    queryKeys.trash.children.page(nodeId, { page: 1, pageSize: 100, depth }),
    {
      content: {
        id: nodeId,
        createdAt: "",
        updatedAt: "",
        nodes: [],
        files: [],
      },
      totalCount: 0,
    },
  );
};

describe("invalidateTrashChildren", () => {
  it("marks only the matching node's children queries as stale", async () => {
    const queryClient = createQueryClient();
    seedTrashChildren(queryClient, "node-a");
    seedTrashChildren(queryClient, "node-a", 1);
    seedTrashChildren(queryClient, "node-b");

    await invalidateTrashChildren(queryClient, "node-a");

    const aQueries = queryClient.getQueryCache().findAll({
      queryKey: queryKeys.trash.children.all("node-a"),
      exact: false,
    });
    const bQueries = queryClient.getQueryCache().findAll({
      queryKey: queryKeys.trash.children.all("node-b"),
      exact: false,
    });

    expect(aQueries).toHaveLength(2);
    expect(aQueries.every((query) => query.state.isInvalidated)).toBe(true);
    expect(bQueries).toHaveLength(1);
    expect(bQueries.every((query) => !query.state.isInvalidated)).toBe(true);
  });
});

describe("clearTrashCaches", () => {
  it("drops every cached entry under the trash namespace", () => {
    const queryClient = createQueryClient();

    queryClient.setQueryData(queryKeys.trash.root(), {
      id: "root",
      createdAt: "",
      updatedAt: "",
      layoutId: "layout-1",
      parentId: null,
      name: "Trash",
      metadata: {},
    });
    queryClient.setQueryData(queryKeys.trash.meta("node-a"), {
      node: null,
      ancestors: [],
    });
    seedTrashChildren(queryClient, "node-a");

    clearTrashCaches(queryClient);

    expect(
      queryClient.getQueryCache().findAll({
        queryKey: queryKeys.trash.all(),
        exact: false,
      }),
    ).toEqual([]);
  });
});
