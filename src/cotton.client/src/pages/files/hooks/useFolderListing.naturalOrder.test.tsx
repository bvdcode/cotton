import { QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { InterfaceLayoutType } from "@shared/api/layoutsApi";
import { nodesApi, type NodeResponse } from "@shared/api/nodesApi";
import { queryClient } from "@shared/api/queries/queryClient";
import { FOLDER_PAGE_SIZE } from "@shared/api/queries/folderPageSize";
import { useFolderListing } from "./useFolderListing";

const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);
const layouts = [InterfaceLayoutType.Tiles, InterfaceLayoutType.List];

function response(names: string[], totalCount = names.length): NodeResponse {
  return {
    totalCount,
    content: {
      ...createFolder(),
      node: createFolder(),
      nodes: [],
      files: names.map((name) => createFile({ id: name, name })),
      stats: { files: totalCount, folders: 0, sizeBytes: totalCount },
    },
  };
}

afterEach(() => {
  cleanup();
  queryClient.clear();
  vi.restoreAllMocks();
});

it.each(layouts)(
  "naturally orders a complete folder in layout %s without changing cached data",
  async (layout) => {
    const source = response(["1.txt", "10.txt", "2.txt"]);
    source.content.nodes = ["1", "10", "2"].map((name) =>
      createFolder({ id: `folder-${name}`, name }),
    );
    source.totalCount += source.content.nodes.length;
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue(source);
    const { result } = renderHook(
      () => useFolderListing("folder-1", "user-1", layout),
      { wrapper },
    );
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.content?.files.map((file) => file.name)).toEqual([
      "1.txt",
      "2.txt",
      "10.txt",
    ]);
    expect(result.current.content?.nodes.map((node) => node.name)).toEqual([
      "1",
      "2",
      "10",
    ]);
    expect(source.content.files.map((file) => file.name)).toEqual([
      "1.txt",
      "10.txt",
      "2.txt",
    ]);
    expect(get).toHaveBeenCalledTimes(1);
  },
);

it.each(layouts)(
  "only orders a single complete batch at the 25000-item boundary in layout %s",
  async (layout) => {
    const names = [
      "1",
      "10",
      "2",
      ...Array.from(
        { length: FOLDER_PAGE_SIZE - 3 },
        (_, index) => `item-${index}`,
      ),
    ];
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(response(names));
    const { result } = renderHook(
      () => useFolderListing("folder-1", "user-1", layout),
      { wrapper },
    );
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(
      result.current.content?.files.slice(0, 3).map((file) => file.name),
    ).toEqual(["1", "2", "10"]);
    get.mockResolvedValue(response(names, FOLDER_PAGE_SIZE + 1));
    await act(async () =>
      queryClient.invalidateQueries({ queryKey: ["nodeChildren"] }),
    );
    await waitFor(() =>
      expect(result.current.totalCount).toBe(FOLDER_PAGE_SIZE + 1),
    );
    expect(
      result.current.content?.files.slice(0, 3).map((file) => file.name),
    ).toEqual(["1", "10", "2"]);
  },
);

it("orders the complete table batch before slicing its 100-row pages", async () => {
  const names = Array.from(
    { length: 205 },
    (_, index) => `${index + 1}.txt`,
  ).sort();
  const get = vi
    .spyOn(nodesApi, "getChildren")
    .mockResolvedValue(response(names));
  const { result } = renderHook(
    () => useFolderListing("folder-1", "user-1", InterfaceLayoutType.List),
    { wrapper },
  );
  await waitFor(() => expect(result.current.loading).toBe(false));
  expect(result.current.content?.files[99].name).toBe("100.txt");
  act(() =>
    result.current.pagination?.onPaginationModelChange({
      page: 1,
      pageSize: 100,
    }),
  );
  expect(result.current.content?.files[0].name).toBe("101.txt");
  expect(result.current.content?.files[99].name).toBe("200.txt");
  expect(get).toHaveBeenCalledTimes(1);
});

it("respects explicit descending name order and leaves size order intact", async () => {
  const get = vi
    .spyOn(nodesApi, "getChildren")
    .mockResolvedValue(response(["2", "11", "10", "1"]));
  const { result } = renderHook(
    () => useFolderListing("folder-1", "user-1", InterfaceLayoutType.List),
    { wrapper },
  );
  await waitFor(() =>
    expect(result.current.pagination?.query?.namesEnabled).toBe(true),
  );
  act(() =>
    result.current.pagination?.query?.onSortModelChange([
      { field: "name", sort: "desc" },
    ]),
  );
  await waitFor(() => expect(get).toHaveBeenCalledTimes(2));
  await waitFor(() =>
    expect(result.current.content?.files.map((file) => file.name)).toEqual([
      "11",
      "10",
      "2",
      "1",
    ]),
  );
  act(() =>
    result.current.pagination?.query?.onSortModelChange([
      { field: "sizeBytes", sort: "desc" },
    ]),
  );
  await waitFor(() => expect(result.current.pagination?.mode).toBe("client"));
  await waitFor(() =>
    expect(result.current.content?.files.map((file) => file.name)).toEqual([
      "2",
      "11",
      "10",
      "1",
    ]),
  );
});

it("keeps server order after all pages of a larger folder have arrived", async () => {
  const first = [
    "1",
    "10",
    "2",
    ...Array.from(
      { length: FOLDER_PAGE_SIZE - 3 },
      (_, index) => `item-${index}`,
    ),
  ];
  vi.spyOn(nodesApi, "getChildren").mockImplementation(async (_, options) =>
    response(options?.page === 1 ? first : ["3"], FOLDER_PAGE_SIZE + 1),
  );
  const { result } = renderHook(
    () => useFolderListing("folder-1", "user-1", InterfaceLayoutType.Tiles),
    { wrapper },
  );
  await waitFor(() =>
    expect(result.current.content?.files).toHaveLength(FOLDER_PAGE_SIZE),
  );
  act(() => result.current.loadMore?.());
  await waitFor(() =>
    expect(result.current.content?.files).toHaveLength(FOLDER_PAGE_SIZE + 1),
  );
  expect(
    result.current.content?.files.slice(0, 3).map((file) => file.name),
  ).toEqual(["1", "10", "2"]);
  expect(result.current.content?.files.at(-1)?.name).toBe("3");
});
