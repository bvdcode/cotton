import { QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { nodesApi, type NodeResponse } from "../../../shared/api/nodesApi";
import { queryClient } from "../../../shared/api/queries/queryClient";
import { useFolderListing } from "./useFolderListing";

const files = Array.from({ length: 101 }, (_, index) =>
  createFile({
    id: `file-${index}`,
    name: `file-${index}.txt`,
    metadata: index === 0 ? { isClientEncrypted: "true" } : {},
  }),
);

const response = (): NodeResponse => ({
  content: {
    node: createFolder({ id: "folder-1" }),
    ...createFolder({ id: "folder-1" }),
    nodes: [],
    files,
    stats: { folders: 0, files: 101, sizeBytes: 101 },
  },
  totalCount: 101,
});

const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);

afterEach(() => {
  cleanup();
  queryClient.clear();
  vi.restoreAllMocks();
});

describe("folder listing", () => {
  it("disables name operations when encrypted folder names remain without an encryption policy", async () => {
    vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
      content: {
        node: createFolder(),
        ...createFolder(),
        nodes: [createFolder({ metadata: { en: "opaque" } })],
        files: [],
        stats: {
          folders: 1,
          files: 0,

          sizeBytes: 0,
        },
      },
      totalCount: 1,
    });
    const { result } = renderHook(
      () =>
        useFolderListing("folder-1", "user-1", InterfaceLayoutType.List, false),
      { wrapper },
    );
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.pagination?.query?.namesEnabled).toBe(false);
  });
  it("shows a single list page while keeping totals for the whole folder", async () => {
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue(response());
    const { result } = renderHook(
      () => useFolderListing("folder-1", "user-1", InterfaceLayoutType.List),
      { wrapper },
    );

    await waitFor(() =>
      expect(result.current.content?.files).toHaveLength(100),
    );
    expect(result.current.stats).toEqual({
      folders: 0,
      files: 101,

      sizeBytes: 101,
    });
    expect(result.current.pagination?.totalCount).toBe(101);

    act(() =>
      result.current.pagination?.onPaginationModelChange({
        page: 1,
        pageSize: 100,
      }),
    );
    await waitFor(() =>
      expect(result.current.content?.files[0]?.id).toBe("file-100"),
    );
    expect(result.current.content?.files).toHaveLength(1);
    expect(result.current.stats.sizeBytes).toBe(101);
    expect(get).toHaveBeenCalledExactlyOnceWith("folder-1", {
      page: 1,
      pageSize: 25000,
      includeStats: true,
    });
  });

  it("loads another tile page only when requested", async () => {
    const tileFiles = Array.from({ length: 25001 }, (_, index) =>
      createFile({ id: `tile-${index}` }),
    );
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (_, options) => ({
        content: {
          node: createFolder(),
          ...createFolder(),
          nodes: [],
          files:
            options?.page === 1
              ? tileFiles.slice(0, 25000)
              : tileFiles.slice(25000),
          stats: {
            folders: 0,
            files: 25001,

            sizeBytes: 25001,
          },
        },
        totalCount: 25001,
      }));
    const { result } = renderHook(
      () => useFolderListing("folder-1", "user-1", InterfaceLayoutType.Tiles),
      { wrapper },
    );

    await waitFor(() =>
      expect(result.current.content?.files).toHaveLength(25000),
    );
    expect(get).toHaveBeenCalledTimes(1);
    act(() => result.current.loadMore?.());
    await waitFor(() =>
      expect(result.current.content?.files).toHaveLength(25001),
    );
    expect(result.current.loadMore).toBeUndefined();
    expect(get).toHaveBeenCalledTimes(2);
    expect(result.current.stats.sizeBytes).toBe(25001);
    expect(get).toHaveBeenNthCalledWith(1, "folder-1", {
      page: 1,
      pageSize: 25000,
      includeStats: true,
    });
    expect(get).toHaveBeenNthCalledWith(2, "folder-1", {
      page: 2,
      pageSize: 25000,
    });
  });

  it("shares a single batch between tiles and table pages without extra name scans", async () => {
    const folders = Array.from({ length: 638 }, (_, index) =>
      createFolder({ id: `child-${index}` }),
    );
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (_, options) => ({
        content: {
          node: createFolder(),
          ...createFolder(),
          nodes: folders.slice(
            ((options?.page ?? 1) - 1) * (options?.pageSize ?? 100),
            (options?.page ?? 1) * (options?.pageSize ?? 100),
          ),
          files: [],
          stats: { folders: 638, files: 0, sizeBytes: 0 },
        },
        totalCount: 638,
      }));
    const { result, rerender } = renderHook<
      ReturnType<typeof useFolderListing>,
      InterfaceLayoutType
    >((layout) => useFolderListing("folder-1", "user-1", layout), {
      wrapper,
      initialProps: InterfaceLayoutType.Tiles,
    });
    await waitFor(() =>
      expect(result.current.content?.nodes).toHaveLength(638),
    );
    expect(result.current.loadMore).toBeUndefined();
    expect(get).toHaveBeenCalledExactlyOnceWith("folder-1", {
      page: 1,
      pageSize: 25000,
      includeStats: true,
    });

    rerender(InterfaceLayoutType.List);
    await waitFor(() =>
      expect(result.current.content?.nodes).toHaveLength(100),
    );
    expect(result.current.pagination?.model?.pageSize).toBe(100);
    await waitFor(() =>
      expect(result.current.pagination?.query?.namesEnabled).toBe(true),
    );
    expect(get).toHaveBeenCalledTimes(1);
    rerender(InterfaceLayoutType.Tiles);
    await waitFor(() =>
      expect(result.current.content?.nodes).toHaveLength(638),
    );
    expect(get).toHaveBeenCalledTimes(1);
    expect(result.current.loadMore).toBeUndefined();
  });
  it("fetches the next batch only after the first 250 table pages", async () => {
    const batchFiles = Array.from({ length: 25001 }, (_, index) =>
      createFile({ id: `item-${index}` }),
    );
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (_, options) => ({
        content: {
          node: createFolder(),
          ...createFolder(),
          nodes: [],
          files:
            options?.page === 1
              ? batchFiles.slice(0, 25000)
              : batchFiles.slice(25000),
          stats: { folders: 0, files: 25001, sizeBytes: 25001 },
        },
        totalCount: 25001,
      }));
    const { result } = renderHook(
      () =>
        useFolderListing("folder-1", "user-1", InterfaceLayoutType.List, true),
      { wrapper },
    );
    await waitFor(() =>
      expect(result.current.content?.files).toHaveLength(100),
    );
    act(() =>
      result.current.pagination?.onPaginationModelChange({
        page: 249,
        pageSize: 100,
      }),
    );
    expect(result.current.content?.files[0].id).toBe("item-24900");
    expect(get).toHaveBeenCalledTimes(1);
    act(() =>
      result.current.pagination?.onPaginationModelChange({
        page: 250,
        pageSize: 100,
      }),
    );
    await waitFor(() =>
      expect(result.current.content?.files[0]?.id).toBe("item-25000"),
    );
    expect(result.current.content?.files).toHaveLength(1);
    expect(get).toHaveBeenNthCalledWith(2, "folder-1", {
      page: 2,
      pageSize: 25000,
      signal: expect.any(AbortSignal),
    });
    act(() =>
      result.current.pagination?.onPaginationModelChange({
        page: 0,
        pageSize: 100,
      }),
    );
    expect(result.current.content?.files[0].id).toBe("item-0");
    act(() =>
      result.current.pagination?.onPaginationModelChange({
        page: 250,
        pageSize: 100,
      }),
    );
    expect(result.current.content?.files[0].id).toBe("item-25000");
    expect(get).toHaveBeenCalledTimes(2);
  });

  it("slices the folder/file boundary without losing or duplicating rows", async () => {
    const folders = Array.from({ length: 155 }, (_, index) =>
      createFolder({ id: `child-${index}` }),
    );
    const pageFiles = Array.from({ length: 51 }, (_, index) =>
      createFile({ id: `item-${index}` }),
    );
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
      content: {
        node: createFolder(),
        ...createFolder(),
        nodes: folders,
        files: pageFiles,
        stats: { folders: 155, files: 51, sizeBytes: 51 },
      },
      totalCount: 206,
    });
    const { result } = renderHook(
      () => useFolderListing("folder-1", "user-1", InterfaceLayoutType.List),
      { wrapper },
    );
    await waitFor(() =>
      expect(result.current.content?.nodes).toHaveLength(100),
    );
    expect(result.current.content?.files).toHaveLength(0);
    act(() =>
      result.current.pagination?.onPaginationModelChange({
        page: 1,
        pageSize: 100,
      }),
    );
    expect(result.current.content?.nodes).toHaveLength(55);
    expect(result.current.content?.nodes[0].id).toBe("child-100");
    expect(result.current.content?.files).toHaveLength(45);
    act(() =>
      result.current.pagination?.onPaginationModelChange({
        page: 2,
        pageSize: 100,
      }),
    );
    expect(result.current.content?.nodes).toHaveLength(0);
    expect(result.current.content?.files.map((file) => file.id)).toEqual(
      pageFiles.slice(45).map((file) => file.id),
    );
    expect(get).toHaveBeenCalledTimes(1);
  });

  it("requests sorting for the whole folder once and pages the ordered batch locally", async () => {
    const sortedFiles = Array.from({ length: 205 }, (_, index) =>
      createFile({ id: `ordered-${index}` }),
    );
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (_, options) => ({
        content: {
          node: createFolder(),
          ...createFolder(),
          nodes: [],
          files: options?.listing?.descending
            ? [...sortedFiles].reverse()
            : sortedFiles,
          stats: { folders: 0, files: 205, sizeBytes: 205 },
        },
        totalCount: 205,
      }));
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
    await waitFor(() =>
      expect(result.current.content?.files[0]?.id).toBe("ordered-204"),
    );
    act(() =>
      result.current.pagination?.onPaginationModelChange({
        page: 1,
        pageSize: 100,
      }),
    );
    expect(result.current.content?.files[0].id).toBe("ordered-104");
    expect(get).toHaveBeenCalledTimes(2);
    expect(get).toHaveBeenLastCalledWith("folder-1", {
      page: 1,
      pageSize: 25000,
      signal: expect.any(AbortSignal),
      listing: { sortBy: "Name", descending: true },
    });
  });
});
