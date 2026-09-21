import { act, renderHook, waitFor } from "@testing-library/react";
import {
  QueryClient,
  QueryClientProvider,
  useQuery,
} from "@tanstack/react-query";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { queryKeys } from "../../shared/api/queries/queryKeys";
import { HUB_METHODS } from "../../shared/signalr/hubMethods";
import type { JsonValue } from "../../shared/types/json";
import { useHomeRealtimeEvents } from "./useHomeRealtimeEvents";

type EventCallback = (...args: JsonValue[]) => void;

const hub = vi.hoisted(() => {
  const listeners = new Map<string, Set<EventCallback>>();
  return {
    listeners,
    start: vi.fn(async (): Promise<void> => undefined),
    on: vi.fn((method: string, callback: EventCallback): (() => void) => {
      method = method.toLowerCase();
      const callbacks = listeners.get(method) ?? new Set<EventCallback>();
      callbacks.add(callback);
      listeners.set(method, callbacks);
      return () => callbacks.delete(callback);
    }),
    emit(method: string, ...args: JsonValue[]): void {
      for (const callback of listeners.get(method.toLowerCase()) ?? []) {
        callback(...args);
      }
    },
  };
});

vi.mock("../../shared/signalr/eventHub", () => ({ eventHub: hub }));

const file: NodeFileManifestDto = {
  id: "file-1",
  nodeId: "folder-1",
  ownerId: "owner-1",
  name: "photo.jpg",
  contentType: "image/jpeg",
  sizeBytes: 1024,
  createdAt: "2026-09-16T12:00:00Z",
  updatedAt: "2026-09-16T12:00:00Z",
  metadata: {},
  requiresVideoTranscoding: false,
  previewHashEncryptedHex: null,
};
const recentKey = queryKeys.layouts.recentFiltered(
  "layout-1",
  12,
  [],
  [],
  true,
);
const imagesKey = queryKeys.layouts.recentFiltered(
  "layout-1",
  6,
  ["image/"],
  [],
  true,
);
const otherLayoutKey = queryKeys.layouts.recentFiltered(
  "layout-2",
  12,
  [],
  [],
  true,
);

describe("useHomeRealtimeEvents", () => {
  let queryClient: QueryClient;

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: Infinity } },
    });
    queryClient.setQueryData(recentKey, [file, { ...file, id: "file-2" }]);
    queryClient.setQueryData(imagesKey, [file]);
    queryClient.setQueryData(otherLayoutKey, [file]);
  });

  afterEach(() => {
    queryClient.clear();
    hub.listeners.clear();
    vi.restoreAllMocks();
    vi.clearAllMocks();
    vi.useRealTimers();
  });

  it.each([HUB_METHODS.PreviewGenerated, "previewgenerated"])(
    "updates visible and filtered recent files on %s without refetching",
    async (method) => {
      const fetchFiles = vi.fn(async () => [file]);
      const invalidate = vi.spyOn(queryClient, "invalidateQueries");
      const update = vi.spyOn(queryClient, "setQueriesData");
      const { result } = renderHook(
        () => {
          useHomeRealtimeEvents(true, "layout-1");
          return useQuery({
            queryKey: recentKey,
            queryFn: fetchFiles,
            staleTime: Infinity,
          });
        },
        { wrapper },
      );

      act(() => hub.emit(method, file.nodeId, file.id, "abcd"));

      await waitFor(() => {
        expect(result.current.data?.[0].previewHashEncryptedHex).toBe("abcd");
      });
      expect(result.current.data?.[1].previewHashEncryptedHex).toBeNull();
      expect(queryClient.getQueryData(imagesKey)).toEqual([
        { ...file, previewHashEncryptedHex: "abcd" },
      ]);
      expect(queryClient.getQueryData(otherLayoutKey)).toEqual([file]);
      expect(fetchFiles).not.toHaveBeenCalled();
      expect(invalidate).not.toHaveBeenCalled();
      expect(update).toHaveBeenCalledOnce();
    },
  );

  it("ignores malformed payloads and events for files outside the cached folder", () => {
    renderHook(() => useHomeRealtimeEvents(true, "layout-1"), { wrapper });

    act(() => {
      hub.emit(HUB_METHODS.PreviewGenerated, file.nodeId, file.id, null);
      hub.emit(HUB_METHODS.PreviewGenerated, { nodeId: file.nodeId });
      hub.emit(HUB_METHODS.PreviewGenerated, "other-folder", file.id, "abcd");
      hub.emit(
        HUB_METHODS.PreviewGenerated,
        file.nodeId,
        "missing-file",
        "abcd",
      );
    });

    expect(queryClient.getQueryData(imagesKey)).toEqual([file]);
    expect(queryClient.getQueryCache().getAll()).toHaveLength(3);
  });

  it.each([
    { enabled: false, layoutId: "layout-1" },
    { enabled: true, layoutId: undefined },
  ])(
    "does not subscribe without authentication and a layout: %o",
    (options) => {
      renderHook(
        () => useHomeRealtimeEvents(options.enabled, options.layoutId),
        {
          wrapper,
        },
      );

      expect(hub.start).not.toHaveBeenCalled();
      expect(hub.on).not.toHaveBeenCalled();
    },
  );

  it("moves the subscription with the layout and removes it on logout and unmount", () => {
    const { rerender, unmount } = renderHook(
      ({ enabled, layoutId }) => useHomeRealtimeEvents(enabled, layoutId),
      { wrapper, initialProps: { enabled: true, layoutId: "layout-1" } },
    );
    rerender({ enabled: true, layoutId: "layout-2" });
    act(() =>
      hub.emit(HUB_METHODS.PreviewGenerated, file.nodeId, file.id, "abcd"),
    );
    expect(queryClient.getQueryData(imagesKey)).toEqual([file]);
    expect(queryClient.getQueryData(otherLayoutKey)).toEqual([
      { ...file, previewHashEncryptedHex: "abcd" },
    ]);

    rerender({ enabled: false, layoutId: "layout-2" });
    expect(hub.listeners.get("previewgenerated")?.size).toBe(0);
    rerender({ enabled: true, layoutId: "layout-2" });
    unmount();
    expect(hub.listeners.get("previewgenerated")?.size).toBe(0);
  });

  it("continues to refresh the overview after file creation", () => {
    vi.useFakeTimers();
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");
    renderHook(() => useHomeRealtimeEvents(true, "layout-1"), { wrapper });

    act(() => {
      hub.emit(HUB_METHODS.FileCreated, { id: "new-file" });
      vi.advanceTimersByTime(250);
    });

    expect(invalidate).toHaveBeenCalledWith({
      queryKey: queryKeys.layouts.recentAll("layout-1"),
    });
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: queryKeys.layouts.stats("layout-1"),
    });
  });
});
