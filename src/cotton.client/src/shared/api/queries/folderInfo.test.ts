import { QueryClient, QueryObserver } from "@tanstack/react-query";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  clearFolderInfoCache,
  FOLDER_SIZE_REFRESH_DELAY_MS,
  invalidateFolderInfoCache,
} from "./folderInfo";
import { queryKeys } from "./queryKeys";
import { invalidateCachedFolder } from "./nodeCache";

let client: QueryClient;
const key = queryKeys.nodeChildren.folderSizes("parent", "user");

beforeEach(() => {
  vi.useFakeTimers();
  client = new QueryClient();
});

afterEach(() => {
  clearFolderInfoCache(client);
  client.clear();
  vi.useRealTimers();
});

it("coalesces upload invalidations and retains the last full size until refresh completes", async () => {
  const fetch = vi.fn().mockResolvedValue(200);
  client.setQueryData(key, 100);
  const observer = new QueryObserver(client, {
    queryKey: key,
    queryFn: fetch,
    staleTime: Infinity,
  });
  const unsubscribe = observer.subscribe(() => {});

  for (let index = 0; index < 10; index++) {
    invalidateCachedFolder(client, "parent", "user");
    await vi.advanceTimersByTimeAsync(400);
    expect(client.getQueryData(key)).toBe(100);
    expect(fetch).not.toHaveBeenCalled();
  }
  await vi.advanceTimersByTimeAsync(FOLDER_SIZE_REFRESH_DELAY_MS - 4_000);
  expect(fetch).toHaveBeenCalledTimes(1);
  expect(client.getQueryData(key)).toBe(200);
  unsubscribe();
});

it("does not cancel a slow calculation and refreshes again for changes made during it", async () => {
  let finish: ((size: number) => void) | undefined;
  let signal: AbortSignal | undefined;
  const fetch = vi.fn(({ signal: requestSignal }: { signal: AbortSignal }) => {
    signal = requestSignal;
    return new Promise<number>((resolve) => {
      finish = resolve;
    });
  });
  client.setQueryData(key, 100);
  const observer = new QueryObserver(client, { queryKey: key, queryFn: fetch });
  const unsubscribe = observer.subscribe(() => {});
  invalidateFolderInfoCache(client);
  await vi.advanceTimersByTimeAsync(FOLDER_SIZE_REFRESH_DELAY_MS);
  expect(fetch).toHaveBeenCalledTimes(1);
  expect(signal?.aborted).toBe(false);
  expect(client.getQueryData(key)).toBe(100);

  invalidateFolderInfoCache(client);
  fetch.mockResolvedValue(300);
  finish?.(200);
  await vi.advanceTimersByTimeAsync(0);
  expect(fetch).toHaveBeenCalledTimes(2);
  expect(client.getQueryData(key)).toBe(300);
  unsubscribe();
});

it.each([false, true])(
  "clears a queued refresh on logout, with calculation running: %s",
  async (running) => {
    let finish: ((size: number) => void) | undefined;
    const fetch = vi.fn(
      () =>
        new Promise<number>((resolve) => {
          finish = resolve;
        }),
    );
    client.setQueryData(key, 100);
    const observer = new QueryObserver(client, {
      queryKey: key,
      queryFn: fetch,
      staleTime: running ? 0 : Infinity,
    });
    const unsubscribe = observer.subscribe(() => {});
    invalidateFolderInfoCache(client);
    if (running) {
      await vi.advanceTimersByTimeAsync(FOLDER_SIZE_REFRESH_DELAY_MS);
    }
    clearFolderInfoCache(client);
    finish?.(200);
    await vi.advanceTimersByTimeAsync(FOLDER_SIZE_REFRESH_DELAY_MS);
    expect(fetch).toHaveBeenCalledTimes(running ? 1 : 0);
    expect(client.getQueryData(key)).toBeUndefined();
    unsubscribe();
  },
);
