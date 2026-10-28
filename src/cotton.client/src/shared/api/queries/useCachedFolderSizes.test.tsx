import { act, cleanup, renderHook } from "@testing-library/react";
import { useLayoutEffect } from "react";
import { flushSync } from "react-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { queryClient } from "./queryClient";
import { queryKeys } from "./queryKeys";
import { useCachedFolderSizes } from "./useCachedFolderSizes";

const auth = vi.hoisted(() => ({ userId: "user" }));
vi.mock("../../store/authStore", () => ({
  useAuthStore: (select: (state: { user: { id: string } }) => string) =>
    select({ user: { id: auth.userId } }),
}));

const stats = (sizeBytes: number) => ({ sizeBytes, folders: 0, files: 1 });
const writeSize = (id: string, size: number, userId = "user") =>
  queryClient.setQueryData(
    queryKeys.nodeChildren.folderInfo(id, userId),
    stats(size),
  );

afterEach(() => {
  cleanup();
  queryClient.clear();
  auth.userId = "user";
  vi.restoreAllMocks();
});

describe("cached folder size snapshots", () => {
  it("reads cached sizes including zero and isolates folders and accounts", () => {
    writeSize("first", 10);
    writeSize("empty", 0);
    writeSize("outside", 20);
    writeSize("missing", 30, "other");
    const ids = ["first", "empty", "missing"];
    const { result } = renderHook(() => useCachedFolderSizes(ids));

    expect(result.current).toEqual(
      new Map([
        ["first", 10],
        ["empty", 0],
      ]),
    );
  });

  it("coalesces repeated updates and removals into the latest values", async () => {
    writeSize("first", 10);
    const ids = ["first", "second"];
    const { result } = renderHook(() => useCachedFolderSizes(ids));
    const previous = result.current;

    await act(async () => {
      writeSize("first", 20);
      writeSize("second", 30);
      writeSize("second", 0);
      writeSize("outside", 40);
      writeSize("second", 50, "other");
      queryClient.removeQueries({
        queryKey: queryKeys.nodeChildren.folderInfo("first", "user"),
        exact: true,
      });
    });

    expect(result.current).toEqual(new Map([["second", 0]]));
    expect(previous).toEqual(new Map([["first", 10]]));
    await act(async () => queryClient.clear());
    expect(result.current.size).toBe(0);
  });

  it("does not expose partial batches even when React flushes synchronously", async () => {
    const ids = Array.from({ length: 50 }, (_, index) => `folder-${index}`);
    const observed = new Set<ReadonlyMap<string, number>>();
    renderHook(() => {
      const sizes = useCachedFolderSizes(ids);
      observed.add(sizes);
    });

    await act(async () => {
      for (const [index, id] of ids.entries()) {
        flushSync(() => writeSize(id, index));
      }
    });

    expect([...observed].map((snapshot) => snapshot.size)).toEqual([0, 50]);
  });

  it("keeps the snapshot when a notification does not change a size", async () => {
    writeSize("first", 10);
    const ids = ["first"];
    const { result } = renderHook(() => useCachedFolderSizes(ids));
    const previous = result.current;

    await act(async () => {
      queryClient.setQueryData(
        queryKeys.nodeChildren.folderInfo("first", "user"),
        { ...stats(10), files: 2 },
      );
      writeSize("outside", 30);
      writeSize("first", 40, "other");
    });

    expect(result.current).toBe(previous);
  });

  it("captures updates between rendering and subscribing", () => {
    const ids = ["first"];
    const { result } = renderHook(() => {
      const sizes = useCachedFolderSizes(ids);
      useLayoutEffect(() => {
        writeSize("first", 25);
      }, []);
      return sizes;
    });

    expect(result.current.get("first")).toBe(25);
  });

  it("drops pending notifications when the folder selection or account changes", async () => {
    const { result, rerender, unmount } = renderHook(
      ({ ids }) => useCachedFolderSizes(ids),
      { initialProps: { ids: ["first"] } },
    );
    const previous = result.current;

    act(() => {
      writeSize("first", 10);
    });
    rerender({ ids: ["second"] });
    auth.userId = "other";
    rerender({ ids: ["second"] });
    await act(async () => {
      writeSize("second", 20);
      writeSize("second", 30, "other");
    });

    expect(result.current).toEqual(new Map([["second", 30]]));
    expect(previous.size).toBe(0);
    const last = result.current;
    act(() => {
      writeSize("second", 40, "other");
    });
    unmount();
    await act(async () => {});
    expect(last).toEqual(new Map([["second", 30]]));
  });
});
