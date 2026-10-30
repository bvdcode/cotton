import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { nodesApi, type NodeResponse } from "../nodesApi";
import { queryKeys } from "./queryKeys";
import { invalidateTrashChildren, useTrashChildrenQuery } from "./trash";

const page = (count: number): NodeResponse => ({
  content: {
    ...createFolder(),
    nodes: [],
    files: Array.from({ length: count }, (_, index) =>
      createFile({ id: `file-${index}` }),
    ),
  },
  totalCount: 1001,
});

const createProvider = () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const wrapper = ({ children }: PropsWithChildren) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return { client, wrapper };
};

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("trash pagination", () => {
  it("loads all pages for tiles and invalidates their cache with the folder", async () => {
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValueOnce(page(1000))
      .mockResolvedValueOnce({
        ...page(1),
        content: { ...page(1).content, files: [createFile({ id: "last" })] },
      });
    const { client, wrapper } = createProvider();
    const { result, unmount } = renderHook(
      () => useTrashChildrenQuery({ nodeId: "trash", isRoot: true }),
      { wrapper },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.content.files).toHaveLength(1001);
    expect(result.current.data?.content.files.at(-1)?.id).toBe("last");
    expect(get).toHaveBeenNthCalledWith(1, "trash", {
      nodeType: "trash",
      depth: 1,
      page: 1,
    });
    expect(get).toHaveBeenNthCalledWith(2, "trash", {
      nodeType: "trash",
      depth: 1,
      page: 2,
    });
    unmount();

    await invalidateTrashChildren(client, "trash");
    expect(
      client.getQueryState(queryKeys.trash.children.complete("trash", 1))
        ?.isInvalidated,
    ).toBe(true);
    client.clear();
  });

  it("requests only the selected page for the table", async () => {
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue(page(100));
    const { client, wrapper } = createProvider();
    const { result, unmount } = renderHook(
      () =>
        useTrashChildrenQuery({
          nodeId: "nested",
          isRoot: false,
          page: 3,
          pageSize: 100,
        }),
      { wrapper },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith("nested", {
      nodeType: "trash",
      depth: 0,
      page: 3,
      pageSize: 100,
    });
    expect(result.current.data?.totalCount).toBe(1001);
    unmount();
    client.clear();
  });
});
