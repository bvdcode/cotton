import { afterEach, describe, expect, it, vi } from "vitest";
import type { JsonValue } from "../types/json";
import {
  PREVIEW_UPDATE_DELAY_MS,
  subscribeToPreviewGenerated,
} from "./subscribeToPreviewGenerated";

const hub = vi.hoisted(() => ({
  receive: (...args: JsonValue[]): void => {
    void args;
  },
  unsubscribe: vi.fn(),
}));
vi.mock("./eventHub", () => ({
  eventHub: {
    on: (_method: string, callback: (...args: JsonValue[]) => void) => {
      hub.receive = callback;
      return hub.unsubscribe;
    },
  },
}));

afterEach(() => {
  vi.useRealTimers();
  vi.clearAllMocks();
});

describe("preview event batching", () => {
  it("merges a burst by folder and file, preserving the newest hash", async () => {
    vi.useFakeTimers();
    const update = vi.fn();
    const unsubscribe = subscribeToPreviewGenerated(update);
    for (let index = 0; index < 100; index++) {
      hub.receive("folder", "file", String(index));
    }
    hub.receive("another-folder", "other-file", "ready");
    hub.receive("folder", "invalid", null);
    expect(update).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(PREVIEW_UPDATE_DELAY_MS);
    expect(update).toHaveBeenCalledExactlyOnceWith(
      new Map([
        ["folder", new Map([["file", "99"]])],
        ["another-folder", new Map([["other-file", "ready"]])],
      ]),
    );
    unsubscribe();
  });

  it("flushes continuous events and cancels pending work on unmount", async () => {
    vi.useFakeTimers();
    const update = vi.fn();
    const unsubscribe = subscribeToPreviewGenerated(update);
    for (let index = 0; index < 10; index++) {
      hub.receive("folder", "file", String(index));
      await vi.advanceTimersByTimeAsync(PREVIEW_UPDATE_DELAY_MS / 5);
    }
    expect(update).toHaveBeenCalledTimes(2);
    hub.receive("folder", "file", "later");
    unsubscribe();
    await vi.advanceTimersByTimeAsync(PREVIEW_UPDATE_DELAY_MS);
    expect(update).toHaveBeenCalledTimes(2);
    expect(hub.unsubscribe).toHaveBeenCalledOnce();
  });
});
