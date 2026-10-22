import { act, cleanup, renderHook } from "@testing-library/react";
import { QueryClientProvider } from "@tanstack/react-query";
import type { PropsWithChildren } from "react";
import { afterEach, expect, it, vi } from "vitest";
import { eventHub } from "../../signalr/eventHub";
import { HUB_METHODS, type HubMethod } from "../../signalr/hubMethods";
import type { JsonValue } from "../../types/json";
import { queryClient } from "./queryClient";
import { queryKeys } from "./queryKeys";
import { useFolderSizeInvalidation } from "./useFolderSizeInvalidation";

const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);

afterEach(() => {
  cleanup();
  queryClient.clear();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

it("expires cached ancestors for nested file changes, but not renames", async () => {
  vi.useFakeTimers();
  const listeners = new Map<HubMethod, (...args: JsonValue[]) => void>();
  vi.spyOn(eventHub, "start").mockResolvedValue();
  vi.spyOn(eventHub, "on").mockImplementation((method, callback) => {
    listeners.set(method, callback);
    return () => {
      listeners.delete(method);
    };
  });
  const totalKey = queryKeys.nodeChildren.folderSizes("parent", "user");
  const childKey = queryKeys.nodeChildren.folderInfo("child", "user");
  queryClient.setQueryData(totalKey, 100);
  queryClient.setQueryData(childKey, { sizeBytes: 100, folders: 0, files: 1 });
  renderHook(() => useFolderSizeInvalidation(true), { wrapper });
  await act(async () => {
    listeners.get(HUB_METHODS.FileRenamed)?.({ nodeId: "descendant" });
    await vi.advanceTimersByTimeAsync(300);
  });
  expect(queryClient.getQueryState(totalKey)?.isInvalidated).toBe(false);
  await act(async () => {
    listeners.get(HUB_METHODS.FileCreated)?.({ nodeId: "descendant" });
    await vi.advanceTimersByTimeAsync(300);
  });
  expect(queryClient.getQueryState(totalKey)?.isInvalidated).toBe(true);
  expect(queryClient.getQueryState(childKey)?.isInvalidated).toBe(true);
});
