import { QueryClient, QueryObserver } from "@tanstack/react-query";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  clearFolderListingRefresh,
  FOLDER_REFRESH_DELAY_MS,
  refreshFolderListings,
} from "./folderRefresh";
import { queryKeys } from "./queryKeys";

let client: QueryClient;
const subscriptions: (() => void)[] = [];
const key = queryKeys.nodeChildren.overview("folder", "user", 25000);

function observe(
  queryKey: readonly string[] | typeof key,
  fetch: () => Promise<number>,
) {
  client.setQueryData(queryKey, 0);
  const observer = new QueryObserver(client, {
    queryKey,
    queryFn: fetch,
    staleTime: Infinity,
  });
  subscriptions.push(observer.subscribe(() => {}));
}

beforeEach(() => {
  vi.useFakeTimers();
  client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
});

afterEach(() => {
  clearFolderListingRefresh(client);
  for (const unsubscribe of subscriptions.splice(0)) {
    unsubscribe();
  }
  client.clear();
  vi.useRealTimers();
});

describe("folder listing refresh", () => {
  it("coalesces local and hub bursts while keeping cached data visible", async () => {
    const fetch = vi.fn(async () => 1);
    observe(key, fetch);
    const completed = Array.from({ length: 100 }, () =>
      refreshFolderListings(client, "folder", "user"),
    );
    expect(client.getQueryState(key)?.isInvalidated).toBe(true);
    expect(client.getQueryData(key)).toBe(0);
    await vi.advanceTimersByTimeAsync(FOLDER_REFRESH_DELAY_MS - 1);
    expect(fetch).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    await Promise.all(completed);
    expect(fetch).toHaveBeenCalledOnce();
    expect(client.getQueryData(key)).toBe(1);
  });

  it("refreshes during a continuous stream instead of waiting for silence", async () => {
    const fetch = vi.fn(async () => 1);
    observe(key, fetch);
    for (let index = 0; index < 20; index++) {
      void refreshFolderListings(client, "folder");
      await vi.advanceTimersByTimeAsync(FOLDER_REFRESH_DELAY_MS / 10);
    }
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it("waits for a slow request, then refreshes again without aborting or overlapping it", async () => {
    let finishFirst: (value: number) => void = () => {};
    let finishSecond: (value: number) => void = () => {};
    const first = new Promise<number>((resolve) => {
      finishFirst = resolve;
    });
    const second = new Promise<number>((resolve) => {
      finishSecond = resolve;
    });
    const aborted = vi.fn();
    const fetch = vi
      .fn()
      .mockImplementationOnce(({ signal }: { signal: AbortSignal }) => {
        signal.addEventListener("abort", aborted);
        return first;
      })
      .mockImplementationOnce(() => second)
      .mockResolvedValue(3);
    client.setQueryData(key, 0);
    const observer = new QueryObserver(client, {
      queryKey: key,
      queryFn: fetch,
      staleTime: Infinity,
    });
    subscriptions.push(observer.subscribe(() => {}));
    const initial = observer.refetch();
    const refreshed = refreshFolderListings(client, "folder");
    await vi.advanceTimersByTimeAsync(FOLDER_REFRESH_DELAY_MS * 2);
    expect(fetch).toHaveBeenCalledOnce();
    expect(aborted).not.toHaveBeenCalled();
    finishFirst(1);
    await initial;
    await vi.advanceTimersByTimeAsync(0);
    expect(fetch).toHaveBeenCalledTimes(2);
    const following = refreshFolderListings(client, "folder");
    await vi.advanceTimersByTimeAsync(FOLDER_REFRESH_DELAY_MS * 2);
    expect(fetch).toHaveBeenCalledTimes(2);
    finishSecond(2);
    await refreshed;
    await vi.advanceTimersByTimeAsync(FOLDER_REFRESH_DELAY_MS);
    await following;
    expect(fetch).toHaveBeenCalledTimes(3);
    expect(aborted).not.toHaveBeenCalled();
    expect(client.getQueryData(key)).toBe(3);
  });

  it("isolates users and folders and leaves recursive statistics to their own refresh", async () => {
    const fetch = vi.fn(async () => 1);
    const otherUser = vi.fn(async () => 1);
    const otherFolder = vi.fn(async () => 1);
    const stats = vi.fn(async () => 1);
    observe(key, fetch);
    observe(queryKeys.nodeChildren.tiles("folder", "other"), otherUser);
    observe(queryKeys.nodeChildren.tiles("other", "user"), otherFolder);
    observe(queryKeys.nodeChildren.folderInfo("folder", "user"), stats);
    const refreshed = refreshFolderListings(client, "folder", "user");
    await vi.advanceTimersByTimeAsync(FOLDER_REFRESH_DELAY_MS);
    await refreshed;
    expect(fetch).toHaveBeenCalledOnce();
    expect(otherUser).not.toHaveBeenCalled();
    expect(otherFolder).not.toHaveBeenCalled();
    expect(stats).not.toHaveBeenCalled();
  });

  it("drops queued work when the session is cleared", async () => {
    const fetch = vi.fn(async () => 1);
    observe(key, fetch);
    const refreshed = refreshFolderListings(client, "folder");
    clearFolderListingRefresh(client);
    await refreshed;
    await vi.advanceTimersByTimeAsync(FOLDER_REFRESH_DELAY_MS * 2);
    expect(fetch).not.toHaveBeenCalled();
  });
});
