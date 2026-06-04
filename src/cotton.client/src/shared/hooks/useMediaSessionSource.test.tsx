import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { useMediaSessionSource } from "./useMediaSessionSource";

const coordinatorMock = vi.hoisted(() => ({
  upsertSource: vi.fn(),
  removeSource: vi.fn(),
  updateSourcePlayback: vi.fn(),
  updateSourcePosition: vi.fn(),
}));

vi.mock("../utils/mediaSessionCoordinator", () => ({
  mediaSessionCoordinator: coordinatorMock,
}));

type FakeMediaElement = HTMLMediaElement & {
  emit: (type: string) => void;
  paused: boolean;
  readyState: number;
  networkState: number;
};

const createFakeMediaElement = (): FakeMediaElement => {
  const listeners = new Map<string, Set<EventListenerOrEventListenerObject>>();

  return {
    paused: false,
    readyState: 4,
    networkState: 1,
    duration: 120,
    currentTime: 0,
    playbackRate: 1,
    addEventListener: vi.fn((type, listener) => {
      const group = listeners.get(type) ?? new Set();
      group.add(listener);
      listeners.set(type, group);
    }),
    removeEventListener: vi.fn((type, listener) => {
      listeners.get(type)?.delete(listener);
    }),
    emit: (type: string) => {
      for (const listener of listeners.get(type) ?? []) {
        if (typeof listener === "function") {
          listener(new Event(type));
        } else {
          listener.handleEvent(new Event(type));
        }
      }
    },
  } as unknown as FakeMediaElement;
};

describe("useMediaSessionSource", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it("keeps a playing source active through the transient pause fired by a track src swap", () => {
    vi.useFakeTimers();
    const mediaElement = createFakeMediaElement();

    const { rerender, unmount } = renderHook(
      ({ title }) =>
        useMediaSessionSource({
          mediaElement,
          track: { title },
          priority: 10,
        }),
      { initialProps: { title: "Track A" } },
    );

    coordinatorMock.updateSourcePlayback.mockClear();

    rerender({ title: "Track B" });

    act(() => {
      Object.assign(mediaElement, {
        paused: true,
        readyState: 0,
        networkState: 2,
      });
      mediaElement.emit("pause");
    });

    expect(coordinatorMock.updateSourcePlayback).toHaveBeenLastCalledWith(
      expect.any(String),
      "playing",
    );

    act(() => {
      vi.advanceTimersByTime(1500);
    });

    expect(coordinatorMock.updateSourcePlayback).toHaveBeenLastCalledWith(
      expect.any(String),
      "paused",
    );

    unmount();
  });
});
