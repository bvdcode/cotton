import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { nodesApi } from "@shared/api/nodesApi";
import { InterfaceLayoutType } from "@shared/api/layoutsApi";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { useTrashListData } from "./useTrashListData";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

it("provides the entire trash listing for sorting before client pagination", async () => {
  const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
    content: {
      ...createFolder(),
      nodes: [],
      files: Array.from({ length: 201 }, (_, i) =>
        createFile({ id: `file-${i}`, sizeBytes: 201 - i }),
      ),
    },
    totalCount: 201,
  });
  const client = new QueryClient();
  const { result, unmount } = renderHook(
    () =>
      useTrashListData({
        nodeId: "trash",
        layoutType: InterfaceLayoutType.List,
        loadErrorText: "Load failed",
      }),
    {
      wrapper: ({ children }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    },
  );
  await waitFor(() =>
    expect(result.current.listContent?.files).toHaveLength(201),
  );
  act(() => result.current.handlePaginationChange(1, 100));
  expect(result.current.paginationModel).toEqual({ page: 1, pageSize: 100 });
  expect(result.current.listContent?.files).toHaveLength(201);
  expect(get).toHaveBeenCalledTimes(1);
  expect(get).toHaveBeenCalledWith("trash", {
    nodeType: "trash",
    depth: 1,
    page: 1,
    pageSize: 25_000,
    signal: expect.any(AbortSignal),
  });
  unmount();
  client.clear();
});
