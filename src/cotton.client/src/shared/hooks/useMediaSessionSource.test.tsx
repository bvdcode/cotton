import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { useMediaSessionSource } from "./useMediaSessionSource";

const coordinatorMock = vi.hoisted(() => ({
  upsertSource: vi.fn(),
  removeSource: vi.fn(),
  updateSourcePlayback: vi.fn(),
  updateSourcePosition: vi.fn(),
  reassertSource: vi.fn(),
}));

vi.mock("../utils/mediaSessionCoordinator", () => ({
  mediaSessionCoordinator: coordinatorMock,
}));

const createFakeMediaElement = () => {
  const element = document.createElement("audio");
  Object.defineProperties(element, {
    paused: { value: false, writable: true },
    readyState: { value: 4, writable: true },
    networkState: { value: 1, writable: true },
    duration: { value: 120 },
  });
  return Object.assign(element, {
    emit: (type: string) => element.dispatchEvent(new Event(type)),
  });
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
    expect(coordinatorMock.reassertSource).toHaveBeenCalledWith(
      expect.any(String),
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
