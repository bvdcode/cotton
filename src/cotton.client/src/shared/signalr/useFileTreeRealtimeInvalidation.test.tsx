import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { JsonValue } from "../types/json";
import { HUB_METHODS } from "./hubMethods";
import { useFileTreeRealtimeInvalidation } from "./useFileTreeRealtimeInvalidation";

type EventCallback = (...args: JsonValue[]) => void;

const eventHubMock = vi.hoisted(() => {
  const listeners = new Map<string, Set<EventCallback>>();

  return {
    listeners,
    start: vi.fn(async (): Promise<void> => undefined),
    on: vi.fn((method: string, callback: EventCallback): (() => void) => {
      const callbacks = listeners.get(method) ?? new Set<EventCallback>();
      callbacks.add(callback);
      listeners.set(method, callbacks);
      return () => {
        callbacks.delete(callback);
      };
    }),
    emit(method: string, ...args: JsonValue[]): void {
      for (const callback of listeners.get(method) ?? []) {
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
  });

  it("coalesces accepted mutation events and ignores rejected ones", () => {
    vi.useFakeTimers();
    const onInvalidate = vi.fn();
    const { unmount } = renderHook(() =>
      useFileTreeRealtimeInvalidation({
        enabled: true,
        onInvalidate,
        shouldInvalidate: (method) => method === HUB_METHODS.FileCreated,
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
    unmount();
    expect(eventHubMock.listeners.get(HUB_METHODS.FileCreated)?.size).toBe(0);
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
