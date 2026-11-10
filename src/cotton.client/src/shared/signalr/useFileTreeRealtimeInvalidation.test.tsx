import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { JsonValue } from "../types/json";
import { HUB_METHODS } from "./hubMethods";
import { useFileTreeRealtimeInvalidation } from "./useFileTreeRealtimeInvalidation";

type EventCallback = (...args: JsonValue[]) => void;

const eventHubMock = vi.hoisted(() => {
  const listeners = new Map<string, Set<EventCallback>>();
  const connected = new Set<(isReconnect: boolean) => void>();

  return {
    listeners,
    connected,
    onConnected: vi.fn((callback: (isReconnect: boolean) => void) => {
      connected.add(callback);
      return () => connected.delete(callback);
    }),
    start: vi.fn(async (): Promise<void> => undefined),
    on: vi.fn((method: string, callback: EventCallback): (() => void) => {
      method = method.toLowerCase();
      const callbacks = listeners.get(method) ?? new Set<EventCallback>();
      callbacks.add(callback);
      listeners.set(method, callbacks);
      return () => {
        callbacks.delete(callback);
      };
    }),
    emit(method: string, ...args: JsonValue[]): void {
      for (const callback of listeners.get(method.toLowerCase()) ?? []) {
        callback(...args);
      }
    },
  };
});

vi.mock("./eventHub", () => ({
  eventHub: eventHubMock,
}));

describe("useFileTreeRealtimeInvalidation", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
    eventHubMock.listeners.clear();
    eventHubMock.connected.clear();
  });

  it("coalesces accepted mutation events and ignores rejected ones", () => {
    vi.useFakeTimers();
    const onInvalidate = vi.fn();
    const shouldInvalidate = vi.fn(
      (method: string) => method === HUB_METHODS.FileCreated,
    );
    const { unmount } = renderHook(() =>
      useFileTreeRealtimeInvalidation({
        enabled: true,
        onInvalidate,
        shouldInvalidate,
      }),
    );

    act(() => {
      eventHubMock.emit(HUB_METHODS.FileCreated, { id: "file-1" });
      eventHubMock.emit(HUB_METHODS.FileCreated, { id: "file-2" });
      eventHubMock.emit(HUB_METHODS.FileDeleted, { nodeFileId: "file-3" });
      vi.advanceTimersByTime(250);
    });

    expect(eventHubMock.start).toHaveBeenCalledOnce();
    expect(onInvalidate).toHaveBeenCalledOnce();
    expect(shouldInvalidate).toHaveBeenCalledTimes(3);
    unmount();
    expect(eventHubMock.listeners.get("filecreated")?.size).toBe(0);
  });

  it("does not connect or subscribe while disabled", () => {
    renderHook(() =>
      useFileTreeRealtimeInvalidation({
        enabled: false,
        onInvalidate: vi.fn(),
      }),
    );

    expect(eventHubMock.start).not.toHaveBeenCalled();
    expect(eventHubMock.on).not.toHaveBeenCalled();
  });

  it("refreshes after reconnecting without adding a request on initial connection", () => {
    vi.useFakeTimers();
    const onInvalidate = vi.fn();
    const { unmount } = renderHook(() =>
      useFileTreeRealtimeInvalidation({ enabled: true, onInvalidate }),
    );
    act(() => {
      for (const callback of eventHubMock.connected) {
        callback(false);
      }
      vi.advanceTimersByTime(1000);
    });
    expect(onInvalidate).not.toHaveBeenCalled();
    act(() => {
      for (const callback of eventHubMock.connected) {
        callback(true);
      }
      vi.advanceTimersByTime(250);
    });
    expect(onInvalidate).toHaveBeenCalledOnce();
    unmount();
    expect(eventHubMock.connected.size).toBe(0);
  });

  it("flushes a continuous mutation burst at the maximum wait", () => {
    vi.useFakeTimers();
    const onInvalidate = vi.fn();
    renderHook(() =>
      useFileTreeRealtimeInvalidation({ enabled: true, onInvalidate }),
    );

    act(() => {
      eventHubMock.emit(HUB_METHODS.FileCreated, { id: "file-1" });
      for (let elapsed = 200; elapsed < 1000; elapsed += 200) {
        vi.advanceTimersByTime(200);
        eventHubMock.emit(HUB_METHODS.FileCreated, { id: `file-${elapsed}` });
      }
    });
    expect(onInvalidate).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(200);
    });
    expect(onInvalidate).toHaveBeenCalledOnce();
  });

  it("cancels a pending invalidation on unmount", () => {
    vi.useFakeTimers();
    const onInvalidate = vi.fn();
    const { unmount } = renderHook(() =>
      useFileTreeRealtimeInvalidation({ enabled: true, onInvalidate }),
    );

    act(() => {
      eventHubMock.emit(HUB_METHODS.FileCreated, { id: "file-1" });
    });
    unmount();
    act(() => {
      vi.runAllTimers();
    });

    expect(onInvalidate).not.toHaveBeenCalled();
  });
});
