import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { nodesApi, type NodeResponse } from "@shared/api/nodesApi";
import { queryClient } from "@shared/api/queries/queryClient";
import { queryKeys } from "@shared/api/queries/queryKeys";
import {
  clearFolderInfoCache,
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
  encryptedFiles: 0,
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
  it("does no work on mount and publishes zero sizes to the memory cache after a click", async () => {
    const children = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(listing(["empty"]));
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
    expect(children).not.toHaveBeenCalled();
    expect(getStats).not.toHaveBeenCalled();
    act(() => result.current.run.calculate());
    await waitFor(() => expect(result.current.size).toBe(0));
    expect(getStats).toHaveBeenCalledWith(
      "empty",
      true,
      expect.any(AbortSignal),
    );
  });

  it("reads every folder page and stops before pages containing only files", async () => {
    const firstIds = Array.from({ length: 10_000 }, (_, i) => `folder-${i}`);
    const children = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValueOnce(listing(firstIds, 50_000))
      .mockResolvedValueOnce(listing(["last"], 50_000));
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
      pageSize: 10_000,
      signal: expect.any(AbortSignal),
    });
    expect(getStats).toHaveBeenCalledTimes(10_001);
    expect(
      client.getQueryData(queryKeys.nodeChildren.folderInfo("last", "user")),
    ).toEqual(stats(42));
    client.clear();
  });

  it("runs requests one at a time and refreshes cached sizes on a repeated calculation", async () => {
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
    const run = calculateFolderSizes(queryClient, "parent", "user", signal);
    await waitFor(() => expect(getStats).toHaveBeenCalledTimes(1));
    finishFirst?.();
    await run;
    expect(getStats).toHaveBeenCalledTimes(2);
    await calculateFolderSizes(queryClient, "parent", "user", signal);
    expect(getStats).toHaveBeenCalledTimes(4);
    expect(
      queryClient.getQueryData(
        queryKeys.nodeChildren.folderInfo("one", "user"),
      ),
    ).toEqual(stats(2));
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
      vi.spyOn(nodesApi, "getChildren").mockResolvedValue(
        listing(["one", "two"]),
      );
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
      act(() => result.current.calculate());
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
