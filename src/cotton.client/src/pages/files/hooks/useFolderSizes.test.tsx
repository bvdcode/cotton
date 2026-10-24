import { QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { nodesApi, type FolderStatsDto } from "@shared/api/nodesApi";
import { queryClient } from "@shared/api/queries/queryClient";
import { queryKeys } from "@shared/api/queries/queryKeys";
import {
  clearFolderInfoCache,
  FOLDER_SIZE_STALE_TIME,
  invalidateFolderInfoCache,
  useCachedFolderSize,
} from "@shared/api/queries/folderInfo";
import { calculateFolderSizes, useFolderSizes } from "./useFolderSizes";

vi.mock("@shared/store/authStore", () => ({
  useAuthStore: (select: (state: { user: { id: string } }) => string) =>
    select({ user: { id: "user" } }),
}));
vi.mock("@shared/ui/notifications", () => ({ toast: { error: vi.fn() } }));
const stats = (sizeBytes: number) => ({ sizeBytes, folders: 0, files: 1 });
const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);
afterEach(() => {
  cleanup();
  queryClient.clear();
  vi.restoreAllMocks();
});

describe("batched folder sizes", () => {
  it("fetches all sizes once without reading listings or individual folder stats", async () => {
    const children = vi.spyOn(nodesApi, "getChildren");
    const individual = vi.spyOn(nodesApi, "getFolderStats");
    const batch = vi.spyOn(nodesApi, "getChildFolderStats").mockResolvedValue({
      first: stats(20),
      second: stats(10),
      empty: stats(0),
    });
    const { result } = renderHook(
      () => ({
        total: useFolderSizes("parent"),
        empty: useCachedFolderSize("empty"),
      }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.total.sizeBytes).toBe(30));
    expect(result.current.empty).toBe(0);
    expect(batch).toHaveBeenCalledExactlyOnceWith("parent", {
      signal: expect.any(AbortSignal),
    });
    expect(children).not.toHaveBeenCalled();
    expect(individual).not.toHaveBeenCalled();
  });

  it("keeps cached totals on return and refreshes once after file changes", async () => {
    const batch = vi
      .spyOn(nodesApi, "getChildFolderStats")
      .mockResolvedValueOnce({ first: stats(10) })
      .mockResolvedValue({ first: stats(25) });
    const { result, rerender } = renderHook(({ id }) => useFolderSizes(id), {
      wrapper,
      initialProps: { id: "parent" as string | null },
    });
    await waitFor(() => expect(result.current.sizeBytes).toBe(10));
    rerender({ id: null });
    expect(result.current.sizeBytes).toBe(0);
    rerender({ id: "parent" });
    expect(result.current.sizeBytes).toBe(10);
    expect(batch).toHaveBeenCalledTimes(1);
    await act(async () => invalidateFolderInfoCache(queryClient));
    await waitFor(() => expect(result.current.sizeBytes).toBe(25));
    expect(batch).toHaveBeenCalledTimes(2);
  });

  it("shows the previous total while refreshing expired data", async () => {
    queryClient.setQueryData(
      queryKeys.nodeChildren.folderSizes("parent", "user"),
      10,
      { updatedAt: Date.now() - FOLDER_SIZE_STALE_TIME - 1 },
    );
    let finish: ((value: Record<string, FolderStatsDto>) => void) | undefined;
    vi.spyOn(nodesApi, "getChildFolderStats").mockImplementation(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const { result } = renderHook(() => useFolderSizes("parent"), { wrapper });
    expect(result.current.sizeBytes).toBe(10);
    expect(result.current.isCalculating).toBe(true);
    await act(async () => finish?.({ first: stats(25) }));
    await waitFor(() => expect(result.current.sizeBytes).toBe(25));
  });

  it("does not carry the previous total into a different folder", async () => {
    vi.spyOn(nodesApi, "getChildFolderStats")
      .mockResolvedValueOnce({ first: stats(10) })
      .mockImplementation(() => new Promise(() => {}));
    const { result, rerender } = renderHook(({ id }) => useFolderSizes(id), {
      wrapper,
      initialProps: { id: "parent" },
    });
    await waitFor(() => expect(result.current.sizeBytes).toBe(10));
    rerender({ id: "next" });
    expect(result.current.sizeBytes).toBe(0);
    expect(result.current.isCalculating).toBe(true);
  });

  it.each(["navigation", "unmount"])(
    "ignores a late response after %s",
    async (exit) => {
      let finish: ((value: Record<string, FolderStatsDto>) => void) | undefined;
      let signal: AbortSignal | undefined;
      vi.spyOn(nodesApi, "getChildFolderStats").mockImplementation(
        (_id, options) => {
          signal = options?.signal;
          return new Promise((resolve) => {
            finish = resolve;
          });
        },
      );
      const { result, rerender, unmount } = renderHook(
        ({ id }) => useFolderSizes(id),
        {
          wrapper,
          initialProps: { id: "parent" as string | null },
        },
      );
      await waitFor(() => expect(signal).toBeDefined());
      if (exit === "navigation") {
        rerender({ id: null });
        expect(result.current.sizeBytes).toBe(0);
      } else {
        unmount();
      }
      expect(signal?.aborted).toBe(true);
      await act(async () => finish?.({ first: stats(99) }));
      expect(
        queryClient.getQueryData(
          queryKeys.nodeChildren.folderInfo("first", "user"),
        ),
      ).toBeUndefined();
      expect(
        queryClient.getQueryData(
          queryKeys.nodeChildren.folderSizes("parent", "user"),
        ),
      ).toBeUndefined();
    },
  );

  it("waits until the listing is ready", async () => {
    const batch = vi
      .spyOn(nodesApi, "getChildFolderStats")
      .mockResolvedValue({});
    const { result, rerender } = renderHook(
      ({ enabled }) => useFolderSizes("parent", enabled),
      {
        wrapper,
        initialProps: { enabled: false },
      },
    );
    expect(batch).not.toHaveBeenCalled();
    rerender({ enabled: true });
    await waitFor(() => expect(batch).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(result.current.isCalculating).toBe(false));
  });

  it("propagates errors without publishing partial sizes", async () => {
    vi.spyOn(nodesApi, "getChildFolderStats").mockRejectedValue(
      new Error("offline"),
    );
    await expect(
      calculateFolderSizes(
        queryClient,
        "parent",
        "user",
        new AbortController().signal,
      ),
    ).rejects.toThrow("offline");
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  });

  it("separates accounts and clears sizes on session change", () => {
    queryClient.setQueryData(
      queryKeys.nodeChildren.folderInfo("first", "other"),
      stats(100),
    );
    const { result } = renderHook(() => useCachedFolderSize("first"), {
      wrapper,
    });
    expect(result.current).toBeUndefined();
    clearFolderInfoCache(queryClient);
    expect(
      queryClient.getQueryData(
        queryKeys.nodeChildren.folderInfo("first", "other"),
      ),
    ).toBeUndefined();
  });
});
