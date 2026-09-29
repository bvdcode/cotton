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
  createFile({ id: `file-${index}`, name: `file-${index}.txt` }),
);

const response = (page: number): NodeResponse => ({
  content: {
    ...createFolder({ id: "folder-1" }),
    nodes: [],
    files: page === 1 ? files.slice(0, 100) : files.slice(100),
    stats:
      page === 1
        ? { folders: 0, files: 101, encryptedFiles: 1, sizeBytes: 101 }
        : null,
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
  it("shows a single list page while keeping totals for the whole folder", async () => {
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (_, options) => response(options?.page ?? 1));
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
      encryptedFiles: 1,
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
    expect(get).toHaveBeenCalledTimes(2);
    expect(get).toHaveBeenNthCalledWith(1, "folder-1", {
      page: 1,
      pageSize: 100,
      includeStats: true,
    });
    expect(get).toHaveBeenNthCalledWith(2, "folder-1", {
      page: 2,
      pageSize: 100,
    });
  });

  it("loads another tile page only when requested", async () => {
    const tileFiles = Array.from({ length: 1001 }, (_, index) =>
      createFile({ id: `tile-${index}` }),
    );
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (_, options) => ({
        content: {
          ...createFolder(),
          nodes: [],
          files:
            options?.page === 1
              ? tileFiles.slice(0, 1000)
              : tileFiles.slice(1000),
          stats: {
            folders: 0,
            files: 1001,
            encryptedFiles: 0,
            sizeBytes: 1001,
          },
        },
        totalCount: 1001,
      }));
    const { result } = renderHook(
      () => useFolderListing("folder-1", "user-1", InterfaceLayoutType.Tiles),
      { wrapper },
    );

    await waitFor(() =>
      expect(result.current.content?.files).toHaveLength(1000),
    );
    expect(get).toHaveBeenCalledTimes(1);
    act(() => result.current.loadMore?.());
    await waitFor(() =>
      expect(result.current.content?.files).toHaveLength(1001),
    );
    expect(result.current.loadMore).toBeUndefined();
    expect(get).toHaveBeenCalledTimes(2);
    expect(result.current.stats.sizeBytes).toBe(1001);
    expect(get).toHaveBeenNthCalledWith(1, "folder-1", {
      page: 1,
      pageSize: 1000,
      includeStats: true,
    });
    expect(get).toHaveBeenNthCalledWith(2, "folder-1", {
      page: 2,
      pageSize: 1000,
    });
  });

  it("loads 638 folders in one tile request and keeps table pages separate", async () => {
    const folders = Array.from({ length: 638 }, (_, index) =>
      createFolder({ id: `child-${index}` }),
    );
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (_, options) => ({
        content: {
          ...createFolder(),
          nodes: folders.slice(0, options?.pageSize),
          files: [],
          stats: { folders: 638, files: 0, encryptedFiles: 0, sizeBytes: 0 },
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
      pageSize: 1000,
      includeStats: true,
    });

    rerender(InterfaceLayoutType.List);
    await waitFor(() =>
      expect(result.current.content?.nodes).toHaveLength(100),
    );
    expect(result.current.pagination?.model?.pageSize).toBe(100);
    rerender(InterfaceLayoutType.Tiles);
    await waitFor(() =>
      expect(result.current.content?.nodes).toHaveLength(638),
    );
    expect(get).toHaveBeenCalledTimes(2);
    expect(result.current.loadMore).toBeUndefined();
  });
});
