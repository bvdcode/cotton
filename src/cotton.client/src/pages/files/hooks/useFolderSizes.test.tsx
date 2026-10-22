import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { nodesApi, type NodeResponse } from "@shared/api/nodesApi";
import { queryClient } from "@shared/api/queries/queryClient";
import { queryKeys } from "@shared/api/queries/queryKeys";
import {
  clearFolderInfoCache,
  FOLDER_SIZE_STALE_TIME,
  invalidateFolderInfoCache,
  useCachedFolderSize,
} from "@shared/api/queries/folderInfo";
import { createFolder } from "../../../test/fileFixtures";
import { calculateFolderSizes, useFolderSizes } from "./useFolderSizes";

vi.mock("@shared/store/authStore", () => ({
  useAuthStore: (select: (state: { user: { id: string } }) => string) =>
    select({ user: { id: "user" } }),
}));
vi.mock("@shared/ui/notifications", () => ({ toast: { error: vi.fn() } }));
const stats = (sizeBytes: number) => ({
  sizeBytes,
  folders: 0,
  files: 1,
});
const listing = (ids: string[], totalCount = ids.length): NodeResponse => ({
  content: {
    ...createFolder({ id: "parent" }),
    nodes: ids.map((id) => createFolder({ id })),
    files: [],
  },
  totalCount,
});
const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);

afterEach(() => {
  cleanup();
  queryClient.clear();
  vi.restoreAllMocks();
});

describe("folder size calculation", () => {
  it("shows the cached total while refreshing expired sizes on reopening", async () => {
    const children = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(listing(["one"]));
    const getStats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockResolvedValue(stats(10));
    const first = renderHook(() => useFolderSizes("parent"), { wrapper });
    await waitFor(() => expect(first.result.current.sizeBytes).toBe(10));
    first.unmount();

    const fresh = renderHook(() => useFolderSizes("parent"), { wrapper });
    expect(fresh.result.current.sizeBytes).toBe(10);
    expect(getStats).toHaveBeenCalledTimes(1);
    fresh.unmount();

    const expiredAt = Date.now() - FOLDER_SIZE_STALE_TIME - 1;
    queryClient.setQueryData(
      queryKeys.nodeChildren.folderInfo("one", "user"),
      stats(10),
      { updatedAt: expiredAt },
    );
    queryClient.setQueryData(
      queryKeys.nodeChildren.folderSizes("parent", "user"),
      10,
      { updatedAt: expiredAt },
    );
    let complete: ((value: ReturnType<typeof stats>) => void) | undefined;
    getStats.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          complete = resolve;
        }),
    );
    const reopened = renderHook(() => useFolderSizes("parent"), { wrapper });
    expect(reopened.result.current.sizeBytes).toBe(10);
    await waitFor(() => expect(getStats).toHaveBeenCalledTimes(2));
    expect(children).toHaveBeenCalledTimes(2);
    expect(reopened.result.current.sizeBytes).toBe(10);
    await act(async () => {
      complete?.(stats(25));
    });
    await waitFor(() => expect(reopened.result.current.sizeBytes).toBe(25));
  });

  it("automatically publishes zero sizes to the memory cache", async () => {
    vi.spyOn(nodesApi, "getChildren").mockResolvedValue(listing(["empty"]));
    const getStats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockResolvedValue(stats(0));
    const { result } = renderHook(
      () => ({
        run: useFolderSizes("parent"),
        size: useCachedFolderSize("empty"),
      }),
      { wrapper },
    );
    expect(result.current.size).toBeUndefined();
    await waitFor(() => expect(result.current.size).toBe(0));
    expect(getStats).toHaveBeenCalledWith(
      "empty",
      true,
      expect.any(AbortSignal),
    );
  });

  it("reads every folder page and stops before pages containing only files", async () => {
    const firstIds = Array.from({ length: 25_000 }, (_, i) => `folder-${i}`);
    const children = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValueOnce(listing(firstIds, 75_000))
      .mockResolvedValueOnce(listing(["last"], 75_000));
    const getStats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockResolvedValue(stats(42));
    const client = new QueryClient();
    await calculateFolderSizes(
      client,
      "parent",
      "user",
      new AbortController().signal,
    );
    expect(children).toHaveBeenCalledTimes(2);
    expect(children).toHaveBeenLastCalledWith("parent", {
      page: 2,
      pageSize: 25_000,
      signal: expect.any(AbortSignal),
    });
    expect(getStats).toHaveBeenCalledTimes(25_001);
    expect(
      client.getQueryData(queryKeys.nodeChildren.folderInfo("last", "user")),
    ).toEqual(stats(42));
    client.clear();
  });

  it("runs requests sequentially, publishes partial sums and reuses cached sizes", async () => {
    vi.spyOn(nodesApi, "getChildren").mockResolvedValue(
      listing(["one", "two"]),
    );
    let finishFirst: (() => void) | undefined;
    const getStats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            finishFirst = () => resolve(stats(1));
          }),
      )
      .mockResolvedValue(stats(2));
    const signal = new AbortController().signal;
    const progress = vi.fn();
    const run = calculateFolderSizes(
      queryClient,
      "parent",
      "user",
      signal,
      progress,
    );
    await waitFor(() => expect(getStats).toHaveBeenCalledTimes(1));
    finishFirst?.();
    expect(progress).not.toHaveBeenCalled();
    await expect(run).resolves.toBe(3);
    expect(progress.mock.calls).toEqual([[1], [3]]);
    expect(getStats).toHaveBeenCalledTimes(2);
    await calculateFolderSizes(queryClient, "parent", "user", signal);
    expect(getStats).toHaveBeenCalledTimes(2);
    expect(
      queryClient.getQueryData(
        queryKeys.nodeChildren.folderInfo("one", "user"),
      ),
    ).toEqual(stats(1));
  });

  it("stops on errors and leaves an uncalculated folder without a size", async () => {
    vi.spyOn(nodesApi, "getChildren").mockResolvedValue(
      listing(["one", "broken", "later"]),
    );
    const getStats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockResolvedValueOnce(stats(1))
      .mockRejectedValueOnce(new Error("offline"));
    await expect(
      calculateFolderSizes(
        queryClient,
        "parent",
        "user",
        new AbortController().signal,
      ),
    ).rejects.toThrow("offline");
    expect(getStats).toHaveBeenCalledTimes(2);
    expect(
      queryClient.getQueryData(
        queryKeys.nodeChildren.folderInfo("broken", "user"),
      ),
    ).toBeUndefined();
  });

  it.each(["navigation", "unmount"])(
    "cancels the active request on %s",
    async (exit) => {
      vi.spyOn(nodesApi, "getChildren")
        .mockResolvedValueOnce(listing(["one", "two"]))
        .mockResolvedValue(listing([]));
      let requestSignal: AbortSignal | undefined;
      const getStats = vi
        .spyOn(nodesApi, "getFolderStats")
        .mockImplementation((_id, _recursive, signal) => {
          requestSignal = signal;
          return new Promise((_resolve, reject) =>
            signal?.addEventListener("abort", () =>
              reject(new Error("cancelled")),
            ),
          );
        });
      const { result, rerender, unmount } = renderHook(
        ({ id }) => useFolderSizes(id),
        {
          wrapper,
          initialProps: { id: "parent" },
        },
      );
      await waitFor(() => expect(getStats).toHaveBeenCalledTimes(1));
      if (exit === "navigation") {
        rerender({ id: "another-parent" });
        await waitFor(() => expect(result.current.isCalculating).toBe(false));
      } else {
        unmount();
      }
      expect(requestSignal?.aborted).toBe(true);
      expect(getStats).toHaveBeenCalledTimes(1);
    },
  );

  it("shows progress, reuses a completed total on return and recalculates after content changes", async () => {
    const children = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(listing(["one", "two"]));
    let finishSecond: (() => void) | undefined;
    const getStats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockResolvedValueOnce(stats(10))
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            finishSecond = () => resolve(stats(20));
          }),
      )
      .mockResolvedValue(stats(50));
    const { result, rerender } = renderHook(({ id }) => useFolderSizes(id), {
      wrapper,
      initialProps: { id: "parent" as string | null },
    });
    await waitFor(() => expect(result.current.sizeBytes).toBe(10));
    expect(result.current.isCalculating).toBe(true);
    await act(async () => finishSecond?.());
    await waitFor(() => expect(result.current.isCalculating).toBe(false));
    expect(result.current.sizeBytes).toBe(30);
    rerender({ id: null });
    expect(result.current.sizeBytes).toBe(0);
    rerender({ id: "parent" });
    expect(result.current.sizeBytes).toBe(30);
    expect(result.current.isCalculating).toBe(false);
    expect(children).toHaveBeenCalledTimes(1);
    expect(getStats).toHaveBeenCalledTimes(2);
    await act(async () => invalidateFolderInfoCache(queryClient));
    await waitFor(() => expect(result.current.sizeBytes).toBe(100));
    expect(getStats).toHaveBeenCalledTimes(4);
  });

  it("reuses the directory overview already loaded for tiles", async () => {
    queryClient.setQueryData(
      queryKeys.nodeChildren.overview("parent", "user", 25_000),
      listing(["one", "two"]),
    );
    const children = vi.spyOn(nodesApi, "getChildren");
    vi.spyOn(nodesApi, "getFolderStats").mockResolvedValue(stats(10));
    const { result } = renderHook(() => useFolderSizes("parent"), { wrapper });
    await waitFor(() => expect(result.current.sizeBytes).toBe(20));
    expect(children).not.toHaveBeenCalled();
  });

  it("does not reuse an invalidated overview after the directory changes", async () => {
    const key = queryKeys.nodeChildren.overview("parent", "user", 25_000);
    queryClient.setQueryData(key, listing(["removed"]));
    await queryClient.invalidateQueries({ queryKey: key, refetchType: "none" });
    vi.spyOn(nodesApi, "getChildren").mockResolvedValue(listing(["added"]));
    const getStats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockResolvedValue(stats(25));
    const { result } = renderHook(() => useFolderSizes("parent"), { wrapper });
    await waitFor(() => expect(result.current.sizeBytes).toBe(25));
    expect(getStats).toHaveBeenCalledExactlyOnceWith(
      "added",
      true,
      expect.any(AbortSignal),
    );
  });

  it("waits for directory contents before starting", async () => {
    const children = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(listing([]));
    const { result, rerender } = renderHook(
      ({ enabled }) => useFolderSizes("parent", enabled),
      {
        wrapper,
        initialProps: { enabled: false },
      },
    );
    expect(children).not.toHaveBeenCalled();
    expect(result.current.isCalculating).toBe(false);
    rerender({ enabled: true });
    await waitFor(() => expect(children).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(result.current.isCalculating).toBe(false));
  });

  it("cancels the folder listing before statistics requests start", async () => {
    let requestSignal: AbortSignal | undefined;
    vi.spyOn(nodesApi, "getChildren").mockImplementation((_id, options) => {
      requestSignal = options?.signal;
      return new Promise((_resolve, reject) =>
        requestSignal?.addEventListener("abort", () =>
          reject(new Error("cancelled")),
        ),
      );
    });
    const getStats = vi.spyOn(nodesApi, "getFolderStats");
    const { unmount } = renderHook(() => useFolderSizes("parent"), { wrapper });
    await waitFor(() => expect(requestSignal).toBeDefined());
    unmount();
    expect(requestSignal?.aborted).toBe(true);
    expect(getStats).not.toHaveBeenCalled();
  });

  it("resumes an interrupted folder after a quick return without caching a partial total", async () => {
    vi.spyOn(nodesApi, "getChildren").mockResolvedValue(
      listing(["one", "two"]),
    );
    const getStats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockResolvedValueOnce(stats(10))
      .mockImplementationOnce(
        (_id, _recursive, signal) =>
          new Promise((_resolve, reject) => {
            signal?.addEventListener("abort", () =>
              reject(new Error("cancelled")),
            );
          }),
      )
      .mockResolvedValue(stats(20));
    const { result, rerender } = renderHook(({ id }) => useFolderSizes(id), {
      wrapper,
      initialProps: { id: "parent" as string | null },
    });
    await waitFor(() => expect(result.current.sizeBytes).toBe(10));
    await waitFor(() => expect(getStats).toHaveBeenCalledTimes(2));
    rerender({ id: null });
    expect(
      queryClient.getQueryData(
        queryKeys.nodeChildren.folderSizes("parent", "user"),
      ),
    ).toBeUndefined();
    rerender({ id: "parent" });
    await waitFor(() => expect(result.current.isCalculating).toBe(false));
    expect(result.current.sizeBytes).toBe(30);
    expect(getStats).toHaveBeenCalledTimes(3);
  });

  it("separates users and clears stored sizes when the session changes", () => {
    queryClient.setQueryData(
      queryKeys.nodeChildren.folderInfo("one", "other-user"),
      stats(100),
    );
    const { result } = renderHook(() => useCachedFolderSize("one"));
    expect(result.current).toBeUndefined();
    clearFolderInfoCache(queryClient);
    expect(
      queryClient.getQueryData(
        queryKeys.nodeChildren.folderInfo("one", "other-user"),
      ),
    ).toBeUndefined();
  });
});
